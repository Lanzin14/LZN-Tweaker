# Roda os testes do núcleo no GitHub Actions. Cada teste que falha vira uma anotação na página da execução
# (visível sem login, ao contrário do log). Uso: pwsh tools/ci/test-core.ps1
$results = Join-Path $PWD 'test-results'
dotnet test tests/LznTweaker.Core.Tests -c Release --logger trx --results-directory $results
if ($LASTEXITCODE -eq 0) { exit 0 }

foreach ($file in Get-ChildItem $results -Filter *.trx -ErrorAction SilentlyContinue) {
    $trx = [xml](Get-Content $file.FullName -Raw)
    foreach ($result in $trx.TestRun.Results.UnitTestResult | Where-Object outcome -eq 'Failed') {
        $stack = ($result.Output.ErrorInfo.StackTrace -split "`n" | Select-Object -First 3) -join "`n"
        $message = "$($result.Output.ErrorInfo.Message)`n$stack"
        Write-Output "::error title=$($result.testName)::$($message.Replace('%', '%25').Replace("`r", '').Replace("`n", '%0A'))"
    }
}
exit 1
