param([Parameter(Mandatory=$true)][string]$PayloadPath, [Parameter(Mandatory=$true)][string]$ResultPath, [Parameter(Mandatory=$true)][string]$ProgressPath)
# Inventário somente leitura dos pacotes Appx da conta que executa o app.
$ErrorActionPreference = 'Stop'

$currentSid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
# Dono do explorer.exe: a conta que está usando a área de trabalho (pode diferir da conta elevada).
$interactiveSid = $null
try {
    $explorer = Get-CimInstance Win32_Process -Filter "Name='explorer.exe'" | Select-Object -First 1
    if ($explorer) { $interactiveSid = [string](Invoke-CimMethod -InputObject $explorer -MethodName GetOwnerSid).Sid }
} catch { }

# Pacotes provisionados (instalados para novas contas): só legíveis com administrador.
$provisioned = $null
try { $provisioned = @(Get-AppxProvisionedPackage -Online -ErrorAction Stop | ForEach-Object { [string]$_.DisplayName }) } catch { }

$packages = @(Get-AppxPackage | ForEach-Object {
    [pscustomobject]@{
        Name = [string]$_.Name
        PackageFullName = [string]$_.PackageFullName
        PackageFamilyName = [string]$_.PackageFamilyName
        Publisher = [string]$_.Publisher
        Version = [string]$_.Version
        InstallLocation = [string]$_.InstallLocation
        SignatureKind = [string]$_.SignatureKind
        IsFramework = [bool]$_.IsFramework
        IsResourcePackage = [bool]$_.IsResourcePackage
        NonRemovable = [bool]$_.NonRemovable
        IsProvisioned = if ($null -eq $provisioned) { $null } else { $provisioned -contains [string]$_.Name }
    }
})

$result = [ordered]@{
    CurrentUserSid = $currentSid
    InteractiveUserSid = $interactiveSid
    ProvisionedKnown = ($null -ne $provisioned)
    Packages = $packages
}
[System.IO.File]::WriteAllText($ResultPath, (ConvertTo-Json -InputObject $result -Depth 5 -Compress), [System.Text.UTF8Encoding]::new($false))
