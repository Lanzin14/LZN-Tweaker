# Hook PostToolUse (Edit/Write/MultiEdit): devolve ao Claude, na hora, as regras de interface do CLAUDE.md que o
# teste DesignSystemGuardTests só pegaria depois (~40 s): cor em hexadecimal fora de Themes/Palette.*.xaml, Click= no XAML
# e MessageBox em code-behind (diálogos vão por IUserDialogs). App.xaml.cs fica de fora: avisa falhas antes da injeção
# de dependência existir.
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
try {
    $event = [Console]::In.ReadToEnd() | ConvertFrom-Json
    $file = [string]$event.tool_input.file_path
    if (-not $file -or -not (Test-Path -LiteralPath $file)) { exit 0 }
    $name = Split-Path $file -Leaf
    $isXaml = $file.EndsWith('.xaml', [StringComparison]::OrdinalIgnoreCase)
    $isCodeBehind = $file.EndsWith('.xaml.cs', [StringComparison]::OrdinalIgnoreCase) -and $name -ne 'App.xaml.cs'
    if (-not $isXaml -and -not $isCodeBehind) { exit 0 }

    $problems = @()
    $number = 0
    foreach ($line in Get-Content -LiteralPath $file -Encoding UTF8) {
        $number++
        if ($isXaml -and -not $name.StartsWith('Palette.') -and $line -match '(?<![&\w])#(?:[0-9A-Fa-f]{8}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{3,4})\b') {
            $problems += "${name}:${number}: cor em hexadecimal; use um pincel do tema (Themes/Palette.*.xaml, gerado por tools/theme/paletas.py, ou ThemeBrushes)."
        }
        if ($isXaml -and $line -match '\sClick\s*=') {
            $problems += "${name}:${number}: Click= no XAML; use Command com um RelayCommand no ViewModel."
        }
        if ($isCodeBehind -and $line -match '\bMessageBox\s*\.') {
            $problems += "${name}:${number}: MessageBox em code-behind; use IUserDialogs."
        }
    }
    if ($problems.Count -eq 0) { exit 0 }

    $reason = "Regras de interface do CLAUDE.md:`n" + ($problems -join "`n")
    @{ decision = 'block'; reason = $reason } | ConvertTo-Json -Compress
}
catch {
    [Console]::Error.WriteLine("regras-interface.ps1: $($_.Exception.Message)")
}
exit 0
