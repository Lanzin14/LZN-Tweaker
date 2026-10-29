param([Parameter(Mandatory=$true)][string]$PayloadPath, [Parameter(Mandatory=$true)][string]$ResultPath, [Parameter(Mandatory=$true)][string]$ProgressPath)
# Coleta somente leitura para o diagnóstico de jogos. Nada é alterado. Cada fato ausente vira null.
$ErrorActionPreference = 'Continue'
$payload = Get-Content -LiteralPath $PayloadPath -Raw -Encoding UTF8 | ConvertFrom-Json

function Read-Value([string]$Path, [string]$Name) {
    try { return (Get-ItemProperty -LiteralPath $Path -Name $Name -ErrorAction Stop).$Name } catch { return $null }
}

$facts = [ordered]@{}
$facts.GameModeAuto = Read-Value 'HKCU:\Software\Microsoft\GameBar' 'AutoGameModeEnabled'
$facts.GameDvrEnabled = Read-Value 'HKCU:\System\GameConfigStore' 'GameDVR_Enabled'
$facts.AppCaptureEnabled = Read-Value 'HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR' 'AppCaptureEnabled'
$facts.HwSchMode = Read-Value 'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers' 'HwSchMode'
$facts.HiberbootEnabled = Read-Value 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power' 'HiberbootEnabled'
$facts.HibernateEnabled = Read-Value 'HKLM:\SYSTEM\CurrentControlSet\Control\Power' 'HibernateEnabled'
$facts.OverlayTestMode = Read-Value 'HKLM:\SOFTWARE\Microsoft\Windows\Dwm' 'OverlayTestMode'
$facts.DirectXUserGlobalSettings = Read-Value 'HKCU:\Software\Microsoft\DirectX\UserGpuPreferences' 'DirectXUserGlobalSettings'

try { $facts.ActivePowerScheme = [string](powercfg /getactivescheme) } catch { $facts.ActivePowerScheme = $null }

# Sobreposições de temporizador no BCD (bcdedit exige administrador; sem ele, BcdReadable = false).
$facts.BcdReadable = $false
$timer = [ordered]@{}
try {
    $bcd = & bcdedit /enum '{current}' 2>$null
    if ($LASTEXITCODE -eq 0) {
        $facts.BcdReadable = $true
        foreach ($line in @($bcd)) {
            if ([string]$line -match '^\s*(useplatformclock|useplatformtick|disabledynamictick|tscsyncpolicy)\s+(\S+)') { $timer[$Matches[1]] = $Matches[2] }
        }
    }
} catch { }
$facts.BcdTimer = $timer

# TRIM (NTFS): 0 = ativo.
$facts.TrimDisabled = $null
try {
    $trim = [string](& fsutil behavior query DisableDeleteNotify 2>$null)
    if ($trim -match 'NTFS\s+DisableDeleteNotify\s*=\s*(\d)') { $facts.TrimDisabled = [int]$Matches[1] }
} catch { }

$facts.Volumes = @(foreach ($letter in @($payload.DriveLetters)) {
    try {
        $volume = Get-Volume -DriveLetter $letter -ErrorAction Stop
        $media = $null; $bus = $null
        try {
            $partition = Get-Partition -DriveLetter $letter -ErrorAction Stop
            $physical = Get-PhysicalDisk -ErrorAction Stop | Where-Object { [string]$_.DeviceId -eq [string]$partition.DiskNumber } | Select-Object -First 1
            if ($physical) { $media = [string]$physical.MediaType; $bus = [string]$physical.BusType }
        } catch { }
        [pscustomobject]@{ Letter = [string]$letter; SizeBytes = [int64]$volume.Size; FreeBytes = [int64]$volume.SizeRemaining; MediaType = $media; BusType = $bus }
    } catch { }
})

$facts.Memory = @(Get-CimInstance Win32_PhysicalMemory -ErrorAction SilentlyContinue | ForEach-Object {
    [pscustomobject]@{ DeviceLocator = [string]$_.DeviceLocator; BankLabel = [string]$_.BankLabel; CapacityBytes = [int64]$_.Capacity
        Speed = [int]$_.Speed; ConfiguredClockSpeed = [int]$_.ConfiguredClockSpeed; Manufacturer = ([string]$_.Manufacturer).Trim(); PartNumber = ([string]$_.PartNumber).Trim() }
})
$facts.MemorySlots = $null
try { $facts.MemorySlots = [int](Get-CimInstance Win32_PhysicalMemoryArray -ErrorAction Stop | Measure-Object MemoryDevices -Sum).Sum } catch { }

$bios = Get-CimInstance Win32_BIOS -ErrorAction SilentlyContinue | Select-Object -First 1
$board = Get-CimInstance Win32_BaseBoard -ErrorAction SilentlyContinue | Select-Object -First 1
$facts.BiosVersion = if ($bios) { [string]$bios.SMBIOSBIOSVersion } else { $null }
$facts.BiosDate = if ($bios -and $bios.ReleaseDate) { ([datetime]$bios.ReleaseDate).ToString('yyyy-MM-dd') } else { $null }
$facts.Board = if ($board) { ("{0} {1}" -f $board.Manufacturer, $board.Product).Trim() } else { $null }
$facts.Gpus = @(Get-CimInstance Win32_VideoController -ErrorAction SilentlyContinue | ForEach-Object { [string]$_.Name })
$facts.CpuName = [string](Get-CimInstance Win32_Processor -ErrorAction SilentlyContinue | Select-Object -First 1).Name
$facts.IsLaptop = [bool](@(Get-CimInstance Win32_Battery -ErrorAction SilentlyContinue).Count -gt 0)

$layersUser = 'HKCU:\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers'
$layersMachine = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers'
$facts.Executables = @(foreach ($exe in @($payload.Executables)) {
    [pscustomobject]@{
        Path = [string]$exe
        GpuPreference = Read-Value 'HKCU:\Software\Microsoft\DirectX\UserGpuPreferences' ([string]$exe)
        LayersUser = Read-Value $layersUser ([string]$exe)
        LayersMachine = Read-Value $layersMachine ([string]$exe)
    }
})

[System.IO.File]::WriteAllText($ResultPath, (ConvertTo-Json -InputObject $facts -Depth 5 -Compress), [System.Text.UTF8Encoding]::new($false))
