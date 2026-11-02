# Hook PostToolUse (Edit/Write/MultiEdit): depois de editar uma área sensível (sensitive_paths em
# tools/graph/expectations.json), lembra as garantias que o CLAUDE.md exige. Não bloqueia nada.
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
try {
    $event = [Console]::In.ReadToEnd() | ConvertFrom-Json
    $file = [string]$event.tool_input.file_path
    if (-not $file) { exit 0 }

    $root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    $relative = $file.Replace('\', '/')
    $rootSlash = $root.Replace('\', '/').TrimEnd('/') + '/'
    if ($relative.StartsWith($rootSlash, [StringComparison]::OrdinalIgnoreCase)) { $relative = $relative.Substring($rootSlash.Length) }

    $expectations = Get-Content (Join-Path $root 'tools\graph\expectations.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $area = $expectations.sensitive_paths | Where-Object { $relative.StartsWith($_, [StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1
    if (-not $area) { exit 0 }

    $context = "Área sensível ($area): este código altera o Windows. Confira antes de seguir: backup do estado anterior, " +
        "ponto de restauração obrigatório, VerifyScript que funciona mesmo sem o valor no registro (ex.: HibernateEnabled), " +
        "desfazer e registro no histórico. Nada de prioridade Realtime, afinidade automática de CPU ou serviços desativados " +
        "de forma agressiva. Teste sem aplicar mudanças reais ao sistema."
    @{ hookSpecificOutput = @{ hookEventName = 'PostToolUse'; additionalContext = $context } } | ConvertTo-Json -Compress
}
catch {
    # Um hook com defeito não pode travar a edição; o erro aparece só no log do Claude Code.
    [Console]::Error.WriteLine("area-sensivel.ps1: $($_.Exception.Message)")
}
exit 0
