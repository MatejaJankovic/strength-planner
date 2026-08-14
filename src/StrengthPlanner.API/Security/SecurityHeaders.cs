namespace StrengthPlanner.API.Security;

/// <summary>
/// Bezbednosna zaglavlja na odgovorima samog API-ja.
///
/// nginx ih već šalje na sve što posluži (<c>strength-planner-web/security-headers.conf</c>),
/// i to je i dalje glavni sloj — ali on pokriva samo saobraćaj koji zaista prođe kroz njega.
/// API se pokreće i bez njega: u razvoju, u testovima, i u svakoj isporuci gde neko objavi
/// port ili promeni proxy. Tada je odgovor do sada išao potpuno go.
///
/// Pravila su uža nego na frontendu, jer API ne služi stranice: sve je zabranjeno, jer
/// nijedan JSON odgovor ne treba da učitava skripte, stilove ni okvire. Ako se takav
/// odgovor ipak negde prikaže kao dokument — greškom u tipu sadržaja ili preko starije
/// verzije pregledača — nema šta da se izvrši.
/// </summary>
public static class SecurityHeaders
{
    /// <summary>
    /// Zaglavlja su statična, pa se prave jednom umesto po zahtevu.
    /// </summary>
    private static readonly (string Name, string Value)[] Headers =
    {
        // API vraća isključivo JSON. Zabrana svega je ovde tačna, a ne stroga.
        ("Content-Security-Policy",
            "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'"),

        // Bez ovoga pregledač sme da pogađa tip sadržaja i JSON protumači kao HTML.
        ("X-Content-Type-Options", "nosniff"),
        ("X-Frame-Options", "DENY"),

        // Putanja zna identifikatore korisnikovih zapisa; ne šalju se trećoj strani.
        ("Referrer-Policy", "no-referrer"),
        ("Permissions-Policy", "geolocation=(), microphone=(), camera=()"),

        // Odgovori nose lične trenažne podatke i nemaju šta da traže u deljenom kešu.
        ("Cache-Control", "no-store")
    };

    public static IApplicationBuilder UseApiSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            // Postavlja se pre nego što bilo šta krene ka klijentu: kada odgovor jednom
            // počne, zaglavlja se više ne mogu menjati.
            context.Response.OnStarting(() =>
            {
                foreach (var (name, value) in Headers)
                {
                    // Ne prepisuj ako je neki endpoint svesno postavio svoje.
                    if (!context.Response.Headers.ContainsKey(name))
                    {
                        context.Response.Headers[name] = value;
                    }
                }

                return Task.CompletedTask;
            });

            await next();
        });
    }
}
