# LZN Tweaker: planejamento do redesign Obsidian

> **Tecnologia, precisão e controle.** Ferramenta de otimização para gamers, com aparência profissional,
> identidade própria e uso simples.

Este documento é o plano técnico e visual do redesign. **Nada aqui foi implementado ainda**: cada fase só começa
depois de aprovada, em entregas pequenas, sem mudar funcionalidades, scripts, configurações do Windows ou o fluxo
de segurança (lote, ponto de restauração, verificação, desfazer, histórico).

Fontes da análise: os 13 prints do teste de interface (`%TEMP%\lzn-smoke`), a estrutura real de `src/LznTweaker.App`
(levantada em 30/10/2026, branch `feature/obsidian`), as skills `design:design-critique` e `ui-ux-pro-max`
(guias gerais de UX e as específicas de WPF) e as conversas anteriores sobre o projeto.

---

## 1. O que já foi feito (branch `feature/obsidian`)

| Commit | Entrega |
|---|---|
| `8f63bb3` | Paleta Obsidian aplicada aos tokens; texto escuro sobre o roxo (5,3:1). |
| `1142d6f` | Sidebar recolhível (208 ↔ 68 px) e seções Início, Otimização, Jogos e manutenção, Segurança. |
| `bc665a9` | Geral em lista compacta com detalhe fixo (painel sobreposto abaixo de 960 px). |
| `3936c97` | Menções ao WinUtil removidas da interface. |
| `38c6de9` | Network e Fixes no mesmo padrão de lista + detalhe. |

Correção de uma premissa do plano anterior: o app já pede administrador ao abrir (`app.manifest`,
`requireAdministrator`). Os erros "exige que o LznTweaker esteja em execução como administrador" vistos em Debloat e
Jogos aparecem **só no teste de interface**, que roda sem elevação. Por isso, o plano não inclui um botão "Reiniciar
como administrador" como prioridade: o que falta é transformar **qualquer** falha técnica em orientação clara
(Fase 2), e o aviso de administrador vira um caso desse componente.

---

## 2. Direção visual definitiva

**Minimalista, sofisticada, técnica.** Um software de Windows premium, com identidade gamer discreta: nada de RGB,
gradientes ou brilho em repouso.

Elementos que dão personalidade sem decoração:

- **Precisão:** traços de 1 px, alinhamento rigoroso numa grade de 4 px, números com largura fixa (contadores,
  tamanhos, ms) e valores técnicos (chaves de registro, comandos) em fonte monoespaçada.
- **Controle:** cada tela mostra estado real e o que vai mudar antes de mudar. O roxo marca onde o usuário age; o
  resto é grafite.
- **Assinatura:** o marcador vertical roxo de 3 px (já usado na sidebar e nas linhas selecionadas) vira o elemento
  de identidade de "item ativo" em todo o app.
- **Espaço negativo:** menos caixas dentro de caixas; seções separadas por espaço e por um título, não por bordas.

Referência estrutural: um dashboard com sidebar vertical, conteúdo bem distribuído, cards organizados e bastante
espaço negativo, conforme a descrição. A imagem de referência compartilhada antes não está disponível nesta sessão.
Se quiser que ela seja considerada em detalhes, envie-a de novo.

### O que foi rejeitado das recomendações automáticas (e por quê)

A skill `ui-ux-pro-max --design-system` sugeriu, para "ferramenta desktop gamer escura":

| Sugestão | Decisão | Motivo |
|---|---|---|
| Destaque verde `#22C55E` | Rejeitada | A identidade é o roxo elétrico; verde fica reservado para o estado "sucesso/ativo". |
| Padrão "coluna única, CTA grande" | Rejeitada | É um padrão de landing page; o app é uma ferramenta com muitas telas. |
| Glassmorphism, blur, "ambient light" | Rejeitada | Em WPF exige `AllowsTransparency`/efeitos que custam desempenho e contraste. Também contraria "nada brilha". |
| Fonte Inter | Rejeitada por ora | Segoe UI Variable é nativa do Windows 11, renderiza melhor no ClearType e transmite "software de Windows". Inter exigiria embutir a fonte. |
| Evitar preto puro `#000000` | Aceita | Já atendido: o fundo é `#111116`. |
| Movimento sutil (150–300 ms) | Aceita | Já é a regra; ver Fase 6. |
| Fluent `ThemeMode` (WPF .NET 9+) | Adiada | Todos os controles têm template próprio; misturar com Fluent geraria dois sistemas. Reavaliar depois. |

---

## 3. Paleta oficial e regras de uso

### 3.1 As oito cores oficiais

| Token | Cor | HEX | Uso |
|---|---|---|---|
| Background | Preto azulado | `#0A0A0E` | Fundo das páginas (escurecido em 31/10/2026, como no mockup C; antes `#111116`). |
| Surface | Grafite escuro | `#1B1B24` | Cards e painéis. |
| Elevated | Grafite elevado | `#292936` | Campos, blocos de código, hover de linhas, menus. |
| Accent | Roxo elétrico | `#8175FF` | Botão principal, seleção ativa, interruptor ligado, marcador de item ativo, logotipo. |
| Lavender | Lavanda | `#A69FFF` | Hover do botão principal, anel de foco, links, destaque secundário. |
| Text | Branco frio | `#E9E8F0` | Títulos, textos principais, valores. |
| Muted | Cinza | `#858594` | Descrições, metadados, rótulos. |
| Danger | Vermelho suave | `#E47777` | Erros, risco alto, ações destrutivas. |
| Sidebar | Grafite | `#1C1C24` | Fundo da barra lateral (escolha do usuário em 01/11/2026; antes Índigo `#4B0082`). Hover `#26262F`, ativo `#2E2A45` com traço lilás, borda `#2E2E38`, texto `#E9E8F0` e secundário `#A9A9B8` (todos AA, conferidos no teste de guarda). |

### 3.2 Cores de estado (complementares, já em uso)

Separadas da identidade, para o roxo não virar "estado":

| Estado | HEX | Uso |
|---|---|---|
| Sucesso | `#7FD1A8` | "Ativo", "Baixo risco", "Sucesso verificado". |
| Atenção | `#E5C07B` | "Pendente", "Atenção", "Exige reinício". |
| Erro | `#E47777` (= Danger) | "Falhou", "Alto risco", "Bloqueado". |
| Neutro | `#858594` (= Muted) | "Inativo", "Não verificado". |

Cada estado tem também um fundo suave e um traço (os `*Soft`/`*Stroke` atuais) para selos e avisos. **Estado nunca
é só cor**: sempre vem com ícone ou texto.

### 3.3 Tons derivados (permitidos, documentados)

Interfaces precisam de degraus intermediários (hover, traços). Eles derivam da paleta e ficam só em `Colors.xaml`:

| Derivado | HEX | De onde vem |
|---|---|---|
| SurfaceHover | `#20202A` | Entre Surface e Elevated |
| Stroke | `#292936` | = Elevated, usado como traço de 1 px |
| StrokeStrong | `#3A3A4A` | Elevated + 1 degrau (bordas de campo em foco, separadores fortes) |
| AccentSoft | `#24223F` | Accent a ~12% sobre Background (fundo de selecionado) |
| AccentStroke | `#3A3570` | Accent a ~30% (borda de selecionado) |
| AccentPressed | `#7266F5` | Accent −1 degrau (botão pressionado) |
| TextSecondary | `#B9B8C6` | Entre Text e Muted (corpo de texto longo) |

A ser **removido**: `Gray925 #0D0D11` (SurfaceSunken/Sidebar), que não pertence à paleta. Pela nova regra, a
sidebar passa a usar **Surface**.

### 3.4 Contraste (WCAG AA, conferido)

| Combinação | Contraste | Regra |
|---|---|---|
| Text sobre Background / Surface | > 13:1 | Livre |
| Muted sobre Background | 5,2:1 | Ok para texto |
| Muted sobre Surface | 4,7:1 | Ok para texto |
| Muted sobre Elevated | ~4,0:1 | **Não usar para texto**: em Elevated, usar TextSecondary |
| Texto escuro `#111116` sobre Accent | 5,3:1 | Texto de botão principal |
| Branco sobre Accent | 3,5:1 | **Proibido** para texto |
| Accent como texto sobre Background | 5,3:1 | Ok (links, rótulos ativos) |
| Danger sobre Background | 6,4:1 | Ok |

### 3.5 Regras de uso do roxo

O roxo aparece em, no máximo, **um bloco de ação por área visível**:

- **Sim:** botão principal (um por tela), interruptor ligado, caixa marcada, item de navegação ativo (fundo suave
  + marcador), linha selecionada, aba ativa (sublinhado), anel de foco (Lavender), logotipo.
- **Não:** ícones decorativos de todos os cards (hoje cada card tem um quadrado roxo suave com ícone roxo), títulos
  de grupo, selos informativos, contadores, texto de "Ativo".
- Ícones de linha passam a ser **Muted** em repouso e **Text** no item selecionado; o roxo fica para o que é
  interativo.

---

## 4. Diagnóstico técnico do sistema atual

O que existe é uma boa base (três camadas documentadas em `Themes/Theme.xaml`, hexadecimais só em `Tokens.xaml`,
nenhum `DynamicResource`). O problema é a **adoção desigual**:

| Medida (levantada no código) | Valor | Consequência |
|---|---|---|
| Estilos/brushes **legados** (`AccentBrush`, `PanelBrush`, `TextPrimaryBrush`, `PrimaryButtonStyle`, `SecondaryButtonStyle`, `AccentCheckBoxStyle`, `MinimalComboBoxStyle`…) | 10 arquivos, ~180 usos (Debloat 34, Deadlock 36, Jogos 24+9, Tweaks 21…) | Duas linguagens visuais convivendo, que é a "sensação de épocas diferentes". |
| `FontSize` literal em views | ~235 ocorrências em 16 arquivos | Escala tipográfica ignorada; 75 textos com 9–11 px. |
| `CornerRadius` literal | 10 valores diferentes (2, 3, 4, 5, 6, 8, 9, 10, 12, 14) | Cantos inconsistentes entre cards, botões e selos. |
| Estilos locais nas views | ~44 (GPU 8, MainWindow 6, Debloat 4, Deadlock 4…) | Componentes iguais reimplementados em cada tela. |
| Botões de texto com setas/caracteres (`→ ⇉ ← ↻`) | Debloat, Limpeza, Inicialização, Advanced | Ícones fora da fonte de ícones, sem nome acessível. |
| `Click=` em code-behind | `TempCleanupPreviewWindow` (3) | Viola o padrão do projeto (MVVM, `CLAUDE.md`). |
| Estado "Ativo" | Roxo nos cards (`CardBrushes.Active = Accent`), verde no Histórico | Identidade confundida com estado. |
| Cabeçalhos de página | 4 formatos diferentes (com/sem sobretítulo, com/sem selo em caixa alta, título ≠ menu) | Navegação parece de produtos diferentes. |

Componentes que **já servem de base** e devem ser reaproveitados:
`Lzn.Card`, `Lzn.Panel`, `Lzn.Badge.*`, `Lzn.ToggleSwitch`, `Lzn.PrimaryButton`/`GhostButton`/`ToolbarButton`/`DangerButton`,
`Lzn.SearchBox`, `Lzn.Tab`, `Lzn.NavItem`, `Lzn.FocusVisual(Inset)`, `Controls/CategoryInfo` ("Saiba mais"),
`Controls/AdaptiveGridPanel`, `ThemeBrushes` (acesso a cores em C#), o layout lista + detalhe de `TweakCardsView` e o
estilo de linha `CardRow`.

---

## 5. Arquitetura do sistema de design

### 5.1 Organização dos arquivos

```
Themes/
  Theme.xaml                 ponto de entrada (ordem dos merges)
  Tokens/
    Colors.xaml              primitivos: 8 oficiais + estados + derivados (ÚNICO lugar com HEX)
    Brushes.xaml             papéis semânticos (Background, Surface, Text, Accent, State.*)
    Typography.xaml          fontes, escala, estilos de TextBlock (Display…Caption, Mono, Overline)
    Layout.xaml              espaçamento (4/8/12/16/24/32/48), raios, larguras (sidebar, detalhe, breakpoints)
    Icons.xaml               glifos nomeados (sys:String) e geometrias de marca (Discord)
  Controls/
    Buttons.xaml             Primary, Secondary, Ghost, Danger, Icon (todos com hover/focus/pressed/disabled/busy)
    Inputs.xaml              SearchBox, ComboBox, CheckBox, ToggleSwitch, TextBox
    Navigation.xaml          NavItem, Tab, Segmented (filtros), Breadcrumb do caminho
    Lists.xaml               ListRow (lista + detalhe), GroupHeader, ScrollBar
    Surfaces.xaml            Card, Panel, DetailPane, Divider
    Feedback.xaml            Badge (por estado), Banner (info/aviso/erro), EmptyState, Loading, Toast, ToolTip
  Implicit.xaml              padrões sem chave (ScrollBar, ToolTip, ComboBoxItem, ProgressBar)
  Legacy.xaml                aliases antigos, isolados e marcados para remoção (Fase 6)
Controls/ (componentes reutilizáveis em C#/XAML)
  PageHeader                 título (= nome no menu), subtítulo, "Saiba mais", status, ações à direita
  SectionHeader              ícone + título + contador + descrição
  StatusBadge                recebe um StatusKind e escolhe cor, ícone e texto
  Banner                     informação/aviso/erro com ação opcional e "Detalhes técnicos" recolhível
  EmptyState                 ícone + frase + ação
  LoadingState               indicador + texto ("Lendo o estado…"), mesmo lugar do conteúdo
  MasterDetailLayout         lista + detalhe fixo, com painel sobreposto abaixo da largura mínima
  DetailSection              rótulo + conteúdo, para padronizar os detalhes
  ValueBlock                 valor técnico em monoespaçada com botão copiar
```

Regras:

- **HEX só em `Colors.xaml`.** Views usam papéis (`Lzn.Surface`), nunca primitivos (`Lzn.Color.*`).
- **Nada de `FontSize` literal** em views: usar os estilos de texto. Exceção: ícones (`Lzn.Icon.*` com tamanhos 12/14/16/20).
- **Raios só por token:** `Sm 6` (selos, campos), `Md 8` (botões, linhas), `Lg 12` (cards, painéis).
- **Espaçamento em múltiplos de 4.** Página: 32 nas laterais e 24 no topo. Entre cards: 12. Dentro de card: 16.
- **C# acessa cor só por `ThemeBrushes`**, e estado só por `StatusKind → brush` (um mapeamento, um lugar).
- **`StaticResource`** continua sendo a regra (não há troca de tema em tempo de execução).
- **Testes de guarda** (novos, em `LznTweaker.Core.Tests` ou num projeto de testes da App):
  1. Nenhum `#RRGGBB` fora de `Colors.xaml`.
  2. Nenhum `FontSize="n"` em views (após a migração de cada tela, numa lista de permitidos que encolhe).
  3. Contraste ≥ 4,5:1 nos pares texto/fundo declarados em `Brushes.xaml`.

### 5.2 Tipografia

Fonte (escolhida em 02/11/2026 a pedido do usuário; antes Space Grotesk no conteúdo e IBM Plex Sans na sidebar): **Geist** no app inteiro e **Geist Mono** em textos técnicos, embutidas no app (SIL OFL 1.1, ver `Assets/Fonts/FONTES.md`), com Segoe UI e Cascadia Mono como reserva.
Técnica: **Cascadia Mono, Consolas**.

| Estilo | Tamanho / peso | Uso |
|---|---|---|
| Display | 32 / Semibold | Só a Home |
| Title | 24 / Semibold | Título da página |
| Section | 18 / Semibold | Título do detalhe, seções |
| Subtitle | 15 / Semibold | Cabeçalho de grupo, título de card |
| Body | **14** / Regular | Texto principal (hoje 13) |
| BodySmall | 13 / Regular | Descrições em linhas e cards |
| Caption | **12** / Regular | Metadados (mínimo para texto corrido) |
| Overline | 11 / Semibold, maiúsculas | Só rótulos de seção curtos |
| Mono | 12 / Regular | Chaves de registro, comandos, caminhos |

Números de contadores e tamanhos com dígitos de largura fixa (`Typography.NumeralAlignment="Tabular"`).

### 5.3 Estados de interação (todos os controles)

| Estado | Tratamento |
|---|---|
| Repouso | Surface; texto Text/Muted; ícone Muted |
| Hover | Fundo SurfaceHover (linhas) ou Elevated (botões secundários); botão principal clareia para Lavender |
| Foco (teclado) | Anel de 2 px Lavender, por fora (ou interno em listas); nunca removido |
| Pressionado | Botão principal em AccentPressed; demais escurecem um degrau |
| Selecionado | AccentSoft + traço AccentStroke + marcador de 3 px Accent |
| Desabilitado | 45% de opacidade, cursor padrão, dica explicando por quê (`ShowOnDisabled`) |
| Ocupado | Botão mostra indicador e texto no gerúndio ("Aplicando…"), desabilitado; a página não congela |

Movimento: 150 ms para hover, 180 ms para troca de página e painel, com saída mais rápida que entrada. Com
"Mostrar animações no Windows" desligado (`SystemParameters.ClientAreaAnimation`), sem animação.

### 5.4 Ícones

- Só **Segoe Fluent Icons** (linha fina), com fallback para Segoe MDL2. Nada de caracteres de texto (`→ ⇉ ↻ ✓`) ou emoji.
- Glifos com nome em `Icons.xaml` (`Lzn.Glyph.Refresh`, `.Search`, `.Restore`, `.Warning`…): o mesmo conceito tem
  sempre o mesmo ícone.
- Tamanhos: 12 (dentro de selos), 14 (linhas, botões), 16 (cabeçalhos), 20 (sidebar recolhida), 28 (estados vazios).
- Logos de terceiros (jogos, Steam, Riot, Discord, programas) continuam como imagem/geometria oficial, sem recolorir.

### 5.5 Tamanhos de janela

| Largura do conteúdo | Comportamento |
|---|---|
| ≥ 960 px | Lista + detalhe lado a lado |
| < 960 px | Detalhe abre por cima da lista (já implementado) |
| Janela < 1180 px | Proposta: sidebar recolhe sozinha (o usuário pode reabrir) |
| Mínimo | 980 × 640 (atual) |

Grades de cards usam `AdaptiveGridPanel` (já existe) com largura mínima por item, nunca colunas fixas.

---

## 6. Componentes a padronizar

| Componente | Hoje | Padrão novo |
|---|---|---|
| Cabeçalho de página | 4 variações; títulos diferentes do menu; selos em caixa alta | `PageHeader`: título = nome do menu, subtítulo de uma linha, "Saiba mais", linha de status, até 2 ações à direita (uma principal) |
| Botões | Primary, Glow, Toolbar, Ghost, Danger + legados Primary/SecondaryButtonStyle; Title Case misturado | 5 estilos (Primary, Secondary, Ghost, Danger, Icon); texto com só a primeira letra maiúscula; verbo no infinitivo |
| Controle binário de estado do sistema | Interruptor (Geral), caixa de marcar (Inicialização) | **Interruptor** |
| Seleção para ação em lote | Caixa de marcar (Limpeza, Debloat, Jogos, Advanced) | **Caixa de marcar** + barra de ação que só habilita com seleção |
| Ação pontual | Botão em card | **Botão** (Secondary na linha, Primary no detalhe) |
| Selos | Risco, estado, categoria, requisitos, origem: até 4 por item (Jogos) | No máximo 2 na linha (risco + 1); o resto vai para o detalhe |
| Estado | Texto colorido com roxo/verde variando | `StatusBadge` (ponto + texto) com as cores de estado |
| Lista + detalhe | Só em TweakCardsView | `MasterDetailLayout` + `ListRow` |
| Avisos | Texto vermelho solto, faixas de cores variadas | `Banner` (info/aviso/erro) com ação e "Detalhes técnicos" |
| Vazio / carregando / erro | Painéis em branco, textos soltos | `EmptyState`, `LoadingState`, `Banner` |
| Barra de pendências | Só em TweakCardsView | Mesmo componente onde houver lote (Geral, Rede, Limpeza, Debloat, Jogos) |

---

## 7. Análise das 13 telas

Legenda de esforço: **P** pequeno (só estilos), **M** médio (troca de layout), **G** grande (muda estrutura/navegação).

| Tela | Situação hoje | Proposta | Esforço |
|---|---|---|---|
| **Home** | Título e 3 cards de valores; 60% vazia; não mostra nada do PC | Painel "Estado do PC" com dados **reais** (ver decisão 8.5) + atalhos; o manifesto de valores vai para "Sobre" | M |
| **Geral** (+ Discord) | Já no padrão novo | Ícones das linhas em Muted (roxo só na seleção); "Ativo" em verde; receber os Perfis como atalho de seleção (decisão 8.1) | P→M |
| **Rede** (Network) | Já no padrão novo | Nome em português; linha de status sem "0 de 3 ajustes ativos" quando a aba é só de testes | P |
| **Reparos** (Fixes) | Já no padrão novo | Nome; o texto "Abrir agora?" da confirmação não deve aparecer no detalhe (é da caixa de diálogo) | P |
| **Avançado** (Advanced) | "Biblioteca de Tweaks": tabela em 2 colunas com caixas, "?" de 20 px, selos "Baixo/Médio", títulos em inglês ("Essential Tweaks", "Customize Preferences"), selo "EXECUÇÃO COORDENADA", "Detectar Estado Real" | Virar **Perfis** (decisão 8.1); até lá, só estilos: cabeçalho, português, "?" → botão de ícone acessível de 32 px | G |
| **Inicialização** | Cards grandes por item, caixa de marcar como estado, nomes crus de registro com hash, selo "COMPATIBILIDADE NATIVA (STARTUPAPPROVED)" | `MasterDetailLayout`: linha com nome amigável, fabricante, impacto e interruptor; detalhe com origem (HKCU/HKLM/pasta), comando completo em Mono e efeito de desativar. **Mesma semântica de hoje** (aplica ao alternar, se é o que faz hoje) | M |
| **Debloat** | Duas colunas com setas de texto `→ ⇉ ← ⇇`; painéis vazios quando a leitura falha; texto técnico de erro | Lista única com caixas de marcar, agrupada (Patrocinados, Microsoft, Sistema: protegidos), filtro "Selecionados"; detalhe do app (o que é, se é seguro, como voltar); barra de ação "Remover N apps" habilitada só com seleção; `Banner` de erro claro | M |
| **Limpeza** | Bom resumo no topo; cards altos por área; Title Case ("Iniciar Varredura", "Limpar Selecionados", "Marcar Todos"); selo "ARQUIVOS EM USO SÃO PRESERVADOS" | Manter o resumo (é o melhor uso de número grande do app); áreas como linhas compactas (caixa, nome, caminho em Mono, tamanho alinhado à direita); a frase do selo vira subtítulo | M |
| **Jogos** | Já é mestre-detalhe por jogo; ações com 4 selos cada (texto 10 px); sobretítulo "OTIMIZAÇÃO POR JOGO" | Alinhar ao `ListRow`/`DetailSection`: no máximo 2 selos; requisitos e reversão no detalhe da ação; tipografia mínima 12 | M |
| **Placa de Vídeo** | Simplificada recentemente; chips de valor recomendado; muitos estilos locais (8) | Migrar estilos locais para os globais; valores recomendados como `ValueBlock`; manter as 4 abas | P |
| **Hardware** | 5 cards de resumo bons; título duplicado ("Hardware — somente diagnóstico"); chevrons em círculo branco destoando | Remover o subtítulo duplicado; acordeões com o chevron padrão; números em largura fixa | P |
| **Programas** | Cards longos (descrição, funções, configurações), bem escritos | `MasterDetailLayout`: linha com logo, nome, finalidade e selo de licença; detalhe com funções, configurações recomendadas e "Site oficial". Filtros como segmentado | M |
| **Histórico** (Backups) | Linhas boas e honestas; "Restaurar selecionados" roxo sem seleção possível; linhas cortadas sem dica | `MasterDetailLayout`: detalhe com valor anterior, verificação e "Restaurar este"; caixas de marcar só nos itens restauráveis; botão principal desabilitado até haver seleção | M |
| **Componentes globais** | Sidebar (feita), toasts, painel de detalhes, janela de prévia da Limpeza (`Click=`), diálogos | Sidebar em Surface; toasts com `StatusBadge`; janela de prévia com comandos (MVVM) e estilos novos; diálogos de confirmação com botão destrutivo em Danger | P–M |

---

## 8. Decisões que precisam da sua aprovação

### 8.1 Página Advanced: Perfis, tabela ou sair do menu?

Contexto (conferido no código): o catálogo tem 38 operações. A Geral e a Rede mostram 36. As 2 que faltam ficam de
fora de propósito: o ponto de restauração é criado automaticamente em todo lote, e a limpeza de temporários exige
prévia na página Limpeza. A Advanced mostra o catálogo inteiro em tabela com os presets. Ou seja, **todo ajuste da
Advanced já existe na Geral**: a página é 100% duplicada, com controles e estados de pendência independentes. O
que só ela tem são os **presets** (Padrão, Mínimo, Avançado) e a **visão em tabela**.

| Opção | Vantagens | Desvantagens |
|---|---|---|
| **A. Virar "Perfis"** *(recomendada)* | Acaba a duplicidade; os presets ganham uma tela própria que explica o que cada um marca e por quê; aplicar continua num só lugar (Geral) | Exige um serviço compartilhado de pendências para os Perfis marcarem itens na Geral |
| B. Continuar como tabela ("modo especialista") | Zero risco imediato; quem quer ver tudo de uma vez continua tendo | A duplicidade continua: o mesmo ajuste pode ficar pendente em dois lugares com estados diferentes |
| C. Sair do menu | Interface mais simples; nada se perde além dos presets | Os presets somem até ganharem outro lugar (por exemplo, um botão "Aplicar perfil" na Geral) |

**Recomendação:** A, em duas etapas reversíveis:
1. Criar o serviço de pendências compartilhado e conferir por teste que cada preset marca os mesmos ids de antes.
2. Trocar a Advanced por Perfis: cada perfil mostra a lista do que marca, e o botão "Marcar na Geral" leva à Geral
   com os itens pendentes. Nada é aplicado sem "Revisar e aplicar". A visão em tabela pode voltar como uma opção
   de densidade da própria Geral, se fizer falta.

### 8.2 Como eliminar a duplicidade Geral × Advanced

Uma única fonte de pendências (`PendingChangesService`, singleton), usada pela Geral e pelos Perfis, e um único
executor de lote (o atual). A Advanced deixa de ter estado próprio de seleção. Até a etapa 2 de 8.1, a duplicidade é
mitigada com um aviso no topo da Advanced: "Estes ajustes também estão na página Geral; aplique por um lugar só".

### 8.3 Controles entre Geral, Jogos, Inicialização e outras

Regra única, já descrita na seção 6:

- **Interruptor:** liga e desliga um estado persistente do sistema.
- **Caixa de marcar:** seleciona itens para uma ação em lote.
- **Botão:** ação pontual.

Na Inicialização, a troca de caixa por interruptor é só visual: o comportamento de aplicar continua exatamente o de
hoje. Vantagem: o usuário aprende uma vez. Desvantagem: exige revisar o texto de cada tela para deixar claro
quando algo é imediato e quando vai para a fila.

### 8.4 Aproveitar a lista + detalhe

Extrair `MasterDetailLayout` do `TweakCardsView` (sem mudar o comportamento das 3 páginas que já usam) e aplicar em
Inicialização, Histórico, Programas, Debloat e Limpeza, nessa ordem (do mais simples ao mais sensível).

- Vantagens: menos código, uma única experiência de teclado (setas, Esc) e o mesmo comportamento em janela estreita.
- Desvantagem: a extração mexe numa tela que já funciona. Risco mitigado pelo teste de interface, que já cobre a
  lista, o detalhe e a janela estreita.

### 8.5 Home útil sem excesso

Mostrar só o que o app **já sabe ler**, sem números inventados:

1. Ajustes ativos (detecção existente).
2. Alterações pendentes e reinício necessário.
3. Última operação registrada no histórico, com data.
4. Resumo do PC numa linha (CPU, GPU, RAM, do serviço de Hardware).
5. Três atalhos: Geral, Limpeza, Jogos.

O manifesto "Base técnica, Segurança, Transparência" vai para uma seção "Sobre". Não entra "último ponto de
restauração" enquanto o app não tiver uma leitura confiável dele.

- Vantagens: a Home vira ponto de partida real.
- Desvantagem: ler o estado ao abrir custa tempo, então a carga é assíncrona, com `LoadingState`.

### 8.6 Outras decisões menores

- **Nomes em português no menu:** Network → **Rede**, Fixes → **Reparos**, Backups → **Histórico**,
  Debloat → **Remover apps**, Advanced → **Perfis** (ou **Avançado**, se a 8.1 não for aprovada).
- **Logotipo:** a arte atual (cromado com seta vermelha) destoa da paleta. Proposta: uma variante monocromática
  (Text ou Accent) só para a sidebar, mantendo a original em "Sobre". Depende da sua aprovação, por ser um ativo de marca.

---

## 9. Fases de implementação revisadas

Cada fase é dividida em entregas de um commit, cada uma testada antes da próxima. Nenhuma toca em `LznTweaker.Core`
além de textos, salvo onde indicado.

### Fase 0: Fundação (sem mudança visual)
1. Tirar prints de referência (`%TEMP%\lzn-smoke`) e guardá-los fora do repositório.
2. Reorganizar `Themes/` na estrutura da seção 5.1, **mantendo todas as chaves atuais** (os aliases vão para `Legacy.xaml`).
3. Criar os tokens que faltam: espaçamento, raios, glifos nomeados, estilos de texto da escala nova (ainda sem uso).
4. Testes de guarda (HEX fora de Colors, contraste dos pares).
5. Ampliar o teste de interface para abrir também as janelas e diálogos (prévia da Limpeza, confirmações).

*Validação:* prints idênticos aos de referência; build sem avisos; todos os testes.

### Fase 1: Identidade e linguagem
1. Componentes `PageHeader` e `StatusBadge`; aplicar em todas as páginas, uma por commit.
2. Estado "Ativo" em verde (`CardBrushes.Active → Success`); ícones de linha em Muted.
3. Sidebar em Surface; remover `Gray925`.
4. Escala tipográfica nova (Body 14, mínimo 12), página a página, trocando `FontSize` literais por estilos.
5. Textos: português (após 8.6), botões com só a primeira letra maiúscula, sem selos em caixa alta.

*Validação:* prints comparados lado a lado; nenhuma linha cortada nova; contraste; navegação por Tab em cada página.

### Fase 2: Estados e mensagens
1. `Banner`, `EmptyState` e `LoadingState`.
2. Mapear exceções conhecidas para frases claras, com "Detalhes técnicos" recolhível. Aplicar em Debloat, Jogos,
   Histórico e Limpeza.
3. Regra de habilitação: ação principal desabilitada sem seleção, com dica explicando por quê.
4. `TempCleanupPreviewWindow` sem `Click=` (comandos e `IUserDialogs`).

*Validação:* o teste de interface (sem administrador) passa a mostrar os `Banner`s em vez de texto cru, e os prints
confirmam; nenhum fluxo de aplicação muda.

### Fase 3: Lista + detalhe em todo o app
1. Extrair `MasterDetailLayout` + `ListRow` (Geral, Rede e Reparos continuam iguais).
2. Inicialização → Histórico → Programas → Limpeza → Debloat, uma por commit.
3. Jogos alinhado a `ListRow`/`DetailSection`, no máximo 2 selos por item.

*Validação:* teste de interface por página (seleção automática, busca vazia, janela estreita); conferência manual
de que nenhuma ação mudou de comportamento.

### Fase 4: Estrutura (depende das decisões 8.1, 8.5 e 8.6)
1. Teste de paridade: todo id do catálogo aparece numa página (exceto ponto de restauração e temporários, por regra).
2. `PendingChangesService` compartilhado.
3. Advanced → Perfis.
4. Home como painel.
5. Placa de Vídeo e Hardware nos componentes globais.

*Validação:* testes do catálogo; teste de que os presets marcam os mesmos ids de antes; prints.

### Fase 5: Teclado e acessibilidade
1. Atalhos: Ctrl+F (buscar), F5 (verificar estado), Esc (fechar), Ctrl+Enter (revisar e aplicar, se houver pendências).
2. `AutomationProperties.Name` em todo botão só com ícone; ordem de Tab revisada; foco vai para o detalhe ao abrir.
3. Áreas clicáveis de pelo menos 32 × 32 px.

*Validação:* percorrer cada página só com teclado; Narrador do Windows lê os controles principais.

### Fase 6: Acabamento
1. App lembra o estado da sidebar e o tamanho da janela.
2. Movimento padronizado e respeito ao "Mostrar animações".
3. Auditoria de ícones (só glifos nomeados).
4. Remover `Legacy.xaml` quando o teste confirmar zero usos.

*Validação:* teste de guarda de aliases (zero usos); prints finais.

### Fase 3b: layout "Foco" (aprovado em 31/10/2026, a partir do mockup C)

Referência: o print do mockup C (lista à esquerda, detalhe grande à direita). Decisões do usuário:

- **Cor:** continua o roxo Obsidian. O laranja do mockup não entra.
- **Sidebar:** abre recolhida, só com ícones (nome na dica). O botão de expandir continua e a escolha é lembrada entre
  sessões (adianta o item 1 da Fase 6).
- **Controle:** a lista não tem interruptor. Cada linha mostra ponto de estado, nome e estado. Ligar ou desligar fica no
  botão principal do detalhe, que só marca o ajuste como pendente; aplicar continua sendo por "Revisar e aplicar", com
  ponto de restauração.
- **Quadros do detalhe:** só texto real do catálogo. "Compatibilidade" e "Como desfazer" ficam lado a lado, e "Como
  funciona" (motivo técnico) fica recolhível. Não se cria campo "Benefício".

O que muda, em ordem de entrega (um commit cada):

1. **Sidebar recolhida por padrão e lembrada.** O item ativo, recolhido, vira um quadrado roxo suave com ícone em
   Lavender, como no mockup. Preferência salva em `%LocalAppData%\LznTweaker\ui.json`, e um arquivo corrompido volta ao
   padrão.
2. **Cabeçalho compacto das páginas de lista.** O título e a contagem ficam na mesma linha ("Geral  14 de 33 ativos").
   "Saiba mais" vira um ícone de informação ao lado do título. A busca fica logo abaixo, com o ícone de filtros dentro
   da mesma linha. As abas (Ajustes do Windows, Discord) ficam como um seletor discreto.
3. **Lista limpa.** Coluna própria, separada do detalhe por um traço de 1 px. Rótulos de grupo em Overline com espaço
   entre letras, mantendo o ícone da categoria. Linhas de 48 px, sem ícone, descrição nem interruptor: ponto colorido,
   nome em 15 px e estado à direita. A linha selecionada usa o roxo suave e o marcador de 3 px.
4. **Detalhe grande, sem moldura.**
   - Selos no topo: categoria em roxo suave, risco e modo de aplicação.
   - Título com 32 px.
   - Descrição com 16 px.
   - Quadros "Compatibilidade" e "Como desfazer".
   - Quadro **"O que será alterado"**, gerado do catálogo: chaves de registro com o valor (HKCU/HKLM), serviços com o
     tipo de inicialização e tarefas agendadas. Script aparece como "Script do PowerShell, verificado depois".
   - "Como funciona" recolhível.
   - "Estado detectado".
5. **Barra de ação no rodapé do detalhe.**
   - Botão principal conforme o estado: "Ativar ajuste" ou "Desfazer ajuste", que marcam como pendente, e
     "Cancelar alteração" quando já está pendente. Em ação pontual: "Adicionar à fila". Em atalho: o texto do atalho.
     Em guia: nenhum.
   - Botão secundário "Verificar estado".
   - Estado à direita (ex.: "● Ativo · confirmado no sistema").
   - Quando a ação não está disponível, o botão fica desabilitado e a dica explica por quê.
6. **Geral, Rede e Reparos migram para o `MasterDetailLayout`** com esse visual. O mesmo padrão de linha limpa e detalhe
   grande vale para Inicialização (interruptor sai da linha e fica no detalhe), Histórico, Programas e Remover apps
   (a caixa de marcar continua na linha, porque lá ela é seleção para lote, não estado).

Validação: o teste de interface cobre seleção, busca vazia, janela estreita e os botões do detalhe (sem aplicar nada).
Prints comparados com o mockup.

### Depois do redesign
- Conferir os nomes do Discord no app real.
- Teste de ponta a ponta numa máquina virtual (aplicar, verificar, desfazer, com ponto de restauração).
- Juntar a `feature/obsidian` na `main`.
- Distribuição.

---

## 10. Ordem recomendada e dependências

```
Fase 0 ──► Fase 1 ──► Fase 2 ──► Fase 3 ──► Fase 5 ──► Fase 6
                          ▲                    ▲
     decisões 8.1/8.5/8.6 ┴──► Fase 4 ─────────┘
```

- A Fase 0 vem primeiro porque torna todas as outras mais baratas e verificáveis.
- As Fases 1 e 2 mudam aparência e textos, não estrutura: menor risco e o maior ganho visível.
- A Fase 3 depende dos componentes das Fases 1 e 2.
- A Fase 4 pode começar em paralelo quando as decisões forem aprovadas, mas o passo "Advanced → Perfis" só depois
  do serviço de pendências compartilhado e do teste de presets.
- Partes da Fase 5 (nomes acessíveis) podem acompanhar cada tela migrada.

## 11. Riscos e mitigação

| Risco | Probabilidade / impacto | Mitigação |
|---|---|---|
| Mudar um `ResourceDictionary` afeta todas as telas | Alta / médio | Uma mudança por commit; prints comparados; teste de interface |
| Recurso ausente só quebra ao abrir a tela (XAML em tempo de execução) | Média / alto | O teste de interface abre todas as páginas; ampliá-lo para diálogos (Fase 0) |
| Body 13 → 14 corta textos e quebra alturas | Alta / baixo | Página a página, com prints; `TextTrimming` + dica |
| Mesma operação pendente em duas páginas (Geral × Advanced) | Média / alto | `PendingChangesService` antes dos Perfis; aviso provisório na Advanced |
| Extração do `MasterDetailLayout` regredir Geral, Rede e Reparos | Baixa / médio | Os testes atuais de lista, detalhe e janela estreita viram a régua |
| Mudar o comportamento da Inicialização sem querer | Baixa / alto | A troca é só de template; os bindings e comandos são conferidos no diff |
| Sidebar em Surface perder separação do conteúdo | Média / baixo | Traço de 1 px + fundo; validar no print |
| Ativos de marca (logo) alterados sem aprovação | — | Só com o seu OK (8.6) |

## 12. Critérios de validação (cada entrega)

**Funcional**
- `dotnet build` sem avisos; `LznTweaker.Core.Tests` e `LznTweaker.App.Smoke` aprovados.
- Nenhuma mudança em scripts, catálogo de operações, executor de lote, ponto de restauração ou histórico (o diff
  confirma: só XAML, view-models de apresentação e textos).
- Nada é aplicado sem "Revisar e aplicar"; ações imediatas continuam pedindo confirmação.

**Visual**
- Prints de 1400 × 880 e de 1000 px de largura comparados com os de referência.
- Todas as telas com o mesmo cabeçalho, botões, selos e estados.
- Roxo só onde há ação ou seleção; estados com as cores de estado e com texto ou ícone.

**Acessibilidade**
- Contraste ≥ 4,5:1 no texto (teste automático dos pares).
- Foco visível em todo controle; página utilizável só com teclado.
- Texto corrido com pelo menos 12 px.

**Código**
- Nenhum HEX fora de `Colors.xaml`; nenhum `FontSize` literal nas telas já migradas; nenhum `Click=`.
- Nenhum alias legado nas telas já migradas.
