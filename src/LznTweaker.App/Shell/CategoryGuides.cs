namespace LznTweaker.Shell;

/// <summary>
/// Explicação de uma página: <see cref="Summary"/> é a frase curta sob o título ("o que é"); os demais campos ficam
/// em "Saiba mais". Os textos descrevem o que o código faz hoje, sem prometer ganhos.
/// </summary>
public sealed record CategoryGuide(string Summary, string WhatYouCanDo, string Benefit, string? Care);

/// <summary>Textos de todas as páginas, num só lugar para manter a mesma linguagem.</summary>
public static class CategoryGuides
{
    public static CategoryGuide Backups { get; } = new(
        "Histórico de tudo o que o LZN Tweaker alterou no seu PC, com o valor que existia antes.",
        "Pesquisar e filtrar as operações, desfazer ajustes um a um ou vários de uma vez, e abrir a página de origem de apps removidos, pastas da Steam e perfis do Deadlock.",
        "Você sabe exatamente o que mudou e consegue voltar atrás sem precisar lembrar o valor original.",
        "Só dá para desfazer o que tem valor anterior registrado. Ações pontuais, como limpeza de arquivos, não têm volta por aqui; para o sistema inteiro, use a Restauração do Sistema em Fixes.");

    public static CategoryGuide Fixes { get; } = new(
        "Ferramentas do próprio Windows para verificar e reparar arquivos do sistema e o disco.",
        "Rodar SFC, DISM e CHKDSK em uma janela visível e abrir os solucionadores de problemas e a Restauração do Sistema.",
        "Corrigir erros causados por arquivos corrompidos sem reinstalar o Windows.",
        "As verificações podem levar de minutos a mais de meia hora. Não feche a janela nem desligue o PC no meio.");

    public static CategoryGuide General { get; } = new(
        "Ajustes do Windows ligados ao desempenho em jogos, à privacidade e ao uso do dia a dia.",
        "Ligar ou desligar cada ajuste, revisar a lista do que vai mudar e aplicar tudo de uma vez.",
        "Um Windows com menos tarefas em segundo plano, menos coleta de dados e menos interrupções.",
        "Nada é alterado ao mover um interruptor: as mudanças ficam pendentes até você revisar e aplicar. Antes de aplicar, o app cria um ponto de restauração.");

    public static CategoryGuide Hardware { get; } = new(
        "Os componentes do seu PC, como o Windows informa: processador, placa de vídeo, memória, disco e placa-mãe.",
        "Conferir modelos, uso atual e orientações de diagnóstico para RAM, BIOS e drivers.",
        "Saber o que você tem antes de mexer em configurações e identificar gargalos conhecidos.",
        "É só leitura. O app não altera BIOS, XMP, overclock ou undervolt, e não lê temperaturas.");

    public static CategoryGuide Gpu { get; } = new(
        "Central de configuração da placa de vídeo NVIDIA ou AMD para jogos competitivos.",
        "Ver as recomendações do driver por modo, os perfis gráficos de CS2, VALORANT, Overwatch 2 e Deadlock, e orientações de monitor e latência.",
        "Configurar o driver e os jogos com critério, sabendo o efeito de cada opção.",
        "São recomendações: você aplica no driver ou no menu do jogo. O app ainda não altera essas configurações sozinho.");

    public static CategoryGuide Debloat { get; } = new(
        "Remoção de aplicativos que vêm instalados com o Windows e que você não usa.",
        "Escolher os apps, revisar a lista e remover em lote; depois, restaurar o que foi removido pelo app.",
        "Menos apps abrindo em segundo plano e menos itens no menu Iniciar.",
        "Componentes do sistema ficam protegidos e não aparecem. Removendo \"para todos os usuários\", os arquivos são apagados e a volta só é possível pela Microsoft Store.");

    public static CategoryGuide Network { get; } = new(
        "Testes, ajustes e reparos da conexão de rede.",
        "Testar a conexão, ver a configuração da placa de rede, limpar o cache de DNS, ajustar o IPv6 e redefinir o Winsock.",
        "Encontrar e corrigir problemas de conexão. Esses ajustes não diminuem o ping do servidor do jogo.",
        "Desativar o IPv6 pode atrapalhar redes que dependem dele. Redefinir o Winsock exige reiniciar o PC e pode exigir reinstalar VPNs.");

    public static CategoryGuide Advanced { get; } = new(
        "A lista completa de ajustes em formato de tabela, com perfis prontos para selecionar vários de uma vez.",
        "Usar os perfis Mínimo, Standard e Avançado, marcar itens um a um, executar a seleção e desfazer ajustes aplicados.",
        "Controle fino para quem já sabe o que quer mudar.",
        "Os itens da seção Advanced Tweaks têm risco maior. Escolher um perfil só marca as opções; nada é executado até você confirmar.");

    public static CategoryGuide Games { get; } = new(
        "Otimizações separadas por jogo, além das opções que valem para o Windows e para a Steam.",
        "Ver o que foi detectado em cada jogo, limpar caches de shaders, guardar com backup e restaurar as pastas da Steam de cada jogo e seguir orientações para Windows e Steam. No Deadlock, também aplicar perfis do gameinfo.gi.",
        "Cuidar de cada jogo sem misturar configurações de um com o outro.",
        "O app não edita arquivos de configuração pessoais dos jogos e nunca toca no Vanguard do VALORANT.");

    public static CategoryGuide Cleanup { get; } = new(
        "Limpeza de arquivos temporários, caches e restos que o Windows e os programas deixam para trás.",
        "Analisar cada local, ver o espaço que pode ser liberado e escolher o que apagar.",
        "Liberar espaço em disco, útil principalmente quando o disco do sistema está quase cheio.",
        "A limpeza não tem desfazer. Apagar caches de shaders pode causar travadas na primeira partida, enquanto o jogo os recria, e esvaziar a Lixeira é definitivo.");

    public static CategoryGuide Startup { get; } = new(
        "Os programas que abrem sozinhos quando o Windows inicia.",
        "Ver o impacto estimado de cada programa, desativar ou reativar a abertura automática e desativar os launchers de jogos de uma vez.",
        "O Windows fica pronto para uso mais rápido e com menos programas consumindo memória.",
        "Desativar só impede a abertura automática; o programa continua instalado. Evite desativar drivers e o antivírus.");
}
