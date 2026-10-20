namespace Gevitas.Zeiterfassung.Web.Infrastructure;

/// <summary>
/// Darstellung (Hell/Dunkel/System) und Sprache. Die Werte werden pro Benutzer in der Datenbank gespeichert
/// und zusätzlich als Cookie gesetzt, damit sie bei jeder Anfrage – auch auf der Anmeldeseite – sofort wirken.
/// </summary>
public static class Benutzereinstellungen
{
    public const string DesignCookie = "gevitas.design";
    public const string SpracheCookie = "gevitas.sprache";

    public const string DesignSystem = "system";
    public const string DesignHell = "hell";
    public const string DesignDunkel = "dunkel";

    public const string Deutsch = "de";
    public const string Englisch = "en";

    public static readonly IReadOnlyList<string> Designs = new[] { DesignSystem, DesignHell, DesignDunkel };
    public static readonly IReadOnlyList<string> Sprachen = new[] { Deutsch, Englisch };

    public static string? GueltigesDesign(string? wert) => Normalisieren(wert, Designs);
    public static string? GueltigeSprache(string? wert) => Normalisieren(wert, Sprachen);

    public static string Design(HttpContext context) =>
        GueltigesDesign(context.Request.Cookies[DesignCookie]) ?? DesignSystem;

    public static string Sprache(HttpContext context) =>
        GueltigeSprache(context.Request.Cookies[SpracheCookie]) ?? Deutsch;

    public static void Setzen(HttpContext context, string cookie, string wert)
    {
        context.Response.Cookies.Append(cookie, wert, new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps
        });
    }

    /// <summary>Übernimmt die gespeicherten Einstellungen eines Benutzers (z. B. nach der Anmeldung) in die Cookies.</summary>
    public static void Uebernehmen(HttpContext context, string? design, string? sprache)
    {
        if (GueltigesDesign(design) is { } d)
        {
            Setzen(context, DesignCookie, d);
        }

        if (GueltigeSprache(sprache) is { } s)
        {
            Setzen(context, SpracheCookie, s);
        }
    }

    private static string? Normalisieren(string? wert, IReadOnlyList<string> erlaubt)
    {
        var normalisiert = wert?.Trim().ToLowerInvariant();
        return normalisiert is not null && erlaubt.Contains(normalisiert) ? normalisiert : null;
    }
}
