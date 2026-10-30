# Grafo de conhecimento (graphify) no LznTweaker

O grafo é um **mapa auxiliar** para entender dependências antes de mudar o código. Não é fonte de verdade:
toda relação usada para decidir uma mudança deve ser confirmada no arquivo de origem.

## Como funciona

| Etapa | Comando | Custo | O que faz |
|---|---|---|---|
| Construção completa | `/graphify` no assistente | LLM para documentos | AST de todo o código + extração semântica dos `.md`/`.txt` (com cache por arquivo) e nomes de comunidades |
| Atualização incremental | `.\tools\graph\graph.ps1 update` | sem LLM, ~3 s | `graphify update .` (só código) e em seguida `check` |
| Documentos alterados | `/graphify --update` no assistente | LLM só para os documentos alterados | reextrai o que mudou |

- Saída em `graphify-out/` (ignorado pelo Git, local à máquina): `graph.json`, `graph.html`, `GRAPH_REPORT.md`.
- `.graphifyignore` tira do grafo o que não descreve o app (`.agents/`, `tools/graph/`).
- O `graphify update` preserva os nós dos documentos e os nomes das comunidades; quando o conjunto de
  comunidades muda, as novas recebem o nome do nó central (`graphify label` refaz com LLM).
- Quando uma reconstrução encolhe o grafo, o graphify guarda a versão anterior em `graphify-out/<data>/`.
- Relações `EXTRACTED` vêm do código (chamadas, referências, herança, `x:Class`, bindings XAML).
  `INFERRED` são deduções (ex.: View → ViewModel pelo nome, relações entre documentos) e precisam de confirmação.

## Comandos do dia a dia

```powershell
.\tools\graph\graph.ps1 check            # integridade, cobertura e atualidade (sai com 1 em erro)
.\tools\graph\graph.ps1 snapshot antes   # antes de uma mudança grande
.\tools\graph\graph.ps1 diff antes       # depois: nós e dependências que mudaram + testes que cobrem os tipos afetados
.\tools\graph\graph.ps1 risk             # tipos com maior raio de impacto e áreas sensíveis sem teste
graphify affected "TweakOperationHistory" --depth 2   # quem depende de um componente
graphify path "TweakCardsViewModel" "ScriptRunnerService"
graphify explain "RestorePointPrompt"
graphify query "como um ajuste vai do switch ao BatchEngine.ps1?"
```

### O que o `check` verifica

- Relações apontando para nós inexistentes e placeholders `Community N`.
- Todo arquivo-fonte versionado (`.cs .xaml .ps1 .csproj .sln .md .txt`) gera símbolos no grafo, exceto os
  listados em `tools/graph/expectations.json` (`known_empty_files`), que o extrator não modela.
- Todo tipo C# declarado no arquivo existe como nó. Pega extração parcial por erro do parser, como aconteceu
  com `BatchEngineTests.cs`.
- Documentos sem extração semântica.
- Grafo desatualizado em relação ao `HEAD` ou a arquivos alterados e não commitados.

## Fluxo para uma mudança

1. `graphify explain`/`affected` nos componentes envolvidos e leitura dos arquivos reais.
2. Mapear o fluxo (View → ViewModel → serviço do Core → `ScriptRunnerService`/`BatchEngine.ps1` → histórico).
3. Se tocar uma área sensível (`sensitive_paths` em `expectations.json`): conferir backup, ponto de restauração,
   `VerifyScript`, desfazer e testes. O grafo não apontar problema **não** torna a mudança segura.
4. `snapshot` → menor mudança segura → `dotnet test` → `update` → `diff` (rodar os testes listados) → `check`.
5. Atualizar `docs/` e, se mudou, `/graphify --update` para os documentos.
6. Registrar o que não pôde ser validado (ex.: execução elevada real, interface aberta).

## Limitações conhecidas

- **Usings de namespaces externos** (`System.*`, `CommunityToolkit.*`): o extrator cria ~195 relações para
  nós que não existem no projeto e elas são descartadas na construção. É esperado e não afeta o código do app.
- **Acesso estático** (`SupportedGames.All`, `EmbeddedScripts.BatchEngine`) nem sempre vira relação. Por isso o
  `risk` também procura o nome do tipo em `tests/` e informa a origem da evidência (`grafo` ou `texto`).
- **Raiz dos XAML** aparece como `UserControl`/`Window` (vários nós com o mesmo rótulo nos "god nodes"); cada uma
  está ligada à sua classe code-behind por `x:Class`. Views criadas por DI sem convenção de nome
  (`HardwareView`, `MainWindow`) não ganham a relação View → ViewModel.
- **PowerShell**: `AppxInventory.ps1` e `EnableSystemProtection.ps1` são scripts sem funções e não geram símbolos.
  `BatchEngine.ps1` e `GamingDiagnostics.ps1` geram as funções, mas não há ligação C# → PowerShell; o vínculo
  passa por `EmbeddedScripts`.
- **Parser C#**: o cast `(string?)node?[x]` derruba a análise do arquivo inteiro. Use `node?[x]?.GetValue<string>()`.
- **Comunidades** pequenas de estilos XAML (bordas, chips) são ruído visual; não indicam acoplamento.
- Relações recursivas (`.ParseBlock()` chamando a si mesmo) aparecem como laço e são legítimas.

## Achados arquiteturais (verificados no código em 29/10/2026)

Fatos confirmados nos arquivos, ordenados por impacto/risco. São recomendações, não mudanças feitas.

1. **Fluxo de ponto de restauração duplicado.** `Views/TweaksView.xaml.cs` (Advanced, legado) reimplementa com
   `MessageBox` o que `Infrastructure/RestorePointPrompt.cs` faz para as páginas novas (verificar, oferecer ativar a
   Proteção do Sistema, bloquear). Hoje é uma cópia com os mesmos textos, então qualquer correção precisa ser feita
   nos dois lugares. Correção pequena: o code-behind usar `RestorePointPrompt`.
2. **Testes ausentes em código que decide o que o usuário vê ou apaga.** `TweakStateDetector` (estado dos
   switches em General/Network/Fixes, Home, Backups e Advanced), `SystemCleanupService`, `StartupManagerService`,
   `SystemToolsService` e `TempCleanupPreviewService` não têm teste algum. O `TweakStateDetector` é o mais crítico:
   um erro nele faz o app mostrar um estado diferente do real.
3. **Histórico é o maior raio de impacto.** `TweakOperationResult`/`TweakOperationHistory` têm 14 e 10 tipos
   dependentes (executor, Appx, Deadlock, Steam, shaders, ferramentas, Backups). Qualquer mudança no formato do
   `history.json` exige compatibilidade com registros antigos (há teste para o formato legado).
4. **Página Hardware depende da biblioteca de jogos.** `HardwareViewModel` pega
   `GameLibraryViewModel.Sections.First(... Hardware)`: lança exceção se a seção deixar de existir e acopla duas páginas.
   Alternativa: registrar o `GameOptimizationPageViewModel` de hardware no DI e injetá-lo nas duas.
5. **Advanced ainda é o `TweaksViewModel` legado** (480 linhas, maior ViewModel), em paralelo ao `TweakCardsViewModel`
   (388 linhas, maior ponte entre comunidades). Os dois usam o mesmo `TweakBatchExecutor`, então não há duplicação
   da execução, mas há duplicação de seleção, presets e confirmações na interface.
6. **Desfazer não exige ponto de restauração** (`TweakBatchExecutor.UndoAsync` usa `RequireRestorePoint: false`).
   É uma decisão deliberada, porque o desfazer restaura os valores originais registrados; vale manter documentado.

`TweakCardsViewModel` com muitas conexões **não** é problema em si: é a base comum de três páginas.
