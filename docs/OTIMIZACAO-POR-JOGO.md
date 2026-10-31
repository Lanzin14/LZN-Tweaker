# Otimização por jogo — classificação da transcrição

Fonte: transcrição do vídeo "a melhor configuração pro CS2" fornecida em 29/10/2026. Cada item foi conferido contra a versão atual do Windows/Steam e contra o que existe neste PC (leitura em 29/10/2026).

**Tipo de evidência:** `Oficial` = documentação da Microsoft/Valve/fabricante · `Consenso` = comportamento conhecido e reproduzível · `Autor` = opinião ou experiência do autor do vídeo, sem documentação · `Sem evidência` = o próprio autor diz que não há documentação/teste.

**Decisão:** `Automático` = o app executa com seleção, confirmação, registro e reversão · `Diagnóstico` = o app detecta e mostra o estado, sem alterar · `Manual` = orientação com passos exatos · `Recusado` = não implementado (motivo na linha) · `Pendente` = próxima etapa.

## A. Windows (afeta todos os jogos)

| # | Item do vídeo | Evidência | Risco | Decisão | Observação |
|---|---|---|---|---|---|
| A1 | Apagar cache de shaders do DirectX (`%LOCALAPPDATA%\D3DSCache`) | Consenso | Baixo | **Automático** | Mostra locais e tamanho; aviso de recompilação e travadas temporárias. Sem backup (cache se regenera). |
| A2 | Apagar cache de shaders NVIDIA (`Local\NVIDIA\DXCache/GLCache`, `LocalLow\NVIDIA\PerDriverVersion\*`) e AMD (`Local\AMD\DxCache/DxcCache/VkCache/GLCache`) | Consenso | Baixo | **Automático** | Só aparecem os locais que existem. Neste PC: AMD (~4,6 GB). |
| A3 | SSD com 15–20% livre | Consenso (o motivo dado no vídeo está tecnicamente errado: não há "células que precisam ficar energizadas"; o espaço livre ajuda o cache SLC e a coleta de lixo) | — | **Diagnóstico** | Alerta abaixo de 15% no volume do jogo. |
| A4 | Otimizar unidade (TRIM) | Oficial | Baixo | **Diagnóstico** + Manual | Detecta se o TRIM está ativo (`fsutil behavior query DisableDeleteNotify`). O Windows já faz o ReTrim semanal; executar manualmente é opcional. |
| A5 | Jogo instalado no SSD | Consenso | — | **Diagnóstico** | Tipo de mídia da unidade do jogo. |
| A6 | Apagar `HKLM\SOFTWARE\Microsoft\Windows\Dwm\OverlayTestMode` (reativa MPO) | Oficial (é o workaround documentado de NVIDIA/Microsoft para *desativar* MPO) | Médio | **Diagnóstico** + Manual | Detecta se o MPO está desativado por essa chave. Não removo automaticamente: quem desativou pode ter feito isso para corrigir cintilação. |
| A7 | BCD: `useplatformclock`, `useplatformtick`, `disabledynamictick` | Oficial (são sobreposições de temporizador; o padrão do Windows é não ter nenhuma) | Alto | **Diagnóstico** + Manual | Lista o que está definido e mostra o comando `bcdedit /deletevalue` exato. Não aplico BCD automaticamente (pedido explícito do projeto). |
| A8 | Plano de energia (evitar Equilibrado) | Autor (AMD/Intel recomendam Equilibrado com driver atualizado em muitos CPUs) | Baixo | **Diagnóstico** | Mostra o plano ativo; aponta os tweaks existentes de Ultimate Performance, com aviso para notebook. |
| A9 | Desligar Inicialização Rápida | Consenso | Baixo | **Diagnóstico** | Detecta `HiberbootEnabled`. O tweak "Desativar Hibernação" já existente também desliga a Inicialização Rápida. |
| A10 | Game Bar: desativar capturas | Oficial | Baixo | **Diagnóstico** | Reusa o tweak existente "Desativar Game DVR" (sem duplicar). |
| A11 | Manter Game Bar e Modo de Jogo ativos | Autor | — | **Diagnóstico** | Mostra o estado; não altera. |
| A12 | Agendamento de GPU acelerado (HAGS) desligado | Autor; Oficial: geração de quadros DLSS 3 exige HAGS | Médio | **Diagnóstico** | Recomendação condicional: só desligue se não usa geração de quadros. |
| A13 | Preferência de GPU "Alto desempenho" por jogo | Oficial | Baixo | **Diagnóstico** | Lê `UserGpuPreferences` para cada exe detectado. Aplicar por jogo: **Pendente**. |
| A14 | "Otimizações para jogos em janela" ligado e "taxa de atualização variável" desligado | Autor (sem evidência de conflito; o VRR do Windows só afeta jogos sem VRR nativo) | Baixo | **Diagnóstico** | Mostra o estado de `DirectXUserGlobalSettings`. |
| A15 | Desligar overlays (NVIDIA App, Adrenalin, Discord) | Consenso (overlays podem quebrar o independent flip) | Baixo | **Manual** | Não há configuração confiável por arquivo/registro. |
| A16 | Aceleração de hardware do Discord/Chrome desligada | Autor, condicional ("só se fica aberto durante o jogo") | Baixo | **Manual** | Condição explicada. |
| A17 | Apps em segundo plano do Chrome | Consenso | Baixo | **Manual** | |
| A18 | Apps na inicialização (RGB, monitoramento) | Consenso | Baixo | **Diagnóstico** | Conta os itens ativos e aponta a página Inicialização (sem duplicar). |
| A19 | Polling rate do mouse 2K–4K | Autor | — | **Manual** | Sem valor universal; depende do mouse. |

## B. Steam (compartilhado; vale por jogo quando indicado)

| # | Item | Evidência | Decisão | Observação |
|---|---|---|---|---|
| B1 | `steamapps\shadercache\<AppID>` renomeado como backup | Consenso | **Automático** (por jogo) | Renomeia para `<AppID>.lzntweaker-backup-<data>` (nada é apagado); restauração documentada. Exige Steam e jogo fechados. Disponível para CS2, Deadlock e Overwatch 2 quando a pasta existe. |
| B2 | Overlay da Steam (global ou por jogo) | Oficial | **Manual** | A Steam regrava os próprios arquivos de configuração; editar `localconfig.vdf` com a Steam fechada é frágil e não tem reversão confiável. |
| B3 | Gravação de jogos da Steam | Oficial | **Manual** | Idem. |
| B4 | Downloads durante o jogo | Oficial | **Manual** | Explica o efeito colateral (atualizações do próprio jogo pausam). |
| B5 | Pré-cache de shaders (Vulkan) | Oficial | **Manual** | |
| B6 | Avatares animados | Autor | **Manual** | |
| B7 | Steam Cloud ao redefinir configurações | Oficial | **Manual** guiado | O app não desliga a Cloud silenciosamente; mostra o passo a passo e o aviso de conflito ("manter local"). |

## C. CS2 (App ID 730)

| # | Item | Evidência | Decisão | Observação |
|---|---|---|---|---|
| C1 | Cache de shaders da Steam do CS2 | Consenso | **Automático** | Usa B1 com App ID 730. |
| C2 | Redefinir `userdata\<conta>\730` | Consenso | **Automático** com backup | Detecta as contas pela `loginusers.vdf`; renomeia para backup (binds preservados no backup); aviso de perda de binds; orientação da Steam Cloud (B7). Restauração devolve a pasta original e guarda a recriada. |
| C3 | "Desabilitar otimizações de tela inteira" desmarcado no `cs2.exe` | Oficial (`AppCompatFlags\Layers`) | **Diagnóstico** | Detecta a flag `DISABLEDXMAXIMIZEDWINDOWEDMODE`. Remover: **Pendente** (precisa de operação de remoção rastreada no motor). |
| C4 | Gráficos: tudo baixo + sombras dinâmicas "Todas", FidelityFX desligado, filtragem de texturas maior, MSAA 2x, qualidade de sombras baixa | Autor | **Manual** | Preferência e hardware; inclui o teste do autor para saber se a GPU limita (trocar para a menor resolução e comparar). |
| C5 | FPS máximo 0 | Autor | **Manual** | Condicional. |
| C6 | NVIDIA Reflex só quando a GPU bate 100% | Autor | **Manual** | Condicional. |
| C7 | Buffer de pacotes / largura de banda 2,5–3 Mbps | **Sem evidência** (o autor diz que não há documentação nem teste) | **Manual**, marcado como experimento | "Teste e compare"; nunca aplicado como ideal. |
| C8 | Pressionar para falar simplificado | Autor (relato de travadas antigas) | **Manual** | Só para quem usa push-to-talk. |
| C9 | Tela cheia; resolução nativa; resolução do jogo = do sistema | Consenso | **Manual** | |
| C10 | Opção de inicialização `-noreflex` + baixa latência Ultra (NVIDIA) | Autor | **Manual** | Explica a dependência entre as opções. |
| C11 | Profile Inspector / registro AMD `Main3D`/`Main3D_DEF` | **Sem evidência** | **Recusado** | Alteração não documentada de driver; o projeto proíbe aplicar só porque aparece no vídeo. |
| C12 | Resolução personalizada / CRU / EDID | Autor | **Recusado** (automação) · Manual | Mexe na saída de vídeo; risco de tela preta sem reversão confiável pelo app. |
| C13 | Modo de apresentação (PresentMon: Hardware Independent Flip) | Oficial (Intel PresentMon) | **Manual** | Link oficial; sem download automático. |
| C14 | Power limit da GPU (Afterburner) / undervolt | Consenso | **Manual** | Undervolt não é automatizado. |
| C15 | Taxa de atualização no monitor/OSD/site de teste | Consenso | **Manual** | |

## D. VALORANT

Não instalado neste PC (sem metadados do Riot Client). Nada do CS2 é reaproveitado: nem App ID 730, nem caminhos Steam, nem convars Source 2, nem opções de rede do CS2.

| Item | Decisão |
|---|---|
| Cache de shaders do Windows/GPU (A1–A2) | Aplica-se (compartilhado). |
| Preferência de GPU e otimizações de tela inteira no executável | **Diagnóstico** quando o jogo for detectado (`VALORANT-Win64-Shipping.exe`). |
| Arquivos `.ini` do jogo | **Recusado**: a Riot sincroniza as configurações na conta; edição manual de `.ini` não é ganho comprovado. |
| Vanguard | **Nunca alterado.** O app não desativa, modifica nem contorna. |
| Configurações internas (Reflex, limite de FPS, MSAA) | **Manual**, condicional ao hardware. |

## E. Overwatch 2 (Steam, App ID 2357570)

| Item | Decisão |
|---|---|
| Cache de shaders da Steam (B1) | **Automático** com App ID 2357570, quando a pasta existe. |
| `Documents\Overwatch\Settings\Settings_v0.ini` | **Recusado** (edição): contém configurações pessoais; nunca alterado. |
| Diagnóstico do `Overwatch.exe` (GPU, tela inteira) | **Diagnóstico**. |
| Configurações internas (Reduce Buffering, Reflex, limite de FPS) | **Manual**. |

## F. Deadlock

A página do `gameinfo.gi` continua como está. Do vídeo, só entram itens compartilhados: cache de shaders da Steam (App ID 1422450) e diagnóstico do `deadlock.exe`. Nenhum item do CS2 (userdata 730, convars de rede) é aplicado.

## G. Hardware — somente diagnóstico e orientação

| Item | Decisão |
|---|---|
| RAM em dual channel / slots | **Diagnóstico** (quantidade e posição dos módulos via WMI) + Manual (CPU-Z). |
| XMP/EXPO, frequência real | **Diagnóstico** (frequência configurada) + Manual. O app nunca altera BIOS. |
| Proporção 1:1 MCLK/UCLK (Ryzen) | **Manual** (ZenTimings). |
| Versão/data do BIOS | **Diagnóstico** (`Win32_BIOS`) + Manual (site do fabricante). |
| Driver de chipset | **Manual** (AMD/Intel oficiais). |
| ReBAR/SAM | **Manual** (GPU-Z); condicional ao gargalo; o autor sugere desligar no CS2 (opinião, "teria que testar"). |
| Clock efetivo, temperatura, limites | **Manual** (HWiNFO + Cinebench). |
| LatencyMon | **Manual**, com o alerta do autor: o driver apontado nem sempre é o culpado. |
| Mouse | **Manual** (A19). |

## H. Fortnite (Epic Games Launcher)

Detecção somente leitura pela lista do launcher (`ProgramData\Epic\UnrealEngineLauncher\LauncherInstalled.dat`,
`AppName = "Fortnite"`). Tudo o mais é **Manual**: o Fortnite não estava instalado para validar um editor do
`GameUserSettings.ini` e a Epic orienta mudar as opções no menu do jogo. Dados em
`src/LznTweaker.Core/GameOptimization/FortniteRecommendations.cs`; cada linha tem origem Oficial, Técnica, Comunidade
ou Experimental, mostrada na página.

| Item | Decisão |
|---|---|
| Perfil competitivo (Modo Desempenho) | **Manual**. Nomes e valores do artigo da Epic "melhores configurações para jogo competitivo em 2026" (30/04/2026). |
| Perfil alternativo DirectX 12 | **Manual**, apresentado como alternativa a testar, nunca como superior. Relatos da comunidade são contraditórios. |
| Resolução 3D | 100%; reduzir em etapas de 10% só se faltar FPS (Epic). |
| NVIDIA (Painel de Controle, perfil do Fortnite) | **Manual**. Reflex Ligado + Boost no jogo; Modo de baixa latência Desligado (o Reflex tem prioridade, NVIDIA); energia em desempenho máximo só no perfil; V-Sync desligado com a nota de G-SYNC (comunidade); cache de shader padrão; filtragem "Alto desempenho" como experimental. |
| AMD (Adrenalin, perfil do jogo) | **Manual**. Anti-Lag ativado; Chill desativado (a AMD documenta que os dois não ficam ativos juntos); V-Sync a critério do app; FreeSync conforme o monitor. Anti-Lag 2 não é recomendado: exige suporte do jogo e o Fortnite não estava na lista da AMD em 31/10/2026. |
| Escolha NVIDIA/AMD | **Manual pelo usuário**. A página não detecta a placa nem a fabricante e não cria configurações a partir do hardware. |
| Argumentos `-d3d12 -FeatureLevelEs31` / `-d3d12 -sm6` | **Manual**, só quando o modo de renderização não fica salvo (ajuda da Epic), com o caminho no launcher e como remover. |
| Texturas de Alta Resolução | **Manual** (launcher > Opções); reversível marcando de novo. |
| Programas em segundo plano | **Manual**; aponta para a página Inicialização, que já existe. |
| Travadas, frametime, perda de pacotes | **Manual** (Exibir FPS, PresentMon, estatísticas de rede). Nenhuma promessa de FPS ou ping. |
| Prioridade Acima do normal | **Recusado**: sem recomendação oficial, o Windows já favorece a janela em primeiro plano e manter a prioridade exigiria um processo vigiando o jogo. Fica só a instrução manual pelo Gerenciador de Tarefas, que volta ao normal ao fechar o jogo. |
| Prioridade Tempo real, afinidade de CPU, serviços do Windows | **Recusado** sempre. |
| Easy Anti-Cheat / BattlEye | **Nunca tocados.** |

Fontes consultadas em 31/10/2026: artigo e páginas de ajuda da Epic (modo de desempenho, texturas de alta resolução,
argumentos quando o modo não fica salvo); ajudas da AMD sobre Radeon Anti-Lag, Anti-Lag 2 e Radeon Chill; artigo da
NVIDIA sobre Reflex e o Modo de baixa latência; discussões da comunidade (Reddit) sobre Modo Desempenho x DirectX 12.

## Ignorado

Publicidade do ExitLag e cupom; divulgação de curso. Não fazem parte de nenhuma otimização.
