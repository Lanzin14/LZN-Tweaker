namespace LznTweaker.Infrastructure;

/// <summary>Quantidade com a palavra no singular ou no plural ("1 app", "3 apps"), sem "app(s)" na interface.</summary>
public static class Plural
{
    public static string Of(long count, string singular, string plural) => $"{count:N0} {(count == 1 ? singular : plural)}";
}
