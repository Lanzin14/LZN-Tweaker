# Reestruturação de UX/UI — auditoria e plano

Data: 31/10/2026. Base: as 13 páginas atuais (prints do teste de interface em `%TEMP%\lzn-smoke`) e os princípios do
Razer Cortex e do WinUtil. Plano aprovado e implementado em 01/11/2026, em seis commits (fases 1 a 6). Diferenças em relação ao plano estão no fim do documento.

Objetivo: menos telas, menos texto e menos cliques, sem perder recursos úteis e mantendo a identidade Obsidian
(fundo escuro, violeta como destaque; a identidade foi refinada na reformulação visual de 02/11/2026, no fim deste arquivo).

---

## Etapa 1 — Auditoria

### Problemas gerais

| # | Problema | Onde aparece |
|---|---|---|
| G1 | **Recursos duplicados em mais de um lugar.** O usuário não sabe onde aplicar. | Geral × Avançado (mesmos ajustes; o próprio Avançado avisa "aplique por um lugar só"). Hardware × Jogos › "Hardware e diagnóstico" (mesmas orientações). Limpeza × Jogos › Windows (cache de shaders do DirectX nos dois). Placa de Vídeo › Perfis por jogo × página de cada jogo. |
| G2 | **13 itens na sidebar**, vários com 3 ou 4 itens só. | Rede (3 itens), Reparos (4), Hardware (só leitura), Programas. |
| G3 | **Quatro layouts de página diferentes.** | Cabeçalho + conteúdo (Hardware, Limpeza, Placa de Vídeo); lista + detalhe com título dentro da lista (Geral, Rede, Reparos); lista + detalhe abaixo do cabeçalho (Inicialização, Histórico, Programas, Remover apps); biblioteca (Jogos). |
| G4 | **Espaço vazio grande.** O painel de detalhe ocupa metade da tela para 1 ou 2 frases. | Rede, Reparos, Histórico, Inicialização. |
| G5 | **Texto repetido em cada linha.** | Placa de Vídeo: "Requer configuração manual" 9 vezes. Jogos: cada ação repete caminho, risco, "Afeta", "Antes" e reversão. |
| G6 | **Botão principal em lugares diferentes** (barra inferior, canto superior, dentro do card) e com nomes diferentes (Ativar ajuste, Executar selecionados, Limpar selecionados, Aplicar perfil selecionado, Remover marcados). | Todas as páginas de ação. |
| G7 | **Home sem função.** Texto de apresentação e três cards institucionais; nenhuma ação, estado do PC ou atalho. | Home. |
| G8 | **Informação técnica sempre aberta.** Caminho de registro, referência e "Estado detectado" aparecem antes de o usuário decidir. | Detalhe de Geral; ações de Jogos. |

### Por tela

- **Home:** não ajuda a decidir nada. É a primeira tela e a mais vazia.
- **Otimização do Windows (Geral + Avançado + Rede › Ajustes):** o mesmo conteúdo em três lugares e dois formatos. O Avançado (lista de caixas de seleção com perfis) é o formato mais rápido; o Geral (um ajuste por vez, com botão por ajuste) pede um clique por item.
- **Limpeza:** fluxo bom (varrer, ver total e limpar). Os cards de área repetem o caminho completo e o tamanho duas vezes ("65,9 MB" e "1.116 arquivo(s) · 65,9 MB").
- **Inicialização:** boa. O detalhe mostra comando e origem, que interessam a poucos, e o interruptor fica longe da lista.
- **Jogos:** a página mais carregada. Os cards de ação são altos (cinco linhas cada), há orientações longas e a seção "Hardware" duplica a página Hardware.
- **Placa de Vídeo:** quatro abas. A aba da fabricante errada aparece com o mesmo destaque, "Perfis por jogo" duplica a página Jogos e há selos repetidos em cada linha.
- **Programas:** já foi enxugada (o que faz, tutorial, cuidados).
- **Histórico e restauração:** funcional, mas o detalhe ocupa metade da tela para 4 campos e a restauração fica escondida no topo.
- **Hardware, Rede e Reparos:** páginas pequenas que poderiam ser abas de uma página só.

---

## Etapa 2 — Plano de reorganização

### Navegação: de 13 para 9 itens

| Hoje | Proposta | Motivo |
|---|---|---|
| Home | **Início** (painel) | Passa a ter estado do PC e ações rápidas. |
| Geral, Avançado, Rede › Ajustes | **Ajustes do Windows** | Fim da duplicação (G1). |
| Placa de Vídeo | **Placa de Vídeo** | Mantida, mais compacta. |
| Remover apps | **Remover apps** | Mantida. |
| Jogos | **Jogos** | Mantida, sem a seção Hardware. |
| Limpeza | **Limpeza** | Mantida. |
| Inicialização | **Inicialização** | Mantida. |
| Hardware, Reparos, Rede › Diagnóstico e Reparo, Programas | **Ferramentas** (abas: Reparos, Rede, Hardware, Programas) | Páginas pequenas viram abas; nada é removido. |
| Histórico | **Histórico** | Mantido, com a restauração mais visível. |

Seções da sidebar: Início · Otimização (Ajustes, Placa de Vídeo, Remover apps) · Jogos e manutenção (Jogos, Limpeza,
Inicialização) · Ferramentas e segurança (Ferramentas, Histórico).

### Componentes padronizados (valem para todas as telas)

1. **Cabeçalho único:** título, uma linha de descrição e, à direita, só ações de página (Atualizar). O "Saiba mais" vira um ícone ⓘ ao lado do título.
2. **Barra de ação fixa no rodapé**, o padrão "Run Tweaks" do WinUtil: "N selecionados · Aplicar · Desfazer selecionados". É o mesmo lugar e o mesmo nome em Ajustes, Jogos, Limpeza e Remover apps.
3. **Linha de lista única:** caixa de seleção, nome, uma frase curta, selo de risco e estado (ponto colorido + palavra), com altura fixa de cerca de 44 px.
4. **Detalhe sob demanda:** clicar na linha abre um painel lateral estreito ou expande a linha. Registro, referências e "como funciona" ficam num expansor "Detalhes técnicos".
5. **Selos limitados a três tipos:** risco (Baixo/Médio/Alto), estado (Ativo/Inativo/Parcial/Pendente) e "Manual" (o app não aplica). Um aviso que vale para a página inteira aparece uma vez no cabeçalho, nunca em cada linha.
6. **Textos:** uma frase por item na lista e até três passos em tutoriais, como já foi feito em Programas.

### Tela a tela

**Início (painel)**
- *Manter:* o logotipo e a versão, menores, no rodapé.
- *Adicionar, com dados que já existem:*
  - uma faixa "Seu PC" com CPU, GPU, RAM e disco (hoje na página Hardware);
  - três ações rápidas com contagem: "Ajustes recomendados (7 pendentes)", "Limpeza (69,9 MB)" e "Inicialização (8 ativados)";
  - a última alteração, com atalho para Desfazer.
- *Remover:* o texto de apresentação e os três cards institucionais (Base técnica, Segurança, Transparência). A mensagem passa para uma linha no rodapé.

**Ajustes do Windows** (Geral + Avançado + Rede › Ajustes)
- *Formato:* lista de caixas de seleção agrupada por categoria (Desempenho em jogos, Privacidade, Interface, Rede, Avançados com selo "Cuidado"), com os perfis Recomendado, Mínimo e Limpar seleção no topo e busca.
- *Detalhe:* um clique no nome expande a linha com a descrição e o expansor técnico. Não há mais um botão "Ativar" por ajuste: tudo passa pela barra de ação.
- *Manter:* o estado lido do sistema, a revisão antes de aplicar, o ponto de restauração, o histórico e o desfazer. A aba Discord vira uma categoria "Discord (manual)".
- *Remover:* a página Avançado e a lista duplicada.

**Limpeza**
- *Simplificar:* cada área numa linha só (nome, uma frase e o tamanho), com o caminho no detalhe técnico.
- *Agrupar:* "Caches de jogos e shaders" passa a reunir os itens de cache hoje em Jogos › Windows. A página Jogos aponta para cá.
- *Manter:* varredura, total e "Limpar selecionados" na barra de ação.

**Inicialização**
- *Simplificar:* o interruptor fica na própria linha, o que dá um clique por item. Comando e origem vão para o detalhe técnico.
- *Manter:* busca, filtro e "Desativar launchers de jogos" como ação rápida.

**Jogos**
- *Simplificar:*
  - as ações (cache de shaders do jogo) viram linhas com caixa de seleção e passam pela barra de ação;
  - as orientações viram uma lista de títulos com expansor;
  - as configurações recomendadas (CS2, Fortnite) ficam numa aba "Configurações" dentro do jogo, separada de "Ações" e "Orientações".
- *Remover:* a seção "Hardware e diagnóstico", que vai para Ferramentas › Hardware.
- *Manter:* a biblioteca à esquerda, os ícones e os selos de plataforma.

**Placa de Vídeo**
- *Simplificar:*
  - mostrar primeiro a aba da fabricante detectada; a outra fica disponível, mas discreta;
  - "Requer configuração manual" aparece uma vez no topo da aba;
  - cada linha mostra só nome, valor recomendado e ⓘ, com o caminho no ⓘ.
- *Agrupar:* "Perfis por jogo" vira um link para a página do jogo, sem duplicar.
- *Manter:* "Monitor e latência" e "Restaurar alterações".

**Ferramentas** (Reparos + Rede › Diagnóstico e Reparo + Hardware + Programas)
- *Formato:* abas. Em Reparos e Rede, cada ferramenta numa linha com o botão "Executar" na própria linha e um aviso curto. Não há mais painel de detalhe vazio.
- *Hardware:* os componentes (lidos no Início) e as orientações em expansores.
- *Programas:* como está hoje.

**Histórico e restauração**
- *Simplificar:* uma lista com data, item e resultado, e caixa de seleção nas linhas restauráveis. "Restaurar selecionados" vai para a barra de ação. O detalhe vira uma linha expandida.
- *Agrupar:* "Removidos pelo LZN Tweaker", hoje em Remover apps, passa a ser um filtro do Histórico, com atalho de volta.

### O que não muda

- Nenhum ajuste, script ou regra de segurança: ponto de restauração obrigatório, verificação, desfazer e registro no histórico.
- A identidade visual: cores, fontes e sidebar.
- O que é recusado continua recusado: prioridade Realtime, afinidade automática e promessas de FPS.

---

## Etapa 3 — Referências

| Princípio | Razer Cortex | WinUtil | Como adaptar no LZN | O que **não** copiar |
|---|---|---|---|---|
| Ação principal clara | "Boost" e "Optimize now" num botão, com reversão em um clique | "Run Tweaks" e "Undo Selected Tweaks" no rodapé | Barra de ação fixa e as ações rápidas do Início | O booster automático que mexe em processos ao abrir o jogo: sem evidência, e o LZN já recusa isso |
| Varrer → ver → limpar | System Booster com varredura e lista marcável | — | Já existe em Limpeza; levar o mesmo fluxo ao Início ("69,9 MB para limpar") | Limpeza agendada sem o usuário ver o que sai |
| Seleção em lote com perfis | — | Essential / Advanced (CAUTION) / Preferências, com presets | Ajustes do Windows como lista de caixas com perfis, como o Avançado já faz | Grade densa de texto pequeno sem explicação |
| Explicação sob demanda | Pouco texto na tela principal | Descrição no tooltip de cada item | Uma frase na linha; o resto em expansor | Esconder o risco: o selo continua visível |
| Categorias claras | Separação por função (jogos, sistema) | Abas: Install, Tweaks, Config, Updates | 9 itens com seções; páginas pequenas como abas | A aba de instalar programas (o LZN não instala nada) |
| Foco no jogo | Biblioteca de jogos com otimização por jogo | — | Página do jogo com abas Ações, Configurações e Orientações | — |

Fontes:
- [Razer Cortex — Game Booster](https://www.razer.com/cortex/booster)
- [Razer Cortex — System Booster](https://razer.com/cortex/system-booster)
- [Razer Cortex 10 — suporte e FAQ](https://mysupport.razer.com/app/answers/detail/a_id/6104)
- [WinUtil — Getting Started](https://winutil.christitus.com/userguide/getting-started)
- [WinUtil — User Guide](https://winutil.christitus.com/guides/)

---

## Etapa 4 — Implementação (aguardando aprovação)

Ordem proposta, com um commit por fase, testes e prints a cada uma:

1. **Componentes:** barra de ação fixa, linha de lista única, selos e cabeçalho com ⓘ. Base para o resto, sem mudar a navegação.
2. **Ajustes do Windows:** une Geral, Avançado e Rede › Ajustes; a página Avançado sai.
3. **Ferramentas:** reúne Reparos, Rede › Diagnóstico e Reparo, Hardware e Programas em abas; a sidebar cai para 9 itens.
4. **Início:** painel com o estado do PC, as ações rápidas e a última alteração.
5. **Jogos e Placa de Vídeo:** compactação, abas por jogo e fim das duplicações.
6. **Limpeza, Inicialização e Histórico:** linhas compactas, interruptor na linha e restauração na barra de ação.

Decisões para o usuário antes de começar:
- Confirmar a junção de Geral + Avançado numa lista de caixas de seleção (formato WinUtil). Nesse formato, o botão "Ativar" por ajuste sai.
- Confirmar o fim das páginas Hardware, Rede e Reparos como itens da sidebar (viram abas de Ferramentas).
- Confirmar o Início como painel, sem o texto de apresentação.

## Mockups

Imagens da proposta (dados de exemplo) em `docs/mockups-ux/`: `1-inicio.png`, `2-ajustes.png`, `3-ferramentas.png` e `4-jogos.png`.

## Implementação (01/11/2026)

| Fase | O que entrou |
|---|---|
| 1 | Cabeçalho com ⓘ (guia da página recolhido) e `ActionBar` (rodapé com resumo e botões). |
| 2 | Ajustes do Windows = Geral + Avançado + Rede › Ajustes: interruptor na linha, perfis Recomendado/Mínimo/Avançado, categoria Rede, detalhe curto com "Detalhes técnicos"; a página Avançado saiu. |
| 3 | Ferramentas com abas Reparos, Rede, Hardware e Programas; menu com 9 itens; chaves antigas abrem a aba certa. |
| 4 | Início como painel: PC, ações rápidas com números, últimas alterações e jogos encontrados. |
| 5 | Jogos: abas Ações/Configurações/Orientações/Diagnóstico, linhas compactas, Hardware fora da lista. Placa de Vídeo: aviso de configuração manual uma vez por aba. |
| 6 | Limpeza em linhas com barra de ação; Inicialização com interruptor na linha; Histórico com restauração na barra de ação. |

Diferenças em relação ao plano:
- Ajustes do Windows usa interruptor (estado desejado) em vez de caixa de seleção: o modelo de "pendente até revisar" já existia e mostra melhor o que está ativo. Ações de uma vez usam caixa.
- O Início não tem "Desfazer" direto nas últimas alterações: o atalho leva ao Histórico, que já faz a restauração com as verificações de segurança.
- Placa de Vídeo › Perfis por jogo foi mantida como está (a remoção ficou para depois, para não perder conteúdo).
- "Removidos pelo LZN Tweaker" continua em Remover apps.

## Reformulação visual "Obsidian + Electric Violet" (02/11/2026)

Objetivo: um app mais fácil de entender, transparente e agradável, sem mais opções. Análise com o revisor-visual (31
prints), design-critique, accessibility-review (WCAG 2.1 AA) e ui-ux-pro-max; mockups aprovados em
`docs/mockups-visual/` (Início, Ajustes, página do CS2). Nenhuma lógica de aplicar ajustes mudou.

Regras visuais (valem para telas novas):
- Violeta só para ação, seleção e foco. Estado usa verde (ativo/verificado), âmbar (pendente), vermelho (falha), azul
  (em andamento) e cinza (inativo/não verificado), sempre com texto, nunca só cor.
- Um botão principal por tela, sempre na `ActionBar` do rodapé (Ctrl+Enter aciona). Ações de linha e de detalhe são
  secundárias.
- Listas são linhas de 44 px com divisória (`Lzn.SettingRow`), sem cards dentro de cards. Lista e detalhe: a lista ocupa
  o espaço livre e o detalhe tem 440 px (`Lzn.Layout.DockedDetailWidth`), com título de 18.
- Abas de nível 1 com sublinhado (`Lzn.Tab`); nível 2 com seletor segmentado (`Lzn.Segment`).
- Valor (recomendado, configurado) em selo neutro com fonte técnica (`Lzn.ValueChip` + `Lzn.ValueText`).
- Plural sempre por `Plural.Of` ("1 app", "3 apps"), nunca "app(s)".

| Etapa | O que entrou |
|---|---|
| 0 | Plurais sem parênteses, título legível no Histórico, áreas da Limpeza com 0 B desmarcadas. |
| 1 | Paleta nova (tabela em `REDESIGN-OBSIDIAN.md` 3.1) e borda de controle ≥ 3:1, com testes de contraste. |
| 2 | Abas com sublinhado, seletor segmentado, linha padrão, raio 8; fim do `Legacy.xaml` (66 usos migrados). |
| 3 | Sidebar em Windows / Jogos / Ferramentas, Histórico fixo no rodapé, selo único de privilégio. |
| 4 | Botão principal só na barra do rodapé (Ajustes, Remover apps, Jogos). |
| 5 | Início minimalista: hardware numa linha, três ações com estado, alterações agrupadas, jogos com ícone. |
| 6 | Lista larga e detalhe fixo de 440 px; detalhe de ajuste com fatos em grade. |
| 7 | Jogos: capa local no cabeçalho, lista plana, valores neutros. |
| 8 | Placa de Vídeo: valor ao lado do nome; sai o botão "Restaurar alterações", que nunca funcionava. |
| 9 | Ferramentas sem "Pronto" repetido; Limpeza com o total uma vez; progresso em azul. |
| 10 | Ctrl+Enter na barra de ação; auditoria de nomes para leitor de tela (nenhum botão só com ícone sem nome). |

Diferenças em relação ao plano:
- "Removidos pelo LZN Tweaker" continua como painel em Remover apps (virar filtro mudaria o comportamento da lista).
- Os perfis Recomendado/Mínimo/Avançado continuam como botões: eles marcam ajustes (ação), não são um filtro.
- O Ctrl+Enter não tem teste automático: o teste de interface não simula o estado real do teclado.

## Temas claro e escuro Grafite + Azul (11/11/2026)

Referência visual: painel financeiro claro (cartões brancos sobre fundo cinza-azulado, cantos arredondados, azul de
ação, seletor de tema no rodapé do menu). O violeta saiu; o app tem dois temas com as mesmas chaves:

- **Claro**: fundo `#F3F5F9`, superfícies brancas, texto `#0F1724`, azul `#2563EB`.
- **Escuro Grafite + Azul**: chumbo `#111317`, grafite `#181B21`, azul elétrico `#2F6BEB` (texto branco nos botões).

Como funciona:
- Cores só em `Themes/Palette.Light.xaml` e `Palette.Dark.xaml`, gerados por `tools/theme/paletas.py`, que confere o
  contraste AA antes de gravar. Para mudar uma cor, edite o script e rode de novo.
- As telas usam `{DynamicResource Lzn.*}` para cores; o `ThemeService` troca a paleta em tempo real, segue o Windows
  por padrão (e acompanha a troca do sistema) e salva a escolha em `ui.json`. `ThemeBrushes` reaproveita os pincéis
  dos ViewModels e só troca a cor.
- Seletor no rodapé da sidebar: seguir o Windows, claro ou escuro; recolhida, um botão alterna.
- Visual da referência: sidebar como painel solto (cantos 14), lista e detalhe em painéis de superfície (cantos 12),
  blocos do Início em painéis.
- Testes: contraste e chaves iguais nos dois temas (`DesignSystemGuardTests`) e prints das páginas principais nos dois
  temas (`theme-light-*.png`, `theme-dark-*.png`).
