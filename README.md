# LznTweaker

Otimizador para Windows 10/11 (WPF, .NET 10) com execução coordenada, verificação de resultados e reversão.

## Estrutura

| Pasta | Conteúdo |
|---|---|
| `src/LznTweaker.App` | Interface WPF: `MainWindow`, Views, ViewModels e `Composition/ServiceRegistration.cs` (injeção de dependência). |
| `src/LznTweaker.Core` | Motor e serviços, sem dependência de WPF. |
| `src/LznTweaker.Core/Execution` | `ScriptRunnerService` (um processo PowerShell por lote, timeout, progresso), `ElevatedWorkspace` (pasta de operações restrita a Administradores/SYSTEM) e os scripts embutidos em `Scripts/`. |
| `src/LznTweaker.Core/Changes` | `RestorePointGuard`: o ponto de restauração é obrigatório antes de qualquer lote. |
| `src/LznTweaker.Core/Tweaks` | Catálogo, presets, executor do lote e histórico. |
| `tests/LznTweaker.Core.Tests` | Testes xUnit que executam o script real do motor em uma chave isolada do HKCU. |
| `docs/` | Auditoria técnica e as etapas originais do projeto. |

## Desenvolvimento

O app exige administrador (`app.manifest`). Para executar, abra o terminal **como administrador**:

```powershell
dotnet run --project src/LznTweaker.App
```

Os testes devem rodar **sem** elevação; elevados, os testes que alterariam o sistema real são pulados:

```powershell
dotnet test
```
