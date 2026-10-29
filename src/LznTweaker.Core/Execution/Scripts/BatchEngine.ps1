param([Parameter(Mandatory=$true)][string]$PayloadPath, [Parameter(Mandatory=$true)][string]$ResultPath, [Parameter(Mandatory=$true)][string]$ProgressPath)
$ErrorActionPreference = 'Stop'
$payload = Get-Content -LiteralPath $PayloadPath -Raw -Encoding UTF8 | ConvertFrom-Json
$results = New-Object System.Collections.Generic.List[object]
# DWord, QWord, String e ExpandString podem ser restaurados com fidelidade; outros tipos são recusados.
$SupportedOldKinds = @(4, 11, 1, 2)
$StartTypeCodes = @{ 'Automatic' = 2; 'Manual' = 3; 'Disabled' = 4 }

function Test-SameValue($Left, $Right) {
    return [string]::Equals([string]$Left, [string]$Right, [System.StringComparison]::Ordinal)
}

# @($lista) falha no Windows PowerShell 5.1 com List[object] ("tipos de argumento não correspondem").
function ConvertTo-Array($Value) {
    $items = New-Object System.Collections.Generic.List[object]
    if ($null -ne $Value) { foreach ($item in $Value) { $items.Add($item) } }
    return ,$items.ToArray()
}

function ConvertTo-RegistryValue($Value, [int]$Kind) {
    if ($Kind -eq 4) { return [int]$Value }
    if ($Kind -eq 11) { return [int64]$Value }
    return [string]$Value
}

function Open-HiveKey([string]$Hive, [string]$SubKey, [bool]$Writable) {
    $path = $SubKey.TrimStart('\')
    if ($Hive -eq 'HKLM') { $root = [Microsoft.Win32.Registry]::LocalMachine }
    elseif ($Hive -eq 'HKCU') { $root = [Microsoft.Win32.Registry]::Users; $path = ([string]$payload.UserSid) + '\' + $path }
    else { throw "Hive não permitida: $Hive" }
    $key = $root.OpenSubKey($path, $Writable)
    if ($null -eq $key -and $Writable) { $key = $root.CreateSubKey($path) }
    return $key
}

function Get-EntryHive($Entry) {
    if ($Entry.PSObject.Properties['Hive'] -and $Entry.Hive) { return [string]$Entry.Hive }
    return 'HKCU'
}

function New-Tracker {
    return [pscustomobject]@{
        Registry = New-Object System.Collections.Generic.List[object]
        Services = New-Object System.Collections.Generic.List[object]
        Tasks = New-Object System.Collections.Generic.List[object]
    }
}

function Set-TrackedRegistryValue([string]$Hive, $Change, $Tracker) {
    $key = Open-HiveKey $Hive ([string]$Change.SubKey) $true
    if ($null -eq $key) { throw "Não foi possível abrir $Hive\$($Change.SubKey)." }
    try {
        $name = [string]$Change.Name
        $kind = [int]$Change.Kind
        $exists = @($key.GetValueNames()) -contains $name
        $oldValue = $null
        $oldKind = $kind
        if ($exists) {
            $oldKind = [int]$key.GetValueKind($name)
            if ($SupportedOldKinds -notcontains $oldKind) { throw "O valor $name tem um tipo anterior ($oldKind) sem reversão segura; ele não foi alterado." }
            $oldValue = $key.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
        }
        $newValue = ConvertTo-RegistryValue $Change.Value $kind
        $key.SetValue($name, $newValue, [Microsoft.Win32.RegistryValueKind]$kind)
        $Tracker.Registry.Add([pscustomobject][ordered]@{
            Hive = $Hive; SubKey = [string]$Change.SubKey; Name = $name; OldExists = [bool]$exists
            OldValue = $oldValue; OldKind = $oldKind; AppliedValue = $newValue; AppliedKind = $kind
        })
        $readBack = $key.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
        if ([int]$key.GetValueKind($name) -ne $kind -or -not (Test-SameValue $readBack $newValue)) {
            throw "A verificação de $Hive\$($Change.SubKey)\$name falhou após a gravação."
        }
    } finally { $key.Dispose() }
}

function Assert-RegistryEntryUnchanged($Entry) {
    $hive = Get-EntryHive $Entry
    $key = Open-HiveKey $hive ([string]$Entry.SubKey) $false
    if ($null -eq $key) { throw "A chave $hive\$($Entry.SubKey) não existe mais; nada foi revertido." }
    try {
        $name = [string]$Entry.Name
        $exists = @($key.GetValueNames()) -contains $name
        if (-not $exists -or [int]$key.GetValueKind($name) -ne [int]$Entry.AppliedKind -or
            -not (Test-SameValue $key.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) $Entry.AppliedValue)) {
            throw "O valor $name mudou desde a aplicação; ele foi preservado."
        }
    } finally { $key.Dispose() }
}

function Restore-RegistryEntry($Entry) {
    Assert-RegistryEntryUnchanged $Entry
    $hive = Get-EntryHive $Entry
    $key = Open-HiveKey $hive ([string]$Entry.SubKey) $true
    try {
        $name = [string]$Entry.Name
        if ([bool]$Entry.OldExists) {
            $oldValue = ConvertTo-RegistryValue $Entry.OldValue ([int]$Entry.OldKind)
            $key.SetValue($name, $oldValue, [Microsoft.Win32.RegistryValueKind][int]$Entry.OldKind)
            $restored = $key.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
            if (-not (Test-SameValue $restored $oldValue)) { throw "A verificação de reversão de $name falhou." }
        } else {
            $key.DeleteValue($name, $false)
            if (@($key.GetValueNames()) -contains $name) { throw "O valor $name permaneceu após a reversão." }
        }
    } finally { $key.Dispose() }
}

function Get-ServiceStartInfo([string]$Name) {
    $key = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Services\$Name", $false)
    if ($null -eq $key) { return $null }
    try { return [pscustomobject]@{ Start = [int]$key.GetValue('Start', -1); Delayed = [int]$key.GetValue('DelayedAutostart', 0) } }
    finally { $key.Dispose() }
}

function Set-TrackedService($Change, $Tracker, $Notes) {
    $name = [string]$Change.Name
    $target = [string]$Change.TargetStartupType
    if (-not $StartTypeCodes.ContainsKey($target)) { throw "Tipo de inicialização não permitido: $target" }
    $before = Get-ServiceStartInfo $name
    if ($null -eq $before -or $null -eq (Get-Service -Name $name -ErrorAction SilentlyContinue)) { $Notes.Add("serviço $name ausente"); return }
    if ($before.Start -eq $StartTypeCodes[$target]) { $Notes.Add("serviço $name já estava $target"); return }
    Set-Service -Name $name -StartupType $target -ErrorAction Stop
    $Tracker.Services.Add([pscustomobject][ordered]@{ Name = $name; OldStart = $before.Start; OldDelayed = $before.Delayed; AppliedStart = $StartTypeCodes[$target] })
    $after = Get-ServiceStartInfo $name
    if ($null -eq $after -or $after.Start -ne $StartTypeCodes[$target]) { throw "O serviço $name não ficou como $target após a alteração." }
}

function Assert-ServiceUnchanged($Entry) {
    $current = Get-ServiceStartInfo ([string]$Entry.Name)
    if ($null -eq $current) { throw "O serviço $($Entry.Name) não existe mais." }
    if ($current.Start -ne [int]$Entry.AppliedStart) { throw "O serviço $($Entry.Name) mudou desde a aplicação; ele foi preservado." }
}

function Restore-TrackedService($Entry) {
    Assert-ServiceUnchanged $Entry
    $name = [string]$Entry.Name
    $mode = switch ([int]$Entry.OldStart) {
        0 { 'boot' } 1 { 'system' } 3 { 'demand' } 4 { 'disabled' }
        2 { if ([int]$Entry.OldDelayed -eq 1) { 'delayed-auto' } else { 'auto' } }
        default { throw "Tipo de inicialização anterior desconhecido para $name." }
    }
    $output = & sc.exe config $name start= $mode
    if ($LASTEXITCODE -ne 0) { throw "sc.exe não restaurou $($name): $output" }
    $after = Get-ServiceStartInfo $name
    if ($null -eq $after -or $after.Start -ne [int]$Entry.OldStart) { throw "A verificação da reversão do serviço $name falhou." }
}

function Split-TaskPath([string]$FullPath) {
    $index = $FullPath.LastIndexOf('\')
    return @($FullPath.Substring(0, $index + 1), $FullPath.Substring($index + 1))
}

function Disable-TrackedTask([string]$FullPath, $Tracker, $Notes) {
    $folder, $leaf = Split-TaskPath $FullPath
    $task = Get-ScheduledTask -TaskPath $folder -TaskName $leaf -ErrorAction SilentlyContinue
    if ($null -eq $task) { $Notes.Add("tarefa $leaf ausente"); return }
    if ([string]$task.State -eq 'Disabled') { $Notes.Add("tarefa $leaf já desativada"); return }
    Disable-ScheduledTask -TaskPath $folder -TaskName $leaf -ErrorAction Stop | Out-Null
    $Tracker.Tasks.Add([pscustomobject]@{ Path = $FullPath })
    $after = Get-ScheduledTask -TaskPath $folder -TaskName $leaf -ErrorAction Stop
    if ([string]$after.State -ne 'Disabled') { throw "A tarefa $leaf não ficou desativada." }
}

function Assert-TaskUnchanged($Entry) {
    $folder, $leaf = Split-TaskPath ([string]$Entry.Path)
    $task = Get-ScheduledTask -TaskPath $folder -TaskName $leaf -ErrorAction SilentlyContinue
    if ($null -eq $task) { throw "A tarefa $leaf não existe mais." }
    if ([string]$task.State -ne 'Disabled') { throw "A tarefa $leaf mudou desde a aplicação; ela foi preservada." }
}

function Restore-TrackedTask($Entry) {
    Assert-TaskUnchanged $Entry
    $folder, $leaf = Split-TaskPath ([string]$Entry.Path)
    Enable-ScheduledTask -TaskPath $folder -TaskName $leaf -ErrorAction Stop | Out-Null
    $after = Get-ScheduledTask -TaskPath $folder -TaskName $leaf -ErrorAction Stop
    if ([string]$after.State -eq 'Disabled') { throw "A tarefa $leaf continuou desativada após a reversão." }
}

function Undo-Tracker($Tracker) {
    $errors = New-Object System.Collections.Generic.List[string]
    $tasks = ConvertTo-Array $Tracker.Tasks
    $services = ConvertTo-Array $Tracker.Services
    $registry = ConvertTo-Array $Tracker.Registry
    for ($i = $tasks.Count - 1; $i -ge 0; $i--) { try { Restore-TrackedTask $tasks[$i] } catch { $errors.Add($_.Exception.Message) } }
    for ($i = $services.Count - 1; $i -ge 0; $i--) { try { Restore-TrackedService $services[$i] } catch { $errors.Add($_.Exception.Message) } }
    for ($i = $registry.Count - 1; $i -ge 0; $i--) { try { Restore-RegistryEntry $registry[$i] } catch { $errors.Add($_.Exception.Message) } }
    return ,$errors
}

function ConvertFrom-RollbackData([string]$Text) {
    $parsed = ConvertFrom-Json -InputObject $Text
    if ($parsed -is [array]) {
        # Formato antigo: lista de valores HKCU.
        return [pscustomobject]@{ Registry = @($parsed); Services = @(); Tasks = @() }
    }
    return [pscustomobject]@{
        Registry = (ConvertTo-Array $parsed.Registry)
        Services = (ConvertTo-Array $parsed.Services)
        Tasks = (ConvertTo-Array $parsed.Tasks)
    }
}

function Publish-Progress {
    try {
        $document = [ordered]@{ Results = @($results.ToArray()) }
        [System.IO.File]::WriteAllText($ProgressPath, (ConvertTo-Json -InputObject $document -Compress -Depth 10), [System.Text.UTF8Encoding]::new($false))
    } catch { }
}

$RestorePointOkOutcomes = @('Sucesso verificado', 'Ponto recente reutilizado')
$blockedReason = $null

foreach ($operation in @($payload.Operations)) {
    $outcome = 'Falha ou não verificado'
    $message = ''
    $previousState = 'Não disponível'
    $rollbackData = $null
    $rollbackOutcome = 'Não disponível'
    if ($blockedReason -and [string]$operation.Kind -ne 'RestorePoint') {
        # Sem ponto de restauração, nada mais do lote é executado.
        $results.Add([pscustomobject]@{
            TweakId = [string]$operation.TweakId; Outcome = 'Bloqueado'; Message = $blockedReason
            PreviousState = 'Não alterado'; RollbackData = $null; RollbackOutcome = 'Nada foi alterado.'
        })
        Publish-Progress
        continue
    }
    try {
        switch ([string]$operation.Kind) {
            'RestorePoint' {
                $policy = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Policies\Microsoft\Windows NT\SystemRestore' -Name DisableSR -ErrorAction SilentlyContinue
                if ($policy -and [int]$policy.DisableSR -eq 1) { throw 'A política do computador bloqueia a Proteção do Sistema.' }
                $before = @(Get-ComputerRestorePoint -ErrorAction Stop | Sort-Object SequenceNumber)
                $previousState = if ($before.Count -gt 0) { "Último ponto anterior: #$($before[-1].SequenceNumber)" } else { 'Nenhum ponto anterior encontrado' }
                $description = "LznTweaker $([Guid]::NewGuid().ToString('N'))"
                Checkpoint-Computer -Description $description -RestorePointType MODIFY_SETTINGS -ErrorAction Stop
                $after = @(Get-ComputerRestorePoint -ErrorAction Stop | Sort-Object SequenceNumber)
                $created = $after | Where-Object { $_.Description -eq $description } | Select-Object -First 1
                $rollbackOutcome = 'Sem reversão automática confiável'
                if ($null -ne $created) {
                    $outcome = 'Sucesso verificado'
                    $message = "Ponto encontrado após a criação (sequência #$($created.SequenceNumber))."
                } else {
                    # O Windows ignora a criação se já houver um ponto nas últimas 24 h; esse ponto continua sendo uma rede de segurança válida.
                    $latest = $after | Select-Object -Last 1
                    $latestTime = if ($latest) { [System.Management.ManagementDateTimeConverter]::ToDateTime([string]$latest.CreationTime) } else { $null }
                    if ($null -eq $latestTime -or ((Get-Date) - $latestTime) -gt [TimeSpan]::FromHours(24)) {
                        throw 'O ponto não apareceu na consulta posterior e não há ponto das últimas 24 horas; a criação não foi confirmada.'
                    }
                    $outcome = 'Ponto recente reutilizado'
                    $message = "O Windows não criou um novo ponto (limite de um a cada 24 h); o ponto #$($latest.SequenceNumber) de $($latestTime.ToString('g')) (`"$($latest.Description)`") é a referência deste lote."
                }
            }
            'Tracked' {
                $tracker = New-Tracker
                $notes = New-Object System.Collections.Generic.List[string]
                try {
                    foreach ($change in (ConvertTo-Array $operation.RegistryChanges)) { Set-TrackedRegistryValue 'HKCU' $change $tracker }
                    foreach ($change in (ConvertTo-Array $operation.HklmRegistryChanges)) { Set-TrackedRegistryValue 'HKLM' $change $tracker }
                    foreach ($svcChange in (ConvertTo-Array $operation.ServiceChanges)) { Set-TrackedService $svcChange $tracker $notes }
                    foreach ($taskPath in (ConvertTo-Array $operation.ScheduledTasks)) { Disable-TrackedTask ([string]$taskPath) $tracker $notes }
                } catch {
                    $failure = $_.Exception.Message
                    $undoErrors = Undo-Tracker $tracker
                    $rollbackOutcome = if ($undoErrors.Count -eq 0) { 'Alterações parciais revertidas após erro.' } else { "A reversão parcial também falhou: $($undoErrors -join ' | ')" }
                    throw $failure
                }
                $changed = $tracker.Registry.Count + $tracker.Services.Count + $tracker.Tasks.Count
                $message = "Verificados: $($tracker.Registry.Count) valor(es) do Registro, $($tracker.Services.Count) serviço(s), $($tracker.Tasks.Count) tarefa(s) agendada(s)."
                if ($notes.Count -gt 0) { $message += ' Observações: ' + ($notes -join '; ') + '.' }
                $previousState = "$changed alteração(ões) com estado anterior capturado"
                if ($changed -eq 0) {
                    $outcome = 'Nada a alterar'
                    $rollbackOutcome = 'Nenhuma alteração para reverter.'
                } else {
                    $outcome = 'Sucesso verificado'
                    $snapshot = [pscustomobject][ordered]@{
                        Registry = @($tracker.Registry.ToArray()); Services = @($tracker.Services.ToArray()); Tasks = @($tracker.Tasks.ToArray())
                    }
                    $rollbackData = ConvertTo-Json -InputObject $snapshot -Compress -Depth 6
                    $rollbackOutcome = 'Reversão registrada; só será aplicada se os valores ainda forem os deste app.'
                }
            }
            'DeleteTempFiles' {
                $root = [System.IO.Path]::GetFullPath([string]$operation.TempRoot).TrimEnd('\')
                $actualRoot = [System.IO.Path]::GetFullPath($env:TEMP).TrimEnd('\')
                if (-not [string]::Equals($root, $actualRoot, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'A pasta TEMP mudou desde a prévia; nenhum arquivo foi removido.' }
                $prefix = $root + '\'
                $removed = 0; $skipped = 0; $bytesRemoved = [int64]0
                foreach ($file in @($operation.Files)) {
                    try {
                        $path = [System.IO.Path]::GetFullPath([string]$file.FullPath)
                        if (-not $path.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) { $skipped++; continue }
                        if (-not [System.IO.File]::Exists($path)) { $skipped++; continue }
                        $info = [System.IO.FileInfo]::new($path)
                        if (($info.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 -or
                            $info.Length -ne [int64]$file.Length -or $info.LastWriteTimeUtc.Ticks -ne [int64]$file.LastWriteTimeUtcTicks) { $skipped++; continue }
                        $probe = [System.IO.File]::Open($path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::None)
                        $probe.Dispose()
                        [System.IO.File]::Delete($path)
                        if ([System.IO.File]::Exists($path)) { $skipped++; continue }
                        $removed++; $bytesRemoved += [int64]$file.Length
                    } catch { $skipped++ }
                }
                $fileCount = @($operation.Files).Count
                $previousState = "$fileCount arquivo(s) selecionado(s) na prévia"
                $outcome = if ($removed -gt 0) { 'Sucesso verificado' } else { 'Nenhum arquivo removido' }
                $message = "Removidos: $removed; ignorados, ausentes, alterados ou em uso: $skipped; espaço removido: $bytesRemoved bytes."
                $rollbackOutcome = 'Exclusão de temporários não possui reversão confiável.'
            }
            'CustomScript' {
                $previousState = 'Estado anterior não capturado (script de sistema)'
                $rollbackOutcome = 'Sem reversão automática pelo app.'
                $global:LASTEXITCODE = 0
                & ([scriptblock]::Create([string]$operation.InvokeScript)) | Out-Null
                if ($LASTEXITCODE -ne 0) { throw "O comando terminou com código $LASTEXITCODE." }
                if ($operation.VerifyScript) {
                    $verified = @(& ([scriptblock]::Create([string]$operation.VerifyScript))) | Select-Object -Last 1
                    if ($verified -ne $true) { throw 'O comando terminou, mas a verificação posterior não confirmou o novo estado.' }
                    $outcome = 'Sucesso verificado'
                    $message = 'Comando executado e novo estado confirmado pela verificação posterior.'
                } else {
                    $outcome = 'Executado sem verificação'
                    $message = 'Comando executado sem erro; o app não consegue confirmar o efeito real desta operação.'
                }
            }
            'Undo' {
                $tracker = ConvertFrom-RollbackData ([string]$operation.RollbackData)
                foreach ($entry in $tracker.Registry) { Assert-RegistryEntryUnchanged $entry }
                foreach ($entry in $tracker.Services) { Assert-ServiceUnchanged $entry }
                foreach ($entry in $tracker.Tasks) { Assert-TaskUnchanged $entry }
                $undoErrors = Undo-Tracker $tracker
                if ($undoErrors.Count -gt 0) { throw "Reversão incompleta: $($undoErrors -join ' | ')" }
                $total = $tracker.Registry.Count + $tracker.Services.Count + $tracker.Tasks.Count
                $previousState = "$total item(ns) verificado(s) antes da reversão"
                $outcome = 'Reversão verificada'
                $message = "Restaurados: $($tracker.Registry.Count) valor(es) do Registro, $($tracker.Services.Count) serviço(s), $($tracker.Tasks.Count) tarefa(s)."
                $rollbackOutcome = 'Estado anterior restaurado.'
            }
            'RemoveAppx' {
                $name = [string]$operation.PackageName
                $installed = @(Get-AppxPackage -Name $name)
                if ($installed.Count -eq 0) {
                    $outcome = 'Nada a alterar'
                    $message = 'O aplicativo já não está instalado nesta conta.'
                    $rollbackOutcome = 'Nada para restaurar.'
                    break
                }
                foreach ($pkg in $installed) {
                    # Defesa em profundidade: mesmo que a interface envie, pacotes do sistema/frameworks nunca são removidos.
                    if ($pkg.IsFramework -or $pkg.NonRemovable -or [string]$pkg.SignatureKind -eq 'System') { throw "O pacote $($pkg.PackageFullName) é protegido pelo Windows; nada foi removido." }
                }
                $first = $installed[0]
                $manifest = Join-Path ([string]$first.InstallLocation) 'AppxManifest.xml'
                $allUsers = [bool]$operation.AllUsers
                $provisionedRemoved = 0
                if ($allUsers) {
                    $provisioned = @(Get-AppxProvisionedPackage -Online -ErrorAction Stop | Where-Object { [string]$_.DisplayName -eq $name })
                    foreach ($pkg in $installed) { Remove-AppxPackage -Package $pkg.PackageFullName -AllUsers -ErrorAction Stop }
                    foreach ($prov in $provisioned) { Remove-AppxProvisionedPackage -Online -PackageName $prov.PackageName -ErrorAction Stop | Out-Null; $provisionedRemoved++ }
                } else {
                    foreach ($pkg in $installed) { Remove-AppxPackage -Package $pkg.PackageFullName -ErrorAction Stop }
                }
                if (@(Get-AppxPackage -Name $name).Count -gt 0) { throw 'O Windows ainda lista o aplicativo após a remoção; a remoção não foi confirmada.' }
                $filesKept = Test-Path -LiteralPath $manifest
                $previousState = "Instalado ($($first.Version))"
                $rollbackData = ConvertTo-Json -Compress -InputObject ([ordered]@{
                    Name = $name; PackageFullName = [string]$first.PackageFullName; PackageFamilyName = [string]$first.PackageFamilyName
                    InstallLocation = [string]$first.InstallLocation; ManifestPath = $manifest; AllUsers = $allUsers; FilesKeptAfterRemoval = [bool]$filesKept
                })
                $outcome = 'Sucesso verificado'
                $scope = if ($allUsers) { "todas as contas (provisionamentos removidos: $provisionedRemoved)" } else { 'a conta atual' }
                $message = "Removido de $scope e confirmado pela consulta posterior."
                $rollbackOutcome = if ($filesKept) { 'Os arquivos continuam no disco: a restauração por re-registro local deve funcionar.' } else { 'Os arquivos foram apagados pelo Windows: a restauração só é possível pela Microsoft Store.' }
            }
            'RestoreAppx' {
                $data = ConvertFrom-Json -InputObject ([string]$operation.RollbackData)
                $name = [string]$data.Name
                if (@(Get-AppxPackage -Name $name).Count -gt 0) {
                    $outcome = 'Nada a alterar'
                    $message = 'O aplicativo já está instalado.'
                    $rollbackOutcome = 'Nada a restaurar.'
                    break
                }
                $manifest = $null
                if ($data.ManifestPath -and (Test-Path -LiteralPath ([string]$data.ManifestPath))) { $manifest = [string]$data.ManifestPath }
                if (-not $manifest) {
                    # Outra conta ou o provisionamento ainda pode ter os arquivos do pacote (exige administrador).
                    try {
                        $other = Get-AppxPackage -AllUsers -Name $name -ErrorAction Stop | Where-Object { $_.InstallLocation -and (Test-Path -LiteralPath (Join-Path $_.InstallLocation 'AppxManifest.xml')) } | Select-Object -First 1
                        if ($other) { $manifest = Join-Path $other.InstallLocation 'AppxManifest.xml' }
                    } catch { }
                }
                if (-not $manifest) {
                    $outcome = 'Requer Microsoft Store'
                    $message = 'Os arquivos do aplicativo não existem mais no disco; reinstale pela página do app na Microsoft Store.'
                    $rollbackOutcome = 'Pendente: reinstalação pela Microsoft Store.'
                    break
                }
                Add-AppxPackage -DisableDevelopmentMode -Register $manifest -ErrorAction Stop
                if (@(Get-AppxPackage -Name $name).Count -eq 0) { throw 'O re-registro terminou, mas o aplicativo não aparece instalado.' }
                $previousState = 'Removido'
                $outcome = 'Reversão verificada'
                $message = 'Aplicativo re-registrado a partir do manifesto local e confirmado pela consulta posterior.'
                $rollbackOutcome = 'Aplicativo restaurado.'
            }
            default { throw "Tipo de operação não permitido: $($operation.Kind)." }
        }
    } catch {
        $message = $_.Exception.Message
    }
    if ([string]$operation.Kind -eq 'RestorePoint' -and $payload.RequireRestorePoint -and $RestorePointOkOutcomes -notcontains $outcome) {
        $blockedReason = "Bloqueado: o ponto de restauração obrigatório não foi criado ($message). Nenhuma alteração foi feita."
    }
    $results.Add([pscustomobject]@{
        TweakId = [string]$operation.TweakId; Outcome = $outcome; Message = $message
        PreviousState = $previousState; RollbackData = $rollbackData; RollbackOutcome = $rollbackOutcome
    })
    Publish-Progress
}

try {
    $document = [ordered]@{ Results = @($results.ToArray()) }
    [System.IO.File]::WriteAllText($ResultPath, (ConvertTo-Json -InputObject $document -Compress -Depth 10), [System.Text.UTF8Encoding]::new($false))
} catch { [Console]::Error.WriteLine($_.Exception.Message); exit 2 }
$okOutcomes = @('Sucesso verificado', 'Reversão verificada', 'Executado sem verificação', 'Nada a alterar', 'Ponto recente reutilizado', 'Requer Microsoft Store')
if (@($results | Where-Object { $_.Outcome -notin $okOutcomes }).Count -gt 0) { exit 1 }
exit 0
