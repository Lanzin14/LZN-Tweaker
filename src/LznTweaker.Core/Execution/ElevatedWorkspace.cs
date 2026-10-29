using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace LznTweaker.Core.Execution;

/// <summary>
/// Pasta onde scripts e payloads executados com privilégio de administrador são gravados.
/// No modo restrito, a pasta (e a pasta-pai) só pode ser alterada por Administradores e SYSTEM, para que
/// nenhum processo sem elevação troque o script entre a gravação e a execução elevada.
/// </summary>
public sealed class ElevatedWorkspace
{
    private static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
    private static readonly SecurityIdentifier LocalSystem = new(WellKnownSidType.LocalSystemSid, null);

    // Direitos que permitiriam alterar, trocar ou apagar o conteúdo da pasta.
    private const FileSystemRights WriteRights =
        FileSystemRights.WriteData | FileSystemRights.AppendData | FileSystemRights.WriteExtendedAttributes |
        FileSystemRights.WriteAttributes | FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles |
        FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;

    private readonly object _sync = new();
    private bool _rootReady;

    public ElevatedWorkspace(string rootPath, bool restrictToAdministrators)
    {
        RootPath = Path.GetFullPath(rootPath);
        RestrictToAdministrators = restrictToAdministrators;
    }

    public string RootPath { get; }
    public bool RestrictToAdministrators { get; }

    /// <summary>%ProgramData%\LznTweaker\Operations, protegida por ACL.</summary>
    public static ElevatedWorkspace CreateDefault() => new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LznTweaker", "Operations"),
        restrictToAdministrators: true);

    /// <summary>Cria uma subpasta exclusiva para uma execução; ela é apagada no <see cref="ScriptRunDirectory.Dispose"/>.</summary>
    public ScriptRunDirectory CreateRunDirectory(string prefix)
    {
        EnsureRoot();
        var safePrefix = new string(prefix.Where(char.IsLetterOrDigit).Take(32).ToArray());
        var directory = Path.Combine(RootPath, $"{(safePrefix.Length == 0 ? "run" : safePrefix)}-{Guid.NewGuid():N}");
        // A subpasta herda a ACL protegida da raiz.
        Directory.CreateDirectory(directory);
        return new ScriptRunDirectory(directory);
    }

    private void EnsureRoot()
    {
        lock (_sync)
        {
            if (_rootReady)
                return;

            if (!RestrictToAdministrators)
            {
                Directory.CreateDirectory(RootPath);
                _rootReady = true;
                return;
            }

            if (!ProcessElevation.IsElevated)
                throw new UnauthorizedAccessException("O espaço de execução protegido exige que o LznTweaker esteja em execução como administrador.");

            // A pasta-pai também é protegida: quem controla a pai poderia trocar a pasta de operações inteira.
            var parent = Directory.GetParent(RootPath)?.FullName
                         ?? throw new InvalidOperationException("O espaço de execução precisa de uma pasta-pai.");
            EnsureProtectedDirectory(parent);
            EnsureProtectedDirectory(RootPath);
            _rootReady = true;
        }
    }

    private static void EnsureProtectedDirectory(string path)
    {
        var directory = new DirectoryInfo(path);
        if (!directory.Exists)
        {
            directory.Create(CreateProtectedSecurity());
            directory.Refresh();
        }
        else if (!IsProtected(directory))
        {
            // Corrige uma pasta pré-existente (por exemplo, criada por outro processo). Se o Windows recusar,
            // a execução é interrompida em vez de usar uma pasta que terceiros podem alterar.
            directory.SetAccessControl(CreateProtectedSecurity());
            directory.Refresh();
        }

        if (!IsProtected(directory))
            throw new UnauthorizedAccessException($"A pasta {path} não pôde ser restrita a administradores; nenhuma operação elevada será executada.");
    }

    internal static bool IsProtected(DirectoryInfo directory)
    {
        if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            return false;

        var security = directory.GetAccessControl();
        var owner = security.GetOwner(typeof(SecurityIdentifier));
        if (owner is null || (!owner.Equals(Administrators) && !owner.Equals(LocalSystem)))
            return false;

        foreach (FileSystemAccessRule rule in security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier)))
        {
            if (rule.AccessControlType == AccessControlType.Allow &&
                !rule.IdentityReference.Equals(Administrators) && !rule.IdentityReference.Equals(LocalSystem) &&
                (rule.FileSystemRights & WriteRights) != 0)
                return false;
        }
        return true;
    }

    private static DirectorySecurity CreateProtectedSecurity()
    {
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetOwner(Administrators);
        const InheritanceFlags inheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        security.AddAccessRule(new FileSystemAccessRule(LocalSystem, FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(Administrators, FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));
        return security;
    }
}

/// <summary>Subpasta de uma única execução; apagada ao final, inclusive em caso de erro.</summary>
public sealed class ScriptRunDirectory(string path) : IDisposable
{
    public string Path { get; } = path;

    public string GetFilePath(string fileName) => System.IO.Path.Combine(Path, fileName);

    public void Dispose()
    {
        try { if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

public static class ProcessElevation
{
    public static bool IsElevated { get; } =
        new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
}
