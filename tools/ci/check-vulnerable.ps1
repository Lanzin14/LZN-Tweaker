# Procura pacotes NuGet (diretos e transitivos) com vulnerabilidades conhecidas no banco do GitHub/NuGet.
# Falha com gravidade High ou Critical; Moderate e Low viram avisos na página da execução.
# Uso: pwsh tools/ci/check-vulnerable.ps1            (o -Report serve para testar a leitura com um JSON salvo)
param([string]$Report)

if ($Report) {
    $json = Get-Content $Report -Raw
} else {
    dotnet restore LznTweaker.sln | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Output '::error title=Dependências::dotnet restore falhou.'; exit 1 }
    $json = dotnet list LznTweaker.sln package --vulnerable --include-transitive --format json | Out-String
    if ($LASTEXITCODE -ne 0) { Write-Output '::error title=Dependências::dotnet list package --vulnerable falhou.'; exit 1 }
}

$blocking = 0
$found = foreach ($project in (ConvertFrom-Json $json).projects) {
    foreach ($framework in $project.frameworks) {
        foreach ($package in @($framework.topLevelPackages) + @($framework.transitivePackages) | Where-Object { $_ }) {
            foreach ($vulnerability in $package.vulnerabilities) {
                [pscustomobject]@{
                    Project  = Split-Path $project.path -Leaf
                    Package  = "$($package.id) $($package.resolvedVersion)"
                    Severity = $vulnerability.severity
                    Advisory = $vulnerability.advisoryurl
                }
            }
        }
    }
}

foreach ($item in $found | Sort-Object Package, Advisory -Unique) {
    $level = if ($item.Severity -in 'High', 'Critical') { $blocking++; 'error' } else { 'warning' }
    Write-Output "::$level title=Vulnerabilidade $($item.Severity) em $($item.Package)::$($item.Project): $($item.Advisory)"
}

if (-not $found) { Write-Output 'Nenhum pacote com vulnerabilidade conhecida.' }
exit ([int]($blocking -gt 0))
