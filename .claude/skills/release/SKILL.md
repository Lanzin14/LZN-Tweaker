---
name: release
description: Publica uma versão nova do LZN Tweaker no GitHub (tag vX.Y.Z → GitHub Actions → Release com .exe, SHA-256, install.ps1 e licenças) e confere o download real. Use só quando o usuário pedir para publicar uma versão.
argument-hint: "[versão, ex.: 1.0.2]"
disable-model-invocation: true
---

# Publicar uma versão

Publicar deixa o `.exe` público para qualquer pessoa e muda o que `irm https://lanzin14.github.io/win | iex` baixa.
Só siga com o pedido explícito do usuário e a versão definida (argumento ou pergunta).

## 1. Antes da tag

1. Árvore limpa (`git status --short` vazio) e branch enviada (`git status -sb` sem `ahead`).
2. A versão é maior que a última tag (`git tag --sort=-v:refname | head -3`) e nunca reaproveita uma tag já enviada:
   uma tag no GitHub não se move (exige reescrever o remoto). Se uma publicação falhar, a correção sai na versão seguinte.
3. Testes locais passam (`/lzn-verificar`, ou no mínimo os do núcleo e os de interface).
4. O workflow **Testes** do último commit enviado passou no GitHub. A API é pública:
   `curl -s "https://api.github.com/repos/Lanzin14/LZN-Tweaker/actions/runs?per_page=5"` → procure o `head_sha` do commit
   com `name == "Testes"` e `conclusion == "success"`. A máquina do GitHub é diferente da sua (idioma, registro): já
   pegou bug que passava aqui (verificação da hibernação).

## 2. Tag

```bash
git tag -a vX.Y.Z -m "LZN Tweaker X.Y.Z"
git push origin vX.Y.Z
```

A versão do app vem da tag (`-p:Version` no `.github/workflows/release.yml`); não edite `Directory.Build.props`.

## 3. Acompanhar o GitHub Actions

Rode a espera em segundo plano (não fique consultando em laço curto):

```bash
for i in $(seq 1 45); do st=$(curl -s "https://api.github.com/repos/Lanzin14/LZN-Tweaker/actions/runs?per_page=5" | python3 -c "
import sys,json
r=[d for d in json.load(sys.stdin)['workflow_runs'] if d['name']=='Release' and d['head_branch']=='vX.Y.Z']
print((r[0]['status']+' '+str(r[0]['conclusion'])+' '+str(r[0]['id'])) if r else 'waiting')"); echo "$st"; case "$st" in completed*) break;; esac; sleep 20; done
```

**Se falhar:** o log exige login, mas as anotações são públicas. `tools/ci/test-core.ps1` transforma cada teste que
falhou numa anotação:

```bash
JOB=$(curl -s "https://api.github.com/repos/Lanzin14/LZN-Tweaker/actions/runs/<RUN_ID>/jobs" | python3 -c "import sys,json;print(json.load(sys.stdin)['jobs'][0]['id'])")
curl -s "https://api.github.com/repos/Lanzin14/LZN-Tweaker/check-runs/$JOB/annotations"
```

Corrija, faça o commit, envie, espere o workflow **Testes** passar e publique a versão seguinte.

## 4. Conferir a Release

1. `curl -s https://api.github.com/repos/Lanzin14/LZN-Tweaker/releases/latest`: `tag_name` é a nova e há 4 arquivos:
   `LznTweaker.exe`, `LznTweaker.exe.sha256`, `install.ps1`, `LznTweaker-licencas.zip`.
2. Teste o download real **sem abrir o app** (PowerShell 5.1), trocando a pasta e o `Start-Process`:

```powershell
$w = New-Object Net.WebClient; $w.Encoding = [Text.Encoding]::UTF8
$script = $w.DownloadString('https://github.com/Lanzin14/LZN-Tweaker/releases/latest/download/install.ps1')
$script = $script.Replace("Join-Path `$env:LOCALAPPDATA 'LznTweaker\app'", "Join-Path `$env:TEMP 'lzn-inst-test'").Replace('Start-Process -FilePath $exe', 'Write-Host "TESTE: abriria $exe"')
Invoke-Expression $script
(Get-Item "$env:TEMP\lzn-inst-test\LznTweaker.exe").VersionInfo.ProductVersion   # deve começar com X.Y.Z
Remove-Item "$env:TEMP\lzn-inst-test" -Recurse -Force
```

## 5. Relatório

Diga ao usuário a versão, o link da Release, o resultado do teste de download e o que não foi conferido (a abertura
do app em si). Lembre que o SmartScreen avisa enquanto o `.exe` não for assinado.
