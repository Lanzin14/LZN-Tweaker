using System.Text;

namespace LznTweaker.Core.GameTuning;

/// <summary>Nó do formato KeyValues da Valve (.vdf, .acf, gameinfo.gi): valor simples ou lista de filhos.</summary>
public sealed record KeyValuesNode(string Key, string? Value, IReadOnlyList<KeyValuesNode> Children)
{
    public KeyValuesNode? Child(string key) =>
        Children.FirstOrDefault(child => string.Equals(child.Key, key, StringComparison.OrdinalIgnoreCase));

    public string? ChildValue(string key) => Child(key)?.Value;
}

/// <summary>
/// Leitor tolerante de KeyValues: aceita chaves com ou sem aspas, comentários //, chaves repetidas e
/// condicionais [$WIN64] (ignoradas). Serve para ler, nunca para reescrever arquivos do jogo.
/// </summary>
public static class KeyValuesReader
{
    public static IReadOnlyList<KeyValuesNode> Parse(string text)
    {
        var tokens = Tokenize(text);
        var index = 0;
        return ParseBlock(tokens, ref index, topLevel: true);
    }

    private static List<KeyValuesNode> ParseBlock(List<string> tokens, ref int index, bool topLevel)
    {
        var nodes = new List<KeyValuesNode>();
        while (index < tokens.Count)
        {
            var token = tokens[index++];
            if (token == "}")
            {
                if (topLevel)
                    continue; // chave de fechamento sobrando: ignora
                return nodes;
            }
            if (token == "{")
            {
                // Bloco sem chave: lê e descarta para manter o alinhamento.
                ParseBlock(tokens, ref index, topLevel: false);
                continue;
            }

            if (index < tokens.Count && tokens[index] == "{")
            {
                index++;
                nodes.Add(new KeyValuesNode(token, null, ParseBlock(tokens, ref index, topLevel: false)));
            }
            else if (index < tokens.Count && tokens[index] != "}")
            {
                nodes.Add(new KeyValuesNode(token, tokens[index++], []));
            }
            else
            {
                nodes.Add(new KeyValuesNode(token, string.Empty, []));
            }
        }
        return nodes;
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n') i++;
                continue;
            }
            if (c is '{' or '}') { tokens.Add(c.ToString()); i++; continue; }
            if (c == '[')
            {
                // Condicional de plataforma ([$WIN64], [!$X360]): não altera a estrutura.
                while (i < text.Length && text[i] != ']' && text[i] != '\n') i++;
                i++;
                continue;
            }
            if (c == '"')
            {
                var builder = new StringBuilder();
                i++;
                while (i < text.Length && text[i] != '"')
                {
                    if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] is '"' or '\\')
                        i++;
                    builder.Append(text[i++]);
                }
                i++;
                tokens.Add(builder.ToString());
                continue;
            }

            var start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] is not ('{' or '}' or '"'))
            {
                if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '/')
                    break;
                i++;
            }
            tokens.Add(text[start..i]);
        }
        return tokens;
    }
}
