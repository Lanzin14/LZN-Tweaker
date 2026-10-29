param([Parameter(Mandatory=$true)][string]$PayloadPath, [Parameter(Mandatory=$true)][string]$ResultPath, [Parameter(Mandatory=$true)][string]$ProgressPath)
# Ativa a Proteção do Sistema no volume do Windows. A confirmação é feita depois pelo app (RestorePointGuard).
$ErrorActionPreference = 'Stop'
try {
    Enable-ComputerRestore -Drive "$env:SystemDrive\"
    $result = [ordered]@{ Outcome = 'Executado'; Message = 'Enable-ComputerRestore concluído.' }
} catch {
    $result = [ordered]@{ Outcome = 'Falha'; Message = $_.Exception.Message }
}
[System.IO.File]::WriteAllText($ResultPath, (ConvertTo-Json -InputObject $result -Compress), [System.Text.UTF8Encoding]::new($false))
