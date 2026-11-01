# LZN Tweaker: baixa a última versão publicada no GitHub, confere o SHA-256 e abre o app.
# Uso (PowerShell):  irm https://lanzin14.github.io/win | iex
# Nada é instalado nem alterado no Windows por este script: ele só baixa o .exe para %LOCALAPPDATA%\LznTweaker\app
# e o abre (o Windows pede permissão de administrador, como em toda abertura do app).

& {
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'   # a barra de progresso do PowerShell 5.1 deixa o download muito lento
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

    $release = 'https://github.com/Lanzin14/LZN-Tweaker/releases/latest/download'
    $folder = Join-Path $env:LOCALAPPDATA 'LznTweaker\app'
    $exe = Join-Path $folder 'LznTweaker.exe'
    $partial = "$exe.download"

    Write-Host ''
    Write-Host '  LZN Tweaker' -ForegroundColor Magenta
    Write-Host '  Baixando a versão mais recente do GitHub...'

    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    try {
        $expected = ((New-Object Net.WebClient).DownloadString("$release/LznTweaker.exe.sha256").Trim() -split '\s+')[0].ToUpperInvariant()
        if ($expected -notmatch '^[0-9A-F]{64}$') { throw 'O arquivo de verificação (SHA-256) da versão publicada é inválido.' }

        if ((Test-Path $exe) -and (Get-FileHash $exe -Algorithm SHA256).Hash -eq $expected) {
            Write-Host '  Você já tem a versão mais recente.'
        }
        else {
            Invoke-WebRequest -Uri "$release/LznTweaker.exe" -OutFile $partial -UseBasicParsing
            $actual = (Get-FileHash $partial -Algorithm SHA256).Hash
            if ($actual -ne $expected) {
                Remove-Item $partial -Force -ErrorAction SilentlyContinue
                throw "O arquivo baixado não confere com o SHA-256 publicado (esperado $expected, recebido $actual). Nada foi aberto."
            }
            Move-Item $partial $exe -Force
            Write-Host '  Download conferido (SHA-256).' -ForegroundColor Green
        }
    }
    catch {
        Write-Host "  Não foi possível baixar o LZN Tweaker: $($_.Exception.Message)" -ForegroundColor Red
        return
    }

    Write-Host '  Abrindo... aprove o pedido de administrador do Windows.'
    Start-Process -FilePath $exe
}
