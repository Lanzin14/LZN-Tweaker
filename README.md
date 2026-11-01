# LznTweaker

Otimizador para Windows 10/11 (WPF, .NET 10) com execução coordenada, verificação de resultados e reversão.

## Como abrir

Cole no PowerShell:

```powershell
irm https://lanzin14.github.io/win | iex
```

O comando baixa o `LznTweaker.exe` da última [Release](https://github.com/Lanzin14/LZN-Tweaker/releases/latest), confere
o SHA-256 publicado junto e abre o app (o Windows pede permissão de administrador). O script é o
[`install.ps1`](install.ps1); leia antes de executar, como em qualquer comando `irm | iex`. Também dá para baixar o
`.exe` direto na página da Release.

Cada Release tem uma atestação assinada de que o `.exe` foi gerado pelo GitHub Actions a partir deste repositório. O
SHA-256 só detecta um arquivo corrompido; a atestação também detecta um `.exe` trocado na Release. Para conferir, com a
[CLI do GitHub](https://cli.github.com):

```powershell
gh attestation verify LznTweaker.exe --repo Lanzin14/LZN-Tweaker
```

## Estrutura

| Pasta | Conteúdo |
|---|---|
| `src/LznTweaker.App` | Interface WPF: `MainWindow`, Views, ViewModels e `Composition/ServiceRegistration.cs` (injeção de dependência). |
| `src/LznTweaker.Core` | Motor e serviços, sem dependência de WPF. |
| `src/LznTweaker.Core/Execution` | `ScriptRunnerService` (um processo PowerShell por lote, timeout, progresso), `ElevatedWorkspace` (pasta de operações restrita a Administradores/SYSTEM) e os scripts embutidos em `Scripts/`. |
| `src/LznTweaker.Core/Changes` | `RestorePointGuard`: o ponto de restauração é obrigatório antes de qualquer lote. |
| `src/LznTweaker.Core/Tweaks` | Catálogo, presets, executor do lote e histórico. |
| `tests/LznTweaker.Core.Tests` | Testes xUnit que executam o script real do motor em uma chave isolada do HKCU. |
| `tests/LznTweaker.App.Smoke` | Teste de fumaça da interface: abre todas as páginas e falha com erro de XAML, recurso ou binding. |
| `docs/` | Auditoria técnica, etapas originais, grafo (`GRAPHIFY.md`) e receitas de desenvolvimento (`COMO-CRIAR.md`). |

## Desenvolvimento

O app exige administrador (`app.manifest`). Para executar, abra o terminal **como administrador**:

```powershell
dotnet run --project src/LznTweaker.App
```

Os testes devem rodar **sem** elevação; elevados, os testes que alterariam o sistema real são pulados:

```powershell
dotnet test
```

## Licenças

O código do LZN Tweaker está sob a licença MIT (`LICENSE`). Partes de terceiros mantêm as próprias licenças:

- Perfis de configuração do Deadlock (`src/LznTweaker.Core/GameTuning/Profiles/Deadlock`): GPL-3.0, do projeto
  OptimizationLock; origem e versões em `FONTES.md` na mesma pasta.
- Fontes IBM Plex Sans e Space Grotesk (`src/LznTweaker.App/Assets/Fonts`): SIL Open Font License 1.1.

Cada Release inclui esses textos em `LznTweaker-licencas.zip`.
