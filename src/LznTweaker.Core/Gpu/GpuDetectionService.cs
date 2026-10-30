using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace LznTweaker.Core.Gpu;

public enum GpuVendor { Unknown, Nvidia, Amd, Intel }

public enum GpuDetectionStatus { NotChecked, Available, Unavailable }

/// <param name="DriverVersion">Versão do driver como o Windows registra (ex.: 32.0.21045.16010).</param>
/// <param name="VendorSoftwareVersion">Versão no padrão do fabricante (GeForce 560.94 / Adrenalin 26.10.1), quando dá para obter sem estimar.</param>
public sealed record GpuAdapter(string Name, GpuVendor Vendor, string? DriverVersion, string? VendorSoftwareVersion, DateOnly? DriverDate);

public sealed record GpuDetectionResult(GpuDetectionStatus Status, IReadOnlyList<GpuAdapter> Adapters, int? RefreshRateHz, string? Error)
{
    public static GpuDetectionResult NotChecked { get; } = new(GpuDetectionStatus.NotChecked, [], null, null);

    /// <summary>A GPU dedicada (NVIDIA/AMD) tem prioridade sobre a integrada.</summary>
    public GpuAdapter? Primary => Adapters.FirstOrDefault(adapter => adapter.Vendor is GpuVendor.Nvidia or GpuVendor.Amd) ?? Adapters.FirstOrDefault();
}

/// <summary>
/// Detecta a placa de vídeo pela chave de classe de vídeo do Windows (somente leitura) e a taxa de atualização atual
/// pela API EnumDisplaySettings. Nada é alterado; quando um dado não existe, ele fica nulo em vez de estimado.
/// </summary>
public sealed class GpuDetectionService
{
    private const string DisplayClass = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
    private readonly Func<IReadOnlyList<IReadOnlyDictionary<string, string?>>> _readAdapters;
    private readonly Func<int?> _readRefreshRate;

    public GpuDetectionService() : this(ReadAdapterKeys, ReadCurrentRefreshRate) { }

    internal GpuDetectionService(Func<IReadOnlyList<IReadOnlyDictionary<string, string?>>> readAdapters, Func<int?> readRefreshRate)
    {
        _readAdapters = readAdapters;
        _readRefreshRate = readRefreshRate;
    }

    public GpuDetectionResult Detect()
    {
        int? refreshRate = null;
        try { refreshRate = _readRefreshRate(); } catch (Exception) { }

        try
        {
            var adapters = _readAdapters()
                .Select(ToAdapter)
                .Where(adapter => adapter is not null)
                .Select(adapter => adapter!)
                .DistinctBy(adapter => adapter.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return adapters.Length == 0
                ? new GpuDetectionResult(GpuDetectionStatus.Unavailable, [], refreshRate, "O Windows não informou nenhuma placa de vídeo além do adaptador básico.")
                : new GpuDetectionResult(GpuDetectionStatus.Available, adapters, refreshRate, null);
        }
        catch (Exception ex)
        {
            return new GpuDetectionResult(GpuDetectionStatus.Unavailable, [], refreshRate, ex.Message);
        }
    }

    internal static GpuAdapter? ToAdapter(IReadOnlyDictionary<string, string?> key)
    {
        var name = key.GetValueOrDefault("DriverDesc")?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Contains("Basic Display", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Basic Render", StringComparison.OrdinalIgnoreCase))
            return null;

        var vendor = ParseVendor(key.GetValueOrDefault("MatchingDeviceId"), key.GetValueOrDefault("ProviderName"));
        var driverVersion = key.GetValueOrDefault("DriverVersion");
        var vendorVersion = vendor switch
        {
            GpuVendor.Nvidia => NvidiaDriverVersion(driverVersion),
            GpuVendor.Amd => key.GetValueOrDefault("RadeonSoftwareVersion"),
            _ => null
        };
        return new GpuAdapter(name, vendor, driverVersion, string.IsNullOrWhiteSpace(vendorVersion) ? null : vendorVersion, ParseDriverDate(key.GetValueOrDefault("DriverDate")));
    }

    /// <summary>Identifica o fabricante pelo ID PCI (VEN_10DE NVIDIA, VEN_1002 AMD, VEN_8086 Intel); o nome do provedor é só reserva.</summary>
    internal static GpuVendor ParseVendor(string? matchingDeviceId, string? providerName)
    {
        var id = matchingDeviceId?.ToUpperInvariant() ?? "";
        if (id.Contains("VEN_10DE")) return GpuVendor.Nvidia;
        if (id.Contains("VEN_1002")) return GpuVendor.Amd;
        if (id.Contains("VEN_8086")) return GpuVendor.Intel;

        var provider = providerName ?? "";
        if (provider.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)) return GpuVendor.Nvidia;
        if (provider.Contains("Advanced Micro Devices", StringComparison.OrdinalIgnoreCase) || provider.StartsWith("AMD", StringComparison.OrdinalIgnoreCase)) return GpuVendor.Amd;
        if (provider.Contains("Intel", StringComparison.OrdinalIgnoreCase)) return GpuVendor.Intel;
        return GpuVendor.Unknown;
    }

    /// <summary>
    /// A versão GeForce são os últimos cinco dígitos da versão do Windows: 32.0.15.6094 → 560.94.
    /// Devolve nulo se o formato não for o esperado, em vez de inventar um número.
    /// </summary>
    internal static string? NvidiaDriverVersion(string? windowsVersion)
    {
        var parts = windowsVersion?.Split('.');
        if (parts is not { Length: 4 } || !parts.All(part => part.Length > 0 && part.All(char.IsAsciiDigit)))
            return null;
        var digits = parts[2] + parts[3].PadLeft(4, '0');
        if (digits.Length < 5)
            return null;
        var last = digits[^5..];
        return $"{int.Parse(last[..3], CultureInfo.InvariantCulture)}.{last[3..]}";
    }

    /// <summary>O registro guarda a data do driver como MM-dd-yyyy.</summary>
    internal static DateOnly? ParseDriverDate(string? value) =>
        DateOnly.TryParseExact(value?.Trim(), ["M-d-yyyy", "MM-dd-yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    private static IReadOnlyList<IReadOnlyDictionary<string, string?>> ReadAdapterKeys()
    {
        using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(DisplayClass);
        if (root is null)
            return [];

        var result = new List<IReadOnlyDictionary<string, string?>>();
        foreach (var subKeyName in root.GetSubKeyNames())
        {
            // Algumas subchaves (ex.: Properties) negam leitura; cada uma é tentada separadamente.
            try
            {
                using var adapter = root.OpenSubKey(subKeyName);
                if (adapter is null)
                    continue;
                var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var name in new[] { "DriverDesc", "DriverVersion", "ProviderName", "DriverDate", "MatchingDeviceId", "RadeonSoftwareVersion" })
                    values[name] = Convert.ToString(adapter.GetValue(name), CultureInfo.InvariantCulture);
                result.Add(values);
            }
            catch (Exception) { }
        }
        return result;
    }

    private static int? ReadCurrentRefreshRate()
    {
        var mode = new DevMode { dmSize = (short)Marshal.SizeOf<DevMode>() };
        // ENUM_CURRENT_SETTINGS = -1; null = monitor principal.
        return EnumDisplaySettings(null, -1, ref mode) && mode.dmDisplayFrequency > 1 ? mode.dmDisplayFrequency : null;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "EnumDisplaySettingsW")]
    private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DevMode devMode);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }
}
