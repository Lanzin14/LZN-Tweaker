using System.Reflection;
using LznTweaker.Core.Execution;
using Microsoft.Win32;

namespace LznTweaker.Infrastructure;

/// <summary>Informações exibidas no rodapé da sidebar: versão do Windows, usuário, privilégios e versão do app.</summary>
public static class WindowsEnvironment
{
    public static string AppVersion { get; } =
        "v" + (Assembly.GetEntryAssembly()?.GetName().Version is { } version ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0");

    public static string UserName { get; } = Environment.UserName;

    public static bool IsElevated => ProcessElevation.IsElevated;

    public static string WindowsVersion { get; } = ReadWindowsVersion();

    private static string ReadWindowsVersion()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var name = Convert.ToString(key?.GetValue("ProductName")) ?? "Windows";
            var build = Convert.ToString(key?.GetValue("CurrentBuildNumber")) ?? "?";
            // O Registro do Windows 11 ainda informa "Windows 10" em ProductName; o build 22000+ é o Windows 11.
            if (int.TryParse(build, out var number) && number >= 22000 && name.StartsWith("Windows 10", StringComparison.OrdinalIgnoreCase))
                name = "Windows 11" + name["Windows 10".Length..];
            var display = Convert.ToString(key?.GetValue("DisplayVersion"));
            return $"{name}{(string.IsNullOrWhiteSpace(display) ? string.Empty : $" {display}")} · build {build}";
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            return "Windows (versão não lida)";
        }
    }
}
