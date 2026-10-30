# Como criar (padrões do projeto)

Receitas curtas para adicionar coisas sem fugir do padrão. Sempre: um commit por mudança, `dotnet test` nos dois
projetos de teste antes do commit e o grafo atualizado (`.\tools\graph\graph.ps1 update`) quando mudar estrutura.

## Uma página nova

1. Crie `src/LznTweaker.App/Features/<Nome>/` com `<Nome>View.xaml` (+ `.xaml.cs`) e `<Nome>ViewModel.cs`.
2. ViewModel: `partial class` herdando `ObservableObject`, com `[ObservableProperty]` e `[RelayCommand]`
   (CommunityToolkit.Mvvm). Serviços vêm pelo construtor.
3. Code-behind só faz `InitializeComponent()`, `DataContext = viewModel` e, se preciso, chama um método de
   primeira leitura no `Loaded`. Nada de `Click=`, `MessageBox` ou regra de negócio na View.
4. Confirmações e mensagens: `IUserDialogs`. Avisos rápidos: `INotificationService`.
5. Cabeçalho: título com `Lzn.Text.Title` e `<controls:CategoryInfo Guide="{x:Static shell:CategoryGuides.<Nome>}" />`;
   escreva o texto em `Shell/CategoryGuides.cs` (o que é, o que permite fazer, benefício, cuidados).
6. Registre View e ViewModel em `Composition/ServiceRegistration.cs` e o item da sidebar em `Shell/ShellPages.cs`.
7. Rode `dotnet test .\tests\LznTweaker.App.Smoke`: ele abre todas as páginas e falha com erro de XAML ou binding.

## Um ajuste novo do Windows (tweak)

1. Adicione um `TweakDefinition` em `src/LznTweaker.Core/Tweaks/TweakCatalog.cs` com Id constante, nome,
   descrição simples (sem promessa de FPS), motivo técnico, risco, compatibilidade e a fonte.
2. Prefira `RegistryChanges` / `HklmRegistryChanges` / `ServiceChanges`: o motor grava o valor anterior, verifica e
   desfaz sozinho. Script (`InvokeScript`) só quando não houver alternativa, sempre com `VerifyScript`
   e, se possível, `UndoScript`.
3. Coloque o Id em uma aba de `Features/TweakCards/TweakCardPages.cs` (e um ícone em `Glyphs`). Páginas com
   `ListLayout: true` (Geral, Network e Fixes) mostram lista compacta com o detalhe fixo à direita; as demais, a grade de cards.
   O interruptor da lista só marca o ajuste como pendente: aplicar continua sendo pelo lote.
4. Testes: `CatalogAndRollbackTests` valida o catálogo; ajustes de registro podem ser testados no
   `BatchEngineTests` com a chave isolada do HKCU.
5. Nunca aplique nada fora do lote: o lote cria o ponto de restauração obrigatório e registra no histórico.

## Um perfil de jogo

- Dados do jogo (App ID, executável): `Core/GameOptimization/SupportedGames.cs`.
- Orientações manuais: `GuidanceCatalog.cs`. Recomendações de vídeo/driver: `Core/Gpu/GpuCatalog.cs`
  (e `Cs2Recommendations.cs` para o CS2).
- Recomendação é sempre "manual" até o app conseguir aplicar **e** verificar. Nunca edite arquivos do jogo nem
  nada ligado a anticheat.

## Visual

- Cores, tamanhos e estilos vêm de `Themes/` (`Tokens.xaml` → `Controls.xaml` → `Implicit.xaml`). Nada de
  hexadecimal nas Views ou nos ViewModels; em C#, use `Infrastructure/ThemeBrushes`.
- Estados: "Aplicado/Restaurado" só para o que o app mudou e verificou; recomendação é "Requer configuração manual".
- Ícones: Segoe Fluent Icons (`Lzn.Icon`). Marca: `Assets/Brand` (pack URI com `;component/`).

## Erros e log

- Falhas inesperadas vão para `%LOCALAPPDATA%\LznTweaker\logs` pelo `AppLog`; o handler global mostra a mensagem.
- Em serviços, capture exceções específicas, devolva um resultado claro e use `AppLog.Warn` para o que ajuda a
  diagnosticar. Não grave senhas nem conteúdo de arquivos no log.
