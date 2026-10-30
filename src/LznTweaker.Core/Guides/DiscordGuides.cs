namespace LznTweaker.Core.Guides;

/// <summary>
/// Configurações do aplicativo do Discord para Windows. Os nomes seguem a central de ajuda do Discord e guias em
/// português (conferidos em outubro de 2026); quando o nome em português não foi confirmado, a nota traz o original
/// em inglês. O Discord muda menus com frequência, por isso cada card avisa o que pode variar.
/// </summary>
public static class DiscordGuides
{
    private const string OpenSettings = "Abra o Discord e clique na engrenagem (Configurações de usuário), ao lado do seu nome, no canto inferior esquerdo.";

    public static IReadOnlyList<ManualSettingGuide> All { get; } =
    [
        new("discord-data", "Uso de dados para melhoria e personalização",
            "Reduz os dados de uso que o Discord coleta para análises e para personalizar recomendações. É uma escolha de privacidade; não muda o desempenho.",
            ["Configurações de usuário", "Dados e privacidade"],
            GuideRecommendation.Disable,
            [OpenSettings,
             "Na lista à esquerda, abra Dados e privacidade (em versões antigas: Privacidade e segurança).",
             "Desative \"Usar dados para melhorar o Discord\".",
             "Desative \"Usar dados para personalizar minha experiência no Discord\"."],
            "Em inglês, a seção se chama Data & Privacy e as opções \"Use data to improve Discord\" e \"Use data to personalize my Discord experience\"; a tradução pode variar um pouco. Desligar interrompe só a coleta daqui em diante.",
            "https://support.discord.com/hc/articles/21864805694999"),
        new("discord-clip-voice", "Gravação da sua voz em clipes",
            "Impede que a sua voz entre nos clipes que outras pessoas gravam em uma chamada.",
            ["Configurações de usuário", "Clipes"],
            GuideRecommendation.Disable,
            [OpenSettings,
             "Na lista à esquerda, abra Clipes.",
             "No fim da página, desative a opção que permite gravar a sua voz em clipes."],
            "Em inglês, a opção se chama \"Allow my voice to be recorded in Clips\". Ela vale só para os Clipes do Discord e pode não aparecer se o recurso não estiver disponível na sua conta ou versão.",
            "https://support.discord.com/hc/articles/16861982215703"),
        new("discord-clips", "Clipes e notificações sem uso",
            "Desliga recursos que você não usa e reduz avisos que aparecem durante a partida.",
            ["Configurações de usuário", "Notificações", "Clipes"],
            GuideRecommendation.Disable,
            [OpenSettings,
             "Abra Notificações e desative os tipos de aviso que você não usa (por exemplo, notificações na área de trabalho e sons).",
             "Abra Clipes e, se você não grava clipes, desative a opção que ativa o recurso."],
            "Não confirmamos uma opção específica de clipes dentro de Notificações em todas as versões. Se ela não existir na sua, ajuste só a página Clipes. Desative apenas o que você não usa.",
            "https://support.discord.com/hc/articles/16861982215703"),
        new("discord-startup", "Abrir com o Windows",
            "O Discord deixa de abrir sozinho ao ligar o PC: a inicialização fica mais leve e o app não ocupa memória quando você não vai usá-lo.",
            ["Configurações de usuário", "Configurações do Windows"],
            GuideRecommendation.Disable,
            [OpenSettings,
             "Em Configurações do aplicativo, abra Configurações do Windows.",
             "Em Comportamento de inicialização do sistema, desative \"Abrir o Discord\".",
             "Opcional: desative \"Minimizar para a bandeja\" para que fechar a janela encerre o Discord de verdade."],
            "Não existe no Discord um interruptor para \"componentes auxiliares\"; esta seção tem só as opções de inicialização e de bandeja. Ela aparece apenas no aplicativo para Windows. Desative só se você não quiser o Discord aberto junto com o Windows.",
            "https://canaltech.com.br/apps/como-impedir-que-o-discord-inicie-com-o-windows-10/"),
        new("discord-hardware-acceleration", "Aceleração de hardware",
            "O Discord para de usar a placa de vídeo para desenhar a própria janela, deixando a GPU livre para o jogo. Em alguns PCs a interface fica menos fluida; se isso acontecer, ative de novo.",
            ["Configurações de usuário", "Avançado"],
            GuideRecommendation.Disable,
            [OpenSettings,
             "Em Configurações do aplicativo, abra Avançado.",
             "Desative \"Aceleração de hardware\".",
             "Confirme quando o Discord pedir para reiniciar (ou feche e abra de novo) para a mudança valer."],
            "O efeito varia de PC para PC. Em Voz e vídeo existe outra opção de aceleração de hardware, para o vídeo de transmissões: é diferente desta e pode ficar como está.",
            "https://support.discord.com/hc/community/posts/15208768791831-Problematic-Double-Use-of-Hardware-Acceleration"),
        new("discord-overlay", "Sobreposição de jogo (overlay)",
            "Remove a camada que o Discord desenha por cima do jogo: menos trabalho extra durante a partida e menos conflitos com programas de gravação e captura.",
            ["Configurações de usuário", "Sobreposição de jogo"],
            GuideRecommendation.Disable,
            [OpenSettings,
             "Em Configurações de atividade, abra Sobreposição de jogo (em versões antigas: Sobreposição).",
             "Desative \"Ativar sobreposição no jogo\".",
             "Se quiser manter a sobreposição em um jogo específico, ela também pode ser ligada ou desligada por jogo em Jogos registrados."],
            "O nome da seção mudou entre versões (Sobreposição, Sobreposição de jogo); a opção principal continua sendo a de ativar a sobreposição no jogo.",
            "https://support.discord.com/hc/articles/217659737"),
        new("discord-appearance", "Aceleração de hardware em Aparência",
            "Em versões antigas, a aceleração de hardware ficava em Aparência. Nas atuais ela está em Avançado: é a mesma opção, não precisa desativar duas vezes.",
            ["Configurações de usuário", "Aparência"],
            GuideRecommendation.Check,
            [OpenSettings,
             "Em Configurações do aplicativo, abra Aparência.",
             "Se aparecer \"Aceleração de hardware\" aqui, é a mesma opção do card Aceleração de hardware: deixe as duas no mesmo estado.",
             "Se não aparecer, não há nada a fazer nesta página."],
            "Não encontramos uma segunda aceleração de hardware em Aparência nas versões atuais; este card existe para evitar que a mesma configuração seja mudada duas vezes.",
            "https://support.discord.com/hc/community/posts/15208768791831-Problematic-Double-Use-of-Hardware-Acceleration")
    ];
}
