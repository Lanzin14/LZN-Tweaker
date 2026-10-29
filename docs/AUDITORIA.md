# Auditoria técnica — LznTweaker (29/10/2026)

Referência: [ChrisTitusTech/winutil](https://github.com/ChrisTitusTech/winutil) (`config/tweaks.json`, `config/preset.json`, `Invoke-WinUtilTweaks.ps1`, `Set-WinUtilRegistry.ps1`, `Invoke-WPFUltimatePerformance.ps1`).

Legenda de evidência usada em todo o documento:

- **[TESTADO]**: comprovado por teste executado nesta auditoria (seção 9).
- **[INSPECIONADO]**: verificado lendo o código linha a linha, sem execução.
- **[NÃO VERIFICADO]**: não pôde ser executado aqui. Os testes rodaram **sem elevação**, então nada em HKLM, serviços, tarefas, `powercfg` ou ponto de restauração foi alterado de verdade.

Ponto de recuperação: o Git não está instalado, então antes de qualquer mudança foi criado `LznTweaker-backup-antes-auditoria-20261029-075032.zip` na raiz do projeto (93 arquivos, incluindo os 22 `.cs`).

---

## 1. Estado geral

A base é boa. O catálogo é declarativo, há um único processo PowerShell por lote, histórico em JSON, a prévia de limpeza de TEMP é rigorosa e as páginas incompletas já aparecem como "PROTÓTIPO".

O defeito central estava no motor de execução: ele **declarava "Sucesso verificado" sem verificar** e o **Undo só funcionava para uma fração dos ajustes**, embora a interface prometesse "reversão confiável" para quase todos. Isso foi corrigido nesta auditoria.

Depois das correções, o núcleo (Tweaks: seleção, presets, execução, verificação, histórico e Undo) está coerente. Ainda precisa de um teste manual elevado antes de ir para usuários (seção 8).

## 2. Funcionalidades que realmente funcionam

| Funcionalidade | Evidência |
|---|---|
| Navegação, Dashboard e Diagnóstico (somente leitura, com *fallbacks* e "não disponível" honesto) | [INSPECIONADO] |
| Presets apenas marcam opções; nada é executado ao clicar | [INSPECIONADO] + [TESTADO] (12c) |
| "Executar" envia **somente** os itens marcados para um único lote | [INSPECIONADO] |
| Ajustes HKCU: DWORD e REG_SZ aplicados, verificados por releitura e revertidos ao valor e tipo anteriores | [TESTADO] (1, 2) |
| Undo recusa reverter se o valor foi alterado depois, sem reverter nada pela metade | [TESTADO] (3) |
| Falha no meio de um ajuste desfaz as partes já aplicadas | [TESTADO] (4, 5) |
| Limpeza de TEMP do Tweaks: só apaga arquivo idêntico ao da prévia e dentro do TEMP | [TESTADO] (10) |
| Validação local do histórico contra o catálogo (impede reverter chaves arbitrárias) | [TESTADO] (11) |
| Conflitos IPv4 x IPv6 e Ativar x Remover Ultimate são bloqueados | [TESTADO] (12f) |
| Localização dos planos Ultimate Performance em pt-BR | [TESTADO] (13a) |

## 3. Funcionalidades que pareciam funcionar, mas não funcionavam (antes da auditoria)

| # | Defeito | Evidência | Situação |
|---|---|---|---|
| 1 | **Serviços nunca eram alterados.** `Get-Service -Name [string]$x.Name` é lido como texto literal em modo de comando, e `-ErrorAction SilentlyContinue` escondia o erro. Telemetria e Homegroup mostravam "Sucesso verificado" sem tocar em nenhum serviço. | [TESTADO]: o nome recebido era `[string]@{Name=DiagTrack;…}.Name` | Corrigido |
| 2 | Tarefas agendadas: caminho sem `\` final e erros silenciados; nada era verificado. | [INSPECIONADO] | Corrigido |
| 3 | **Ajustes REG_SZ sempre falhavam** (aceleração do mouse, Sticky Keys, NumLock, Snap Window): o executor recusava qualquer valor anterior que não fosse DWORD. | [TESTADO] com o motor original do backup | Corrigido |
| 4 | **Undo inexistente para HKLM, serviços e tarefas** (Telemetria, Consumer Features, Activity, Location, IPv4/IPv6, Recall, Copilot, Verbose Logon). A validação rejeitava esses dados e o script só revertia HKCU. | [INSPECIONADO] | Corrigido |
| 5 | `UndoScript` do catálogo **nunca era executado**, mas Hibernação, Teredo, Adobe e planos de energia diziam "Possui reversão confiável". | [INSPECIONADO] | Rótulos corrigidos para "Sem reversão automática" |
| 6 | Scripts (`powercfg`, `netsh`) sempre retornavam "Sucesso verificado", sem checar o código de saída. | [INSPECIONADO] | Corrigido: código de saída + `VerifyScript` |
| 7 | **Ultimate Performance quebrado em pt-BR**: procurava o nome "Ultimate Performance", nunca o encontrava e criava uma cópia nova a cada execução (há 4 "Desempenho Máximo" nesta máquina). | [TESTADO] | Corrigido |
| 8 | Presets diziam ser "baseados no WinUtil", mas não correspondiam ao `preset.json` (incluíam Hibernação, IPv4, Storage Sense e preferências visuais). | [INSPECIONADO] contra `preset.json` | Corrigido |
| 9 | O executor não passava `-ExecutionPolicy`: em um Windows cliente com a política padrão (`Restricted`), `-File` é bloqueado. Nesta máquina a política é `Unrestricted`, por isso não falhava aqui. | [INSPECIONADO] + política local lida | Corrigido (`-ExecutionPolicy Bypass` só no processo) |
| 10 | Estado "Ativado/Desativado" ambíguo ("Desativar Telemetria: Ativado"), e o estado dos serviços era ignorado. | [INSPECIONADO] | Corrigido ("Aplicado/Não aplicado", serviços incluídos) |
| 11 | Limpeza: a **Lixeira vinha marcada por padrão**; após limpar, a reanálise nunca rodava (`IsBusy`) e o resumo era sobrescrito; o HRESULT da Lixeira era ignorado. | [INSPECIONADO] | Corrigido |
| 12 | Inicialização: o filtro "Launchers de Jogos" nunca casava (o item usa "Launcher de Jogos"). Itens HKLM 32 bits e da pasta comum gravavam o estado na chave errada (sem efeito). Nomes reais eram trocados por nomes heurísticos ("edge" e "origin" casavam com qualquer texto). | [INSPECIONADO] | Corrigido |

## 4. Classificação das funcionalidades

### MANTER
| Item | Arquivos | Motivo |
|---|---|---|
| Navegação e layout | `MainWindow.xaml(.cs)`, `App.xaml` | Estável; as páginas futuras já aparecem como "PROTÓTIPO". |
| Dashboard / inventário | `Core/SystemInformationService.cs`, `SystemSnapshot.cs`, `ViewModels/DashboardViewModel.cs` | Somente leitura, com *fallbacks* por registro e API nativa; não inventa métricas. |
| Diagnóstico | `Core/SystemRecommendationEngine.cs` | Avisos explicáveis, nunca executa nada, não promete ganho. |
| Catálogo de tweaks | `Core/Tweaks/TweakCatalog.cs` | Estrutura correta: descrição, motivo técnico, risco, requisitos e fonte. |
| Lote coordenado + histórico | `TweakBatchExecutor.cs`, `TweakOperationHistory.cs` | Atende o pilar "verificável e reversível" após as correções. |
| Ponto de restauração | `TweakBatchExecutor.cs` (tipo `RestorePoint`) | Só declara sucesso se o ponto aparece na consulta posterior. |
| Prévia de TEMP | `TempCleanupPreviewService.cs`, `Views/TempCleanupPreviewWindow.*` | É a implementação de limpeza mais segura do projeto. |
| Customize Preferences | `TweakCatalog.cs` | Valores HKCU simples, agora com Undo real. |
| Inicialização | `Core/Startup/*`, `ViewModels/StartupViewModel.cs`, `Views/StartupView.*` | Útil para jogos; reversível pelo próprio toggle. |

### CORRIGIR
Corrigido nesta auditoria: todos os itens da seção 3.

Ainda pendente:
| Item | Arquivos | Risco | Ação recomendada |
|---|---|---|---|
| Script e payload elevados gravados em `%LOCALAPPDATA%\LznTweaker\Operations` | `TweakBatchExecutor.cs` | **Alto (segurança)**: um processo sem privilégio do mesmo usuário pode trocar o `.ps1`/`.json` entre a gravação e a execução elevada. | Gravar em uma pasta com ACL só para Administradores/SYSTEM (ex.: `%ProgramData%\LznTweaker\Operations` com herança removida), ou validar um hash antes de executar. |
| Falha do ponto de restauração não interrompe o lote | `TweakBatchExecutor.cs`, `TweaksView.xaml.cs` | Médio: os ajustes seguem sem rede de segurança. O Windows limita a 1 ponto a cada 24 h. | **Decisão de produto**: abortar o lote se o ponto falhar, ou perguntar. O WinUtil zera `SystemRestorePointCreationFrequency` para contornar o limite de 24 h. |
| Perfil HKCU quando o app é elevado com outra conta | `TweakBatchExecutor.cs` | Médio: um usuário padrão que digita a senha de outro admin aplica as preferências no perfil do admin. | Detectar SID do usuário interativo ≠ SID elevado e avisar. |
| Estado dos tweaks por script aparece como "Desconhecido" | `TweaksViewModel.cs` | Baixo | Reaproveitar o `VerifyScript` (ou leitura direta de `HibernateEnabled`/Teredo) na detecção. |
| Página Limpeza não confere se o arquivo mudou entre a análise e a exclusão; apaga `SoftwareDistribution\Download` com o serviço de atualização ativo | `SystemCleanupService.cs` | Médio | Reutilizar a mesma checagem de tamanho/data do Tweaks; parar/retomar `wuauserv` como o WinUtil, ou deixar a categoria desmarcada por padrão. |
| Telemetria desativa `dmwappushservice` | `TweakCatalog.cs` | Médio em PCs gerenciados (Intune/MDM) | O WinUtil não mexe nesse serviço (desativa `WerSvc`). Avaliar a remoção; o aviso já está na descrição. |
| Adobe Network Block sem Undo | `TweakCatalog.cs` | Baixo | O bloco tem marcadores; transformar o script de remoção em Undo com estado capturado. |

### SIMPLIFICAR
| Item | Arquivos | Motivo | Ação |
|---|---|---|---|
| Duas limpezas de TEMP (Tweaks "Limpar temporários" e Limpeza "Temporários do Usuário") | `TweakCatalog.cs`, `SystemCleanupService.cs` | Duplicadas e com regras de segurança diferentes. | Unificar no motor com prévia; decidir onde fica (decisão de produto). |
| Botão "Detalhes do Selecionado" | `TweaksView.xaml(.cs)` | Redundante com o tooltip e mostra só o primeiro item marcado. | Remover ou abrir o detalhe do item clicado. |
| Caminho `runas` no executor | `TweakBatchExecutor.cs` | O manifesto já exige administrador, então nunca há um segundo UAC. | Manter (inofensivo) ou remover numa limpeza futura. |
| `ITweakOperationHandler` com uma única implementação | `SystemRestorePointHandler.cs` | Abstração sem uso. | **Feito**: só a detecção ficou. |

### REMOVER TEMPORARIAMENTE (recomendação; não aplicado, porque é decisão de produto)
| Item | Motivo | Risco de remover |
|---|---|---|
| **Adobe Debloat** | Tudo com `SilentlyContinue`, sem verificação, e o "undo" força `Automatic` (não o estado anterior). Fora do foco de jogos. Não existe no WinUtil atual. | Baixo. Os nomes dos serviços são fixos, então pode voltar como ajuste rastreado. |
| **Storage Sense** em Essential | O WinUtil o coloca em "Advanced – CAUTION". | Nenhum: só mudar de categoria. |

### IMPLEMENTAR DEPOIS
- Página **Histórico** lendo `history.json` (custo baixo, grande ganho de transparência).
- "Inteligente": usar o inventário (bateria/laptop, edição Home/Pro, build) para esconder ou avisar sobre Hibernação, Ultimate Performance e políticas que a edição Home ignora.
- Undo para scripts com estado capturado (Hibernação, Teredo, plano de energia anterior).
- Itens do WinUtil ainda ausentes: WPBT, Services (manual), Delivery Optimization, Disk Cleanup.
- Páginas Jogos, Aplicativos e Privacidade: manter como protótipo, com um selo "em breve" no menu lateral.
- Benchmarks e monitoramento avançado: só depois do núcleo testado elevado.

### REMOVER DEFINITIVAMENTE (feito)
| Item | Motivo |
|---|---|
| Tweak **Homegroup** | Serviços removidos do Windows 10 1803+; nenhuma versão suportada os tem. |
| Tweak **Wi-Fi Sense** | Recurso removido no Windows 10 1803; gravava chaves sem efeito. |
| `SystemRestorePointHandler.ApplyAsync/RevertAsync` e o script embutido | Código morto: a criação real acontece no lote. |
| Ramos inalcançáveis de `CreateFeaturePage` (Diagnóstico, Inicialização, Limpeza) | Essas páginas têm views reais. |
| 11 pastas de build (`build-check*`, `build-theme*`, `build-ready`, `build-functional-check*`, `release-final`) | Só continham exe/dll/json/pdb de ontem; regeneráveis e incluídas no ZIP. |

## 5. Checagens de segurança e eficácia pedidas

| Pergunta | Resposta |
|---|---|
| Presets só selecionam? | **Sim** [INSPECIONADO]. `ApplyPreset` só altera `IsSelected`. |
| Run executa só o selecionado? | **Sim** [INSPECIONADO]. |
| Validação, logs e erros? | Agora sim: verificação por releitura, código de saída, conflitos, compatibilidade e histórico por item [TESTADO]. |
| Estado da UI = estado real? | Para Registro e serviços, sim (leitura direta). Para scripts, mostra "Desconhecido" em vez de adivinhar. |
| Reversão confiável? | Registro (HKCU/HKLM), serviços e tarefas: sim, com estado capturado e proteção contra sobrescrever mudanças posteriores [TESTADO no HKCU; NÃO VERIFICADO elevado]. Scripts: sem reversão automática, e isso agora está rotulado. |
| Operações administrativas? | O manifesto exige administrador; a única ressalva é o perfil HKCU com outra conta (seção 4). |
| A limpeza apaga dados pessoais? | Não por padrão. A Lixeira agora começa desmarcada, e Downloads e Documentos nunca entram. |
| Há função simulada apresentada como concluída? | Antes sim (itens 1–7 da seção 3). Agora, o que não é verificável aparece como "Executado sem verificação". |

## 6. MVP recomendado

1. Navegação + Dashboard + Diagnóstico (já prontos).
2. Tweaks: Essential, Advanced, Customize e Performance Plans, com os presets do WinUtil.
3. Execução em lote, verificação, histórico e Undo (prontos após esta auditoria; falta o teste elevado).
4. Ponto de restauração (pronto; falta decidir o que fazer quando falha).
5. Limpeza segura (unificar a de TEMP; resto pronto).
6. Inicialização (pronta).

Fora do MVP, mantidas visíveis como protótipo: Jogos, Aplicativos, Privacidade e Histórico.

## 7. O que foi alterado e por quê

| Arquivo | Mudança |
|---|---|
| `Core/Tweaks/TweakBatchExecutor.cs` | Novo tipo `Tracked` unifica Registro (HKCU/HKLM), serviços e tarefas, com estado anterior, releitura e reversão parcial em caso de erro. Undo completo com pré-checagem tudo-ou-nada. Formato antigo do histórico ainda aceito. Scripts com código de saída e `VerifyScript`. Conflitos bloqueados. `-ExecutionPolicy Bypass`. |
| `Core/Tweaks/TweakCatalog.cs` | `VerifyScript` (Hibernação, Teredo, Adobe Block, planos). Ultimate Performance por GUID, com nome pt-BR, sem duplicar e sem `restoredefaultschemes`. Rótulos de reversão honestos. Homegroup e Wi-Fi Sense removidos. Presets do WinUtil. |
| `Core/Tweaks/SystemRestorePointHandler.cs` | Código morto removido; só a detecção. |
| `ViewModels/TweaksViewModel.cs`, `Views/TweaksView.xaml.cs` | Estado "Aplicado/Não aplicado" com serviços; descrições dos presets; texto de confirmação sem prometer UAC nem reversão inexistente. |
| `Core/Cleanup/SystemCleanupService.cs`, `ViewModels/CleanupViewModel.cs` | Lixeira desmarcada; HRESULT verificado; reanálise real; resumo preservado. |
| `Core/Startup/*`, `ViewModels/StartupViewModel.cs`, `Views/StartupView.xaml` | Chaves corretas (Run32, pasta comum em HKLM), confirmação por releitura, nomes reais, filtro corrigido, "Impacto estimado". |
| `MainWindow.xaml.cs` | Atualização automática só com o Painel visível; ramos mortos removidos. |

## 8. Riscos e defeitos que ainda existem

1. **[NÃO VERIFICADO] Nada elevado foi executado**: HKLM, `Set-Service`/`sc.exe`, tarefas, ponto de restauração, `powercfg` e `netsh`. A lógica está testada no HKCU e por inspeção, mas precisa de um teste manual elevado, de preferência em VM ou após um ponto de restauração.
2. Script/payload elevado em pasta gravável pelo usuário (seção 4, prioridade alta).
3. `publish/LznTweaker.exe` foi gerado **antes** da auditoria e ainda contém o motor antigo. Gere de novo com `build.bat`.
4. A interface não foi aberta nesta sessão (o app exige UAC). O XAML compila, e a única mudança de XAML foi um texto de `StringFormat`.
5. `history.json` cresce sem limite.
6. As pastas `.agents/`, `skills-lock.json` e `Etapa 1 — Analisar o.txt` não fazem parte do app e não foram tocadas.

## 9. Testes executados

Não existe projeto de testes no repositório. Foi criado um *harness* temporário (fora do projeto) que referencia o `LznTweaker.csproj` real e executa o **script PowerShell embutido de verdade**, sem elevação, em uma chave isolada `HKCU\Software\LznTweakerAuditTest`, removida ao final.

| Teste | Resultado |
|---|---|
| Build Debug antes das mudanças | OK, 0 avisos |
| Prova do bug de serviços (argumento `[string]$x.Name`) | Bug confirmado |
| Prova do bug REG_SZ com o motor **original** do backup | Bug confirmado ("tipo anterior incompatível") |
| Harness: 42 verificações em 13 grupos (aplicar/verificar, Undo, proteção contra sobrescrita, reversão parcial, HKLM sem admin, REG_BINARY recusado, serviço/tarefa ausentes, histórico antigo, scripts, limpeza TEMP, validação do histórico, catálogo/presets/conflitos, sondas reais de `powercfg`/Teredo/hibernação) | **42/42 OK** na rodada final. Na primeira rodada, 3 falharam por um bug do PowerShell 5.1 (`@($lista)` com `List[object]`), que foi corrigido. |
| Build Release depois das mudanças | OK, 0 avisos, 0 erros |

## 10. Próximas tarefas (priorizadas)

1. Gerar o `publish` de novo e fazer um **teste manual elevado** (VM ou ponto de restauração): preset Standard → Executar → conferir estados → Undo de Telemetria e Mouse.
2. Proteger a pasta de operações elevadas (ACL só para administradores).
3. Decidir a política para a falha do ponto de restauração (abortar ou perguntar).
4. Instalar o Git (`git init`) e transformar o harness em um projeto de testes versionado.
5. Unificar a limpeza de TEMP.
6. Página Histórico sobre `history.json`.
7. Avisos por hardware (laptop/bateria, edição do Windows).
8. Converter o Adobe Debloat em ajuste rastreado ou ocultá-lo.
