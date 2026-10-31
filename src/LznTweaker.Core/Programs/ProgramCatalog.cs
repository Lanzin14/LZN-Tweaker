namespace LznTweaker.Core.Programs;

/// <summary>
/// Programas recomendados pelo LZN Tweaker. Todos são opcionais e de terceiros: o app só mostra a página oficial
/// (nunca o arquivo de instalação) e não baixa, instala nem executa nenhum deles. Links conferidos em 30/10/2026.
/// Os valores de referência indicam a origem: documentação oficial, prática recorrente da comunidade ou experimento.
/// Texto curto de propósito: o que faz (uma ou duas frases), tutorial resumido e cuidados, que já incluem as limitações.
/// </summary>
public static class ProgramCatalog
{
    public static IReadOnlyList<ExternalProgram> All { get; } =
    [
        new("islc", "ISLC — Intelligent Standby List Cleaner", "Wagnardsoft",
            "Limpa a memória em espera (Standby List) do Windows quando ela cresce demais e a memória livre fica baixa. Pode reduzir travadas em PCs com pouca RAM livre.",
            "Investigar e contornar casos específicos de pressão de memória que causam stuttering.",
            [ProgramTag.Memory, ProgramTag.Latency],
            ProgramSafety.ControlledChange, ProgramLicense.Unconfirmed,
            "O site não declara a licença; é distribuído sem custo, com pedido de doação.",
            "https://www.wagnardsoft.com/content/intelligent-standby-list-cleaner-v1000-released",
            null,
            [
                new("The list size is at least", "1024 MB", EvidenceLevel.Community, "Referência para 16 GB de RAM; confira o nome exato da opção no próprio app."),
                new("Free memory is lower than", "2048 MB", EvidenceLevel.Community, "Para 16 GB; a ideia é limpar só quando a memória livre estiver realmente baixa."),
                new("Desired timer resolution", "0,50 ms", EvidenceLevel.Experimental, "Desde o Windows 10 2004, a resolução vale por processo; veja o card Timer Resolution."),
                new("Polling rate", "1000 ms para começar", EvidenceLevel.Community, "Valor padrão citado no fórum oficial."),
            ],
            [
                "Abra o ISLC e preencha os dois limites (veja os valores abaixo).",
                "Clique em Start e jogue normalmente.",
                "Compare o 1% low com e sem o ISLC; se não melhorar, desative.",
            ],
            [
                "Com RAM suficiente não há ganho: o Windows já reutiliza essa memória sozinho.",
                "Evite limpar o tempo todo e volte às configurações anteriores se piorar.",
            ],
            [
                new("Página oficial do ISLC (Wagnardsoft)", "https://www.wagnardsoft.com/content/intelligent-standby-list-cleaner-v1000-released", EvidenceLevel.Documented),
                new("Fórum oficial: casos em que a limpeza não ocorreu", "https://www.wagnardsoft.com/forums/viewtopic.php?p=9931", EvidenceLevel.Community),
            ]),

        new("process-lasso", "Process Lasso", "Bitsum",
            "Controla prioridade e núcleos de cada programa e cria regras automáticas por jogo. Ajuda quando programas em segundo plano disputam a CPU com o jogo.",
            "Reduzir a interferência de programas em segundo plano e automatizar ajustes por jogo.",
            [ProgramTag.Cpu],
            ProgramSafety.ControlledChange, ProgramLicense.FreeWithPaidEdition,
            "Versão gratuita com ProBalance, Performance Mode, prioridades e afinidades; a Pro (licença vitalícia) libera CPU Limiter e outros recursos.",
            "https://bitsum.com/",
            "process-lasso.png",
            [
                new("ProBalance", "Ativado", EvidenceLevel.Documented),
                new("Performance Mode", "Ativado durante jogos", EvidenceLevel.Documented, "A Bitsum não recomenda para tarefas longas em segundo plano nem na bateria."),
                new("Bitsum Highest Performance", "Testar durante jogos", EvidenceLevel.Documented, "Plano de energia usado pelo Performance Mode."),
                new("Detecção automática de jogos", "Ativado", EvidenceLevel.Documented, "Vem desligada por padrão."),
                new("Prioridade de CPU", "Normal inicialmente", EvidenceLevel.Documented, "A Bitsum desaconselha High e Realtime."),
                new("Prioridade de E/S", "Normal inicialmente", EvidenceLevel.Community),
                new("Afinidade de CPU", "Padrão inicialmente", EvidenceLevel.Community),
                new("CPU Sets", "Testar individualmente", EvidenceLevel.Experimental),
                new("CPU Limiter", "Não aplicar ao jogo", EvidenceLevel.Community, "Limita o uso de CPU; no jogo só derruba o desempenho."),
            ],
            [
                "Deixe o ProBalance ligado (vem ativado).",
                "Ative a detecção de jogos para ligar o Performance Mode só com o jogo aberto.",
                "Para ajustar um jogo, clique com o botão direito no processo e crie a regra só para ele.",
            ],
            [
                "Não aumenta o FPS sozinho; regras mal configuradas pioram o desempenho.",
                "Nunca use prioridade Realtime e mude uma coisa por vez, medindo.",
            ],
            [
                new("Process Lasso — versão gratuita vs. Pro", "https://bitsum.com/process-lasso-free-vs-pro/", EvidenceLevel.Documented),
                new("Como o ProBalance funciona", "https://bitsum.com/how-probalance-works/", EvidenceLevel.Documented),
                new("Performance Mode e detecção de jogos", "https://bitsum.com/gaming-mode/", EvidenceLevel.Documented),
            ]),

        new("timer-resolution", "Timer Resolution", "Microsoft (API do Windows) · ferramentas de terceiros",
            "Consulta e pede uma resolução menor para os temporizadores do Windows, o que pode deixar o frametime mais regular em alguns PCs.",
            "Experimentar e diagnosticar a temporização de jogos e aplicativos.",
            [ProgramTag.Latency, ProgramTag.Diagnostics],
            ProgramSafety.ControlledChange, ProgramLicense.Unconfirmed,
            "A API é parte do Windows. Ferramentas: valleyofdoom/TimerResolution (código aberto, GPL-3.0) e TimerResolution de Lucas Hale (versão gratuita e Pro).",
            "https://learn.microsoft.com/en-us/windows/win32/api/timeapi/nf-timeapi-timebeginperiod",
            null,
            [
                new("Padrão do Windows", "Depende do sistema", EvidenceLevel.Documented),
                new("Solicitação experimental", "0,50 ms", EvidenceLevel.Experimental, "A resolução efetiva pode ser diferente da solicitada; meça com o MeasureSleep."),
                new("Alternativa de teste", "Comportamento padrão", EvidenceLevel.Documented),
            ],
            [
                "Abra a ferramenta e veja a resolução atual.",
                "Peça 0,50 ms e confira a resolução efetiva (pode ser diferente da pedida).",
                "Teste o jogo com e sem a alteração; feche a ferramenta para voltar ao padrão.",
            ],
            [
                "Desde o Windows 10 2004 vale por programa, e no Windows 11 janelas minimizadas podem ignorar o pedido.",
                "Pode aumentar o consumo de energia; não há consenso sobre ganho real.",
            ],
            [
                new("timeBeginPeriod — documentação da Microsoft", "https://learn.microsoft.com/en-us/windows/win32/api/timeapi/nf-timeapi-timebeginperiod", EvidenceLevel.Documented),
                new("valleyofdoom/TimerResolution (código aberto)", "https://github.com/valleyofdoom/TimerResolution", EvidenceLevel.Experimental),
                new("TimerResolution — Lucas Hale", "https://www.lucashale.com/timerresolution/", EvidenceLevel.Community),
                new("Blur Busters: debate sobre efeito real", "https://forums.blurbusters.com/viewtopic.php?p=108220", EvidenceLevel.Community),
            ]),

        new("msi-utility", "MSI Utility v3", "Comunidade Guru3D",
            "Mostra e muda o modo de interrupção (MSI) de placas como GPU e rede. Em dispositivos compatíveis, pode reduzir conflitos de interrupção.",
            "Diagnóstico e gerenciamento avançado de interrupções de hardware.",
            [ProgramTag.Latency],
            ProgramSafety.Advanced, ProgramLicense.Unconfirmed,
            "Distribuída em um tópico do fórum Guru3D; licença e autoria não puderam ser confirmadas. Não há repositório oficial no GitHub: cópias lá não são oficiais.",
            "https://forums.guru3d.com/threads/windows-line-based-vs-message-signaled-based-interrupts-msi-tool.378044/",
            null,
            [
                new("GPU", "Verificar suporte e testar MSI", EvidenceLevel.Community),
                new("Placa de rede", "Verificar suporte e testar individualmente", EvidenceLevel.Community),
                new("NVMe", "Manter o padrão", EvidenceLevel.Community, "Controladores NVMe costumam usar vários MSI por padrão."),
                new("Controladores USB", "Não modificar sem diagnóstico", EvidenceLevel.Community),
                new("Áudio", "Alterar só com justificativa técnica", EvidenceLevel.Experimental),
                new("Prioridade de interrupção", "Não definir High indiscriminadamente", EvidenceLevel.Experimental, "Sem fonte técnica verificada sobre benefício."),
            ],
            [
                "Crie um ponto de restauração e anote o estado de cada dispositivo.",
                "Abra o utilitário como administrador e veja quais dispositivos suportam MSI.",
                "Marque MSI em um dispositivo por vez, clique em Apply e reinicie.",
            ],
            [
                "Alguns dispositivos ficam instáveis em MSI; se um parar, volte ao modo anterior e reinicie.",
                "Não altere em lote e baixe só do tópico original do Guru3D.",
            ],
            [
                new("Tópico original no fórum Guru3D", "https://forums.guru3d.com/threads/windows-line-based-vs-message-signaled-based-interrupts-msi-tool.378044/", EvidenceLevel.Community),
            ]),

        new("latencymon", "LatencyMon", "Resplendence Software Projects",
            "Mede a latência de DPC e ISR do Windows e mostra quais drivers causam picos. Ajuda a achar a origem de travadas e falhas de áudio.",
            "Investigar stuttering, falhas de áudio e possíveis causas de latência do sistema.",
            [ProgramTag.Latency, ProgramTag.Diagnostics],
            ProgramSafety.ReadOnly, ProgramLicense.FreeWithPaidEdition,
            "Home Edition gratuita; Professional Edition paga.",
            "https://www.resplendence.com/latencymon",
            "latencymon.png",
            [
                new("Tempo de DPC/ISR até 2000 µs", "Adequado para áudio em tempo real", EvidenceLevel.Documented, "A própria Resplendence chama esses limites de arbitrários."),
                new("Entre 2000 e 4000 µs", "Duvidoso", EvidenceLevel.Documented),
                new("Acima de 4000 µs", "Inadequado", EvidenceLevel.Documented),
            ],
            [
                "Feche outros programas e clique em Start (play).",
                "Deixe rodar 10 a 15 minutos parado e repita durante uma partida.",
                "Na aba Drivers, ordene pelo maior tempo e veja quem aparece no topo.",
            ],
            [
                "Um resultado bom não garante input lag baixo nos jogos.",
                "Não conclua nada com um teste curto nem desative drivers por um pico isolado.",
            ],
            [
                new("Como usar o LatencyMon (Resplendence)", "https://www.resplendence.com/latencymon_using", EvidenceLevel.Documented),
                new("Latência de interrupção até o processo", "https://resplendence.com/latencymon_interrupt2process", EvidenceLevel.Documented),
                new("Perguntas frequentes", "https://www.resplendence.com/latencymon_faq", EvidenceLevel.Documented),
            ]),

        new("capframex", "CapFrameX", "CXWorld",
            "Grava e compara FPS médio, 1% low e frametime. Serve para saber se um ajuste realmente ajudou.",
            "Medir se os ajustes realmente melhoraram o desempenho nos jogos.",
            [ProgramTag.Benchmark],
            ProgramSafety.ReadOnly, ProgramLicense.OpenSource,
            "Código aberto (licença MIT).",
            "https://www.capframex.com/",
            "capframex.png",
            [
                new("Average FPS", "Desempenho médio", EvidenceLevel.Documented),
                new("1% low", "Consistência dos quadros", EvidenceLevel.Documented, "O CapFrameX explica a diferença entre percentil e \"x% low\"."),
                new("0,1% low", "Quedas mais acentuadas", EvidenceLevel.Documented),
                new("Frametime", "Regularidade dos quadros", EvidenceLevel.Documented),
                new("Variação do frametime", "Oscilação do tempo de renderização", EvidenceLevel.Documented),
            ],
            [
                "Faça três capturas com a configuração atual.",
                "Mude uma única coisa e faça mais três capturas na mesma cena.",
                "Compare as médias e mantenha só o que melhorar de forma consistente.",
            ],
            [
                "Compare sempre na mesma cena, resolução e configuração.",
                "Feche overlays e gravações durante as capturas.",
            ],
            [
                new("CapFrameX: explicação das métricas", "https://www.capframex.com/blog/post/Explanation%20of%20different%20performance%20metrics", EvidenceLevel.Documented),
                new("Repositório oficial no GitHub", "https://github.com/CXWorld/CapFrameX", EvidenceLevel.Documented),
            ]),

        new("autoruns", "Autoruns", "Microsoft Sysinternals",
            "Mostra tudo o que abre com o Windows: programas, serviços, tarefas agendadas e drivers, inclusive o que a página Inicialização não alcança.",
            "Investigar e controlar o que é carregado junto com o Windows.",
            [ProgramTag.Startup, ProgramTag.Diagnostics],
            ProgramSafety.ControlledChange, ProgramLicense.Free,
            "Ferramenta gratuita da Microsoft.",
            "https://learn.microsoft.com/en-us/sysinternals/downloads/autoruns",
            null,
            [],
            [
                "Ative Options > Hide Signed Microsoft Entries para ver só o que é de terceiros.",
                "Desmarque a caixa do item que quer desativar (não use Delete).",
                "Reinicie e confira se tudo funciona; para voltar, marque de novo.",
            ],
            [
                "Desativar a entrada errada pode quebrar drivers, rede, áudio ou segurança.",
                "Crie um ponto de restauração antes de mudanças importantes.",
            ],
            [
                new("Autoruns — Microsoft Learn", "https://learn.microsoft.com/en-us/sysinternals/downloads/autoruns", EvidenceLevel.Documented),
            ]),

        new("rammap", "RAMMap", "Microsoft Sysinternals",
            "Mostra em detalhe como a RAM está sendo usada (em uso, em espera, modificada e cache). Ajuda a decidir se vale usar uma ferramenta de limpeza.",
            "Diagnóstico avançado do consumo e da distribuição da RAM.",
            [ProgramTag.Memory, ProgramTag.Diagnostics],
            ProgramSafety.ReadOnly, ProgramLicense.Free,
            "Ferramenta gratuita da Microsoft.",
            "https://learn.microsoft.com/en-us/sysinternals/downloads/rammap",
            null,
            [],
            [
                "Abra como administrador e vá na aba Use Counts.",
                "Compare a memória em espera (Standby) com a livre durante o jogo.",
                "Use as abas de processos e arquivos para ver quem consome mais.",
            ],
            [
                "É para diagnóstico: limpar sem necessidade (menu Empty) descarta cache útil.",
                "Esvaziar a memória modificada grava dados em disco; salve seu trabalho antes.",
            ],
            [
                new("RAMMap — Microsoft Learn", "https://learn.microsoft.com/en-us/sysinternals/downloads/rammap", EvidenceLevel.Documented),
                new("MiniTool: como usar o RAMMap", "https://www.minitool.com/news/free-up-ram-with-rammap.html", EvidenceLevel.Community),
            ]),
    ];
}
