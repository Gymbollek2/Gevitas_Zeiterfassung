using System.Globalization;

namespace Gevitas.Zeiterfassung.Web.Services;

/// <summary>
/// Einfache Mehrsprachigkeit (Deutsch/Englisch). Schlüssel ist immer der deutsche Text; fehlt eine Übersetzung,
/// wird der deutsche Text angezeigt. Die Sprache gilt pro Anfrage (wird von der Middleware aus dem Cookie gesetzt).
/// </summary>
public static class Texte
{
    private static readonly AsyncLocal<string?> AktuelleSprache = new();

    public static string Sprache
    {
        get => AktuelleSprache.Value ?? "de";
        set => AktuelleSprache.Value = value;
    }

    public static bool IstEnglisch => Sprache == "en";

    /// <summary>Übersetzt einen festen Text.</summary>
    public static string T(string deutsch) =>
        IstEnglisch && Uebersetzungen.Englisch.TryGetValue(deutsch, out var englisch) ? englisch : deutsch;

    /// <summary>Übersetzt einen Text mit Platzhaltern ({0}, {1} …) und setzt die Werte ein.</summary>
    public static string F(string deutschesFormat, params object?[] werte) =>
        string.Format(CultureInfo.InvariantCulture, T(deutschesFormat), werte);

    /// <summary>Alle verwendeten Schlüssel, für die (noch) keine englische Übersetzung existiert – für Tests.</summary>
    public static bool HatUebersetzung(string deutsch) => Uebersetzungen.Englisch.ContainsKey(deutsch);
}
