<#
.SYNOPSIS
    Script de automacao para compilar e gerar a versao final do LznTweaker.

.DESCRIPTION
    Compila o projeto em modo Release, embute o manifesto de Administrador (UAC),
    metadados do executavel, icone (se presente) e gera a pasta de saida limpa,
    removendo arquivos de depuracao (PDBs).
#>

param(
    [ValidateSet("Quick", "Standalone")]
    [string]$Mode = "Quick",

    [string]$Version = "1.0.0",

    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$ProjectDir = $PSScriptRoot
$ProjectFile = Join-Path $ProjectDir "LznTweaker.csproj"
$PublishDir = Join-Path $ProjectDir "publish"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "          LznTweaker - Automacao de Build Final           " -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Modo de compilacao: $Mode" -ForegroundColor White
Write-Host "Versao do produto:  $Version" -ForegroundColor White
Write-Host "Diretorio de saida: $PublishDir" -ForegroundColor White
Write-Host ""

# 1. Fechar processos anteriores se estiverem em execucao
$running = Get-Process -Name "LznTweaker" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "[!] Finalizando instancias em execucao do LznTweaker..." -ForegroundColor Yellow
    $running | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 800
}

# 2. Limpeza opcional
if ($Clean -or (Test-Path $PublishDir)) {
    Write-Host "[*] Limpando diretorio de publicacao anterior..." -ForegroundColor DarkGray
    Remove-Item -Path $PublishDir -Recurse -Force -ErrorAction SilentlyContinue
}

# 3. Verificacao do Icone
$iconFile = Join-Path $ProjectDir "app.ico"
if (Test-Path $iconFile) {
    Write-Host "[OK] Icone personalizado encontrado: app.ico" -ForegroundColor Green
} else {
    Write-Host "[i] Nenhum 'app.ico' encontrado na raiz. O executavel usara o icone padrao do Windows." -ForegroundColor DarkGray
}

# 4. Verificacao do Manifesto UAC
$manifestFile = Join-Path $ProjectDir "app.manifest"
if (Test-Path $manifestFile) {
    Write-Host "[OK] Manifesto UAC (requireAdministrator) verificado." -ForegroundColor Green
} else {
    Write-Host "[!] Aviso: app.manifest nao encontrado. O app pode nao solicitar Administrador automaticamente." -ForegroundColor Red
}

Write-Host ""
Write-Host "[*] Compilando e gerando binarios com dotnet publish..." -ForegroundColor Cyan

$commonParams = @(
    "publish", $ProjectFile,
    "-c", "Release",
    "--no-restore",
    "-p:Version=$Version",
    "-p:FileVersion=$Version.0",
    "-p:AssemblyVersion=$Version.0",
    "-p:DebugType=None",
    "-p:DebugSymbols=false"
)

if ($Mode -eq "Standalone") {
    $publishParams = $commonParams + @(
        "-r", "win-x64",
        "--self-contained", "true",
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:EnableCompressionInSingleFile=true",
        "-o", $PublishDir
    )
} else {
    $publishParams = $commonParams + @(
        "-o", $PublishDir
    )
}

& dotnet $publishParams

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "[ERRO] Falha durante a compilacao! Verifique as mensagens acima." -ForegroundColor Red
    exit $LASTEXITCODE
}

# 5. Remover arquivos PDB residuais caso existam
Get-ChildItem -Path $PublishDir -Filter "*.pdb" -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "           BUILD CONCLUIDO COM SUCESSO!                   " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Arquivos gerados em: $PublishDir" -ForegroundColor White
Get-ChildItem -Path $PublishDir | ForEach-Object {
    $sizeMB = [math]::Round($_.Length / 1MB, 2)
    $sizeKB = [math]::Round($_.Length / 1KB, 1)
    $displaySize = if ($sizeMB -gt 1) { "$sizeMB MB" } else { "$sizeKB KB" }
    Write-Host ("  -> {0,-32} ({1})" -f $_.Name, $displaySize) -ForegroundColor Yellow
}

Write-Host ""
Write-Host "[OK] Executavel pronto para uso com privilegios de Administrador automaticos!" -ForegroundColor Cyan
