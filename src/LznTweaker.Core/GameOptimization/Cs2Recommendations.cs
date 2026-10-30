namespace LznTweaker.Core.GameOptimization;

/// <summary>Procedimento manual numerado (ex.: Gerenciador de Tarefas), com aviso quando o resultado não é garantido.</summary>
public sealed record ManualProcedure(string Title, IReadOnlyList<string> Steps, string? Warning);

/// <param name="Dependency">Quando a recomendação depende de hardware, driver ou preferência; nulo quando vale para todos.</param>
public sealed record InGameSetting(string Name, string Value, string Purpose, string? Dependency = null);

/// <summary>
/// "Configurações recomendadas" do CS2. São instruções manuais: o app não altera o processo do jogo, os arquivos do
/// CS2 (cs2_video.txt e afins) nem nada ligado ao anticheat. Nomes conferidos no menu atual (Video > Advanced Video).
/// </summary>
public static class Cs2Recommendations
{
    public const string VideoMenuPath = "Settings (engrenagem) > Video > Advanced Video";

    public static IReadOnlyList<ManualProcedure> TaskManager { get; } =
    [
        new("Definir afinidade do processador",
            [
                "Abra o CS2 e entre no jogo.",
                "Pressione Ctrl + Shift + Esc para abrir o Gerenciador de Tarefas.",
                "Acesse a aba Detalhes.",
                "Localize cs2.exe.",
                "Clique com o botão direito e selecione Definir afinidade.",
                "Desmarque CPU 0, mantendo os demais processadores selecionados.",
                "Confirme a alteração.",
            ],
            "Ajuste experimental: desmarcar o CPU 0 não garante mais FPS e pode piorar o desempenho em algumas configurações. " +
            "Compare antes e depois e volte a marcar todos os processadores se houver travamentos, stuttering ou perda de desempenho. " +
            "A alteração costuma se perder ao fechar o jogo."),
        new("Definir prioridade do processo",
            [
                "No Gerenciador de Tarefas, ainda na aba Detalhes, localize cs2.exe.",
                "Clique com o botão direito e selecione Definir prioridade.",
                "Selecione Alta e confirme.",
            ],
            "Opcional: teste antes de manter. Não use Tempo real, que pode travar o Windows e outros programas. " +
            "Se notar problemas, volte para Normal. A prioridade também volta ao padrão quando o jogo é fechado."),
    ];

    public static IReadOnlyList<InGameSetting> Video { get; } =
    [
        new("Boost Player Contrast", "Enabled", "Destaca os jogadores no cenário, facilitando enxergar inimigos."),
        new("Wait for Vertical Sync", "Disabled", "Evita a latência extra do V-Sync e não trava o FPS na taxa do monitor."),
        new("AMD Anti-Lag 2", "Enabled", "Reduz a latência de entrada quando o jogo e o driver oferecem a opção.",
            "Só com GPU Radeon compatível e driver com suporte (Adrenalin 24.6.1 ou mais novo). A opção não aparece em GPUs NVIDIA."),
        new("NVIDIA Reflex Low Latency", "Enabled", "Equivalente da NVIDIA para reduzir a latência de entrada.",
            "Só com GPU NVIDIA. Enabled + Boost é opção de teste e aumenta o consumo."),
        new("Dynamic Shadows", "All", "Mostra as sombras de todas as fontes de luz, inclusive as que revelam jogadores."),
        new("High Dynamic Range", "Performance", "Usa o modo mais leve de iluminação, priorizando o FPS."),
        new("FidelityFX Super Resolution", "Disabled (Highest Quality)", "Mantém a resolução nativa e a nitidez da imagem."),
        new("Ambient Occlusion", "Disabled", "Remove o sombreamento de cantos, que pesa e não ajuda na leitura da cena."),
        new("Global Shadow Quality", "Low", "Reduz o custo das sombras sem tirar as sombras dinâmicas.",
            "Preferência: High mostra sombras mais completas em troca de FPS."),
        new("Model / Texture Detail", "Low", "Texturas e modelos mais leves; afeta pouco a visibilidade.",
            "Com VRAM sobrando, Medium muda pouco o FPS."),
        new("Shader Detail", "Low", "Diminui o custo dos efeitos de superfície."),
        new("Particle Detail", "Low", "Fumaça, faíscas e efeitos mais leves, com menos queda de FPS em lutas."),
        new("Texture Filtering Mode", "Bilinear", "O modo mais leve de filtragem de texturas.",
            "Preferência: Anisotropic 4x deixa texturas mais nítidas a distância com custo pequeno."),
        new("Multisampling Anti-Aliasing Mode", "CMAA2", "Suaviza as bordas com custo baixo. Não desligue tudo: None deixa as silhuetas serrilhadas.",
            "Preferência e hardware: 2x MSAA fica mais nítido se a GPU tiver folga."),
    ];

    public static IReadOnlyList<string> Notes { get; } =
    [
        "São instruções manuais: o LZN Tweaker não altera o processo do jogo nem os arquivos do CS2, e nada aqui aparece como aplicado.",
        "Os nomes acima são os do jogo em inglês. Com o CS2 em português, procure a opção equivalente no mesmo menu.",
        "Use o AMD Anti-Lag 2 do próprio jogo. Nunca use o antigo Anti-Lag+ do driver, que causou banimentos VAC em 2023.",
    ];
}
