<#
.SYNOPSIS
  Atalhos do grafo do graphify para o LznTweaker.
.EXAMPLE
  .\tools\graph\graph.ps1 update        # 'graphify update .' (só código, sem LLM) e em seguida 'check'
  .\tools\graph\graph.ps1 check         # integridade, cobertura e atualidade
  .\tools\graph\graph.ps1 snapshot antes
  .\tools\graph\graph.ps1 diff antes    # o que mudou no grafo e quais testes cobrem os tipos afetados
  .\tools\graph\graph.ps1 risk          # tipos com maior raio de impacto
#>
param(
    [Parameter(Mandatory)][ValidateSet('update', 'check', 'snapshot', 'diff', 'risk')][string]$Command,
    [Parameter(ValueFromRemainingArguments)][string[]]$Rest
)
# Sem 'Stop': no PowerShell 5.1 os avisos que o graphify escreve no stderr virariam erro fatal.
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$pythonFile = Join-Path $root 'graphify-out\.graphify_python'
if (-not (Test-Path $pythonFile)) { throw "graphify-out\.graphify_python não existe. Rode /graphify no assistente uma vez." }
$python = (Get-Content $pythonFile -Raw).Trim()
$script = Join-Path $PSScriptRoot 'lzn_graph.py'

Push-Location $root
try {
    if ($Command -eq 'update') {
        graphify update .
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        & $python $script check
    } else {
        & $python $script $Command @Rest
    }
    exit $LASTEXITCODE
} finally {
    Pop-Location
}
