using Microsoft.Win32;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;

namespace LznTweaker.Core;

/// <summary>
/// Coleta informações somente para leitura. WMI e medições são executados fora da UI.
/// </summary>
public sealed class SystemInformationService
{
    public Task<SystemSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Read(cancellationToken), cancellationToken);

    private static SystemSnapshot Read(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var wmiWarnings = new List<string>();

        var operatingSystem = QueryWmiSafely("Windows", "SELECT Caption, Version, BuildNumber, OSArchitecture FROM Win32_OperatingSystem", wmiWarnings).FirstOrDefault();
        var processor = QueryWmiSafely("processador", "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor", wmiWarnings).FirstOrDefault();
        var memoryModules = QueryWmiSafely("memória", "SELECT Capacity, ConfiguredClockSpeed FROM Win32_PhysicalMemory", wmiWarnings);
        var graphics = QueryWmiSafely("placa de vídeo", "SELECT Name FROM Win32_VideoController", wmiWarnings);
        var motherboard = QueryWmiSafely("placa-mãe", "SELECT Manufacturer, Product FROM Win32_BaseBoard", wmiWarnings).FirstOrDefault();
        var diskRows = QueryWmiSafely("atividade de disco", "SELECT Name, PercentDiskTime, DiskReadBytesPersec, DiskWriteBytesPersec FROM Win32_PerfFormattedData_PerfDisk_PhysicalDisk", wmiWarnings);

        cancellationToken.ThrowIfCancellationRequested();

        var memoryStatus = ReadMemoryStatus(out var memoryStatusRead);
        var totalMemory = memoryModules.Sum(module => ParseLong(module.GetValueOrDefault("Capacity")));
        if (totalMemory <= 0 && memoryStatusRead)
            totalMemory = memoryStatus.TotalPhysical > long.MaxValue ? long.MaxValue : (long)memoryStatus.TotalPhysical;
        var speeds = memoryModules
            .Select(module => ParseInt(module.GetValueOrDefault("ConfiguredClockSpeed")))
            .Where(speed => speed > 0)
            .ToArray();
        var memorySpeed = speeds.Length == 0 ? 0 : (int)Math.Round(speeds.Average());
        var windowsIdentity = ReadWindowsIdentity(operatingSystem);
        var windowsBuild = windowsIdentity.Build;
        var drives = ReadDrives();
        var cpuUsage = ReadCpuUsage();
        long? memoryAvailable = memoryStatusRead ? checked((long)memoryStatus.AvailablePhysical) : null;
        long? memoryCommitLimit = memoryStatusRead ? checked((long)memoryStatus.TotalPageFile) : null;
        long? memoryCommitted = memoryStatusRead
            ? Math.Max(0, memoryCommitLimit!.Value - checked((long)memoryStatus.AvailablePageFile))
            : null;
        var diskTotal = diskRows.FirstOrDefault(row => row.GetValueOrDefault("Name")?.Contains("_Total", StringComparison.OrdinalIgnoreCase) == true)
                        ?? diskRows.FirstOrDefault();
        var diskActivity = diskTotal is null ? null : new DiskActivitySnapshot(
            Math.Clamp(ParseDouble(diskTotal.GetValueOrDefault("PercentDiskTime")), 0, 100),
            ParseLong(diskTotal.GetValueOrDefault("DiskReadBytesPersec")),
            ParseLong(diskTotal.GetValueOrDefault("DiskWriteBytesPersec")));
        var boardName = string.Join(" ", new[]
        {
            motherboard?.GetValueOrDefault("Manufacturer"), motherboard?.GetValueOrDefault("Product")
        }.Where(part => !string.IsNullOrWhiteSpace(part)));
        if (string.IsNullOrWhiteSpace(boardName))
            boardName = ReadMotherboardName();

        var processorName = processor?.GetValueOrDefault("Name");
        if (string.IsNullOrWhiteSpace(processorName))
            processorName = ReadProcessorName();
        var processorCores = ParseInt(processor?.GetValueOrDefault("NumberOfCores"));
        if (processorCores <= 0)
            processorCores = ReadPhysicalCoreCount();
        var processorThreads = ParseInt(processor?.GetValueOrDefault("NumberOfLogicalProcessors"));
        if (processorThreads <= 0)
            processorThreads = Environment.ProcessorCount;
        var processorClock = ParseInt(processor?.GetValueOrDefault("MaxClockSpeed"));
        if (processorClock <= 0)
            processorClock = ReadProcessorClockMHz();

        var graphicsNames = graphics.Select(item => item.GetValueOrDefault("Name"))
            .Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (graphicsNames.Length == 0)
            graphicsNames = ReadGraphicsNames();

        // Report only fields that remain unavailable after trying native/registry fallbacks.
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(windowsIdentity.Name) || windowsIdentity.Build.Contains("desconhecida", StringComparison.OrdinalIgnoreCase)) warnings.Add("Windows");
        if (string.IsNullOrWhiteSpace(processorName)) warnings.Add("processador");
        if (totalMemory <= 0) warnings.Add("memória");
        if (memorySpeed <= 0) warnings.Add("frequência da memória");
        if (graphicsNames.Length == 0) warnings.Add("placa de vídeo");
        if (string.IsNullOrWhiteSpace(boardName)) warnings.Add("placa-mãe");
        if (diskRows.Count == 0) warnings.Add("atividade de disco");

        return new SystemSnapshot(
            string.IsNullOrWhiteSpace(windowsIdentity.Name) ? "Windows (informação indisponível)" : windowsIdentity.Name,
            windowsIdentity.Version,
            windowsBuild,
            operatingSystem?.GetValueOrDefault("OSArchitecture") ?? (Environment.Is64BitOperatingSystem ? "64 bits" : "32 bits"),
            string.IsNullOrWhiteSpace(processorName) ? "Processador não identificado" : processorName,
            processorCores,
            processorThreads,
            processorClock,
            totalMemory,
            memorySpeed,
            memoryAvailable,
            memoryCommitted,
            memoryCommitLimit,
            string.Join(" · ", graphicsNames),
            string.IsNullOrWhiteSpace(boardName) ? "Não disponível" : boardName,
            cpuUsage,
            diskActivity,
            drives,
            warnings);
    }

    private static List<Dictionary<string, string?>> QueryWmiSafely(string component, string query, List<string> warnings)
    {
        try
        {
            return QueryWmi("root\\cimv2", query);
        }
        catch (Exception)
        {
            warnings.Add(component);
            return [];
        }
    }

    private static List<Dictionary<string, string?>> QueryWmi(string nameSpace, string query)
    {
        object? locator = null;
        object? services = null;
        object? rows = null;
        var results = new List<Dictionary<string, string?>>();

        try
        {
            var locatorType = Type.GetTypeFromProgID("WbemScripting.SWbemLocator", throwOnError: true)!;
            locator = Activator.CreateInstance(locatorType);
            dynamic dynamicLocator = locator!;
            services = dynamicLocator.ConnectServer(".", nameSpace);
            dynamic dynamicServices = services!;
            rows = dynamicServices.ExecQuery(query, "WQL", 0x10 | 0x20);

            foreach (var row in (IEnumerable)rows!)
            {
                if (row is null)
                    continue;

                var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                dynamic dynamicRow = row;
                foreach (var propertyName in GetPropertyNames(query))
                {
                    object? property = null;
                    try
                    {
                        dynamic properties = dynamicRow.Properties_;
                        property = properties.Item(propertyName);
                        dynamic dynamicProperty = property;
                        object? value = dynamicProperty.Value;
                        values[propertyName] = value?.ToString();
                    }
                    catch
                    {
                        values[propertyName] = null;
                    }
                    finally
                    {
                        ReleaseComObject(property);
                    }
                }

                results.Add(values);
                ReleaseComObject(row);
            }
        }
        finally
        {
            ReleaseComObject(rows);
            ReleaseComObject(services);
            ReleaseComObject(locator);
        }

        return results;
    }

    private static IEnumerable<string> GetPropertyNames(string query)
    {
        var select = query.IndexOf("SELECT ", StringComparison.OrdinalIgnoreCase);
        var from = query.IndexOf(" FROM ", StringComparison.OrdinalIgnoreCase);
        if (select < 0 || from <= select)
            return Array.Empty<string>();

        return query[(select + 7)..from]
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static List<DriveSnapshot> ReadDrives()
    {
        var drives = new List<DriveSnapshot>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.IsReady && drive.TotalSize > 0)
                    drives.Add(new DriveSnapshot(drive.Name.TrimEnd('\\'), drive.TotalSize, drive.AvailableFreeSpace));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        return drives;
    }

    private static (string? Name, string Version, string Build) ReadWindowsIdentity(Dictionary<string, string?>? operatingSystem)
    {
        try
        {
            using var key = OpenLocalMachineKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var buildNumber = Convert.ToString(key?.GetValue("CurrentBuildNumber")) ?? operatingSystem?.GetValueOrDefault("BuildNumber");
            var buildText = string.IsNullOrWhiteSpace(buildNumber) ? "desconhecida" : buildNumber;
            var updateRevision = key?.GetValue("UBR") is { } ubrValue && int.TryParse(Convert.ToString(ubrValue), out var ubr)
                ? $".{ubr}"
                : string.Empty;
            var productName = Convert.ToString(key?.GetValue("ProductName")) ?? operatingSystem?.GetValueOrDefault("Caption");
            if (int.TryParse(buildNumber, out var numericBuild) && numericBuild >= 22000 &&
                productName?.StartsWith("Windows 10", StringComparison.OrdinalIgnoreCase) == true)
                productName = "Windows 11" + productName["Windows 10".Length..];
            var displayVersion = Convert.ToString(key?.GetValue("DisplayVersion"));
            var version = string.IsNullOrWhiteSpace(displayVersion)
                ? $"Versão {operatingSystem?.GetValueOrDefault("Version") ?? "desconhecida"}"
                : displayVersion;
            return (productName, version, $"Build {buildText}{updateRevision}");
        }
        catch (Exception)
        {
            return (
                operatingSystem?.GetValueOrDefault("Caption"),
                $"Versão {operatingSystem?.GetValueOrDefault("Version") ?? "desconhecida"}",
                $"Build {operatingSystem?.GetValueOrDefault("BuildNumber") ?? "desconhecida"}");
        }
    }

    private static string? ReadProcessorName() => ReadProcessorRegistryValue("ProcessorNameString");

    private static int ReadProcessorClockMHz()
    {
        try
        {
            var value = ReadProcessorRegistryValue("~MHz");
            return ParseInt(value);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static string? ReadProcessorRegistryValue(string valueName)
    {
        try
        {
            using var key = OpenLocalMachineKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return Convert.ToString(key?.GetValue(valueName));
        }
        catch (Exception) { return null; }
    }

    private static string ReadMotherboardName()
    {
        try
        {
            using var key = OpenLocalMachineKey(@"HARDWARE\DESCRIPTION\System\BIOS");
            return string.Join(" ", new[]
            {
                Convert.ToString(key?.GetValue("BaseBoardManufacturer")),
                Convert.ToString(key?.GetValue("BaseBoardProduct"))
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }
        catch (Exception) { return string.Empty; }
    }

    private static string[] ReadGraphicsNames()
    {
        const string displayClass = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
        try
        {
            using var key = OpenLocalMachineKey(displayClass);
            if (key is null)
                return [];

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var subKeyName in key.GetSubKeyNames())
            {
                using var adapter = key.OpenSubKey(subKeyName);
                var name = Convert.ToString(adapter?.GetValue("DriverDesc")) ?? Convert.ToString(adapter?.GetValue("AdapterString"));
                if (!string.IsNullOrWhiteSpace(name) && !name.Contains("Basic Display", StringComparison.OrdinalIgnoreCase))
                    names.Add(name.Trim());
            }
            return names.ToArray();
        }
        catch (Exception) { return []; }
    }

    private static RegistryKey? OpenLocalMachineKey(string subKey) =>
        RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
            Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default).OpenSubKey(subKey);

    private static int ReadPhysicalCoreCount()
    {
        const int relationProcessorCore = 0;
        IntPtr buffer = IntPtr.Zero;
        try
        {
            _ = GetLogicalProcessorInformationEx(relationProcessorCore, IntPtr.Zero, out var requiredLength);
            if (requiredLength == 0 || requiredLength > 1024 * 1024)
                return 0;

            buffer = Marshal.AllocHGlobal((int)requiredLength);
            if (!GetLogicalProcessorInformationEx(relationProcessorCore, buffer, out var bufferLength))
                return 0;

            var count = 0;
            var offset = 0;
            var availableLength = (int)Math.Min(bufferLength, requiredLength);
            while (offset <= availableLength - 8)
            {
                var relationship = Marshal.ReadInt32(buffer, offset);
                var recordSize = Marshal.ReadInt32(buffer, offset + 4);
                if (recordSize < 8 || recordSize > availableLength - offset)
                    break;
                if (relationship == relationProcessorCore)
                    count++;
                offset += recordSize;
            }
            return count;
        }
        catch (Exception) { return 0; }
        finally
        {
            if (buffer != IntPtr.Zero)
                Marshal.FreeHGlobal(buffer);
        }
    }

    private static double ReadCpuUsage()
    {
        if (!GetSystemTimes(out var idleStart, out var kernelStart, out var userStart))
            return 0;

        Thread.Sleep(250);
        if (!GetSystemTimes(out var idleEnd, out var kernelEnd, out var userEnd))
            return 0;

        var idle = FileTimeToLong(idleEnd) - FileTimeToLong(idleStart);
        var total = (FileTimeToLong(kernelEnd) - FileTimeToLong(kernelStart)) +
                    (FileTimeToLong(userEnd) - FileTimeToLong(userStart));
        return total <= 0 ? 0 : Math.Clamp((total - idle) * 100d / total, 0, 100);
    }

    private static MemoryStatusEx ReadMemoryStatus(out bool success)
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        success = GlobalMemoryStatusEx(ref status);
        return success ? status : new MemoryStatusEx();
    }

    private static int ParseInt(string? value) => int.TryParse(value, out var result) ? result : 0;
    private static long ParseLong(string? value) => long.TryParse(value, out var result) ? result : 0;
    private static double ParseDouble(string? value) => double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : 0;
    private static long FileTimeToLong(System.Runtime.InteropServices.ComTypes.FILETIME value) =>
        ((long)value.dwHighDateTime << 32) | (uint)value.dwLowDateTime;

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
            Marshal.ReleaseComObject(value);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out System.Runtime.InteropServices.ComTypes.FILETIME idleTime,
        out System.Runtime.InteropServices.ComTypes.FILETIME kernelTime,
        out System.Runtime.InteropServices.ComTypes.FILETIME userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLogicalProcessorInformationEx(int relationshipType, IntPtr buffer, out uint returnedLength);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }
}
