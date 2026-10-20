namespace Gevitas.Zeiterfassung.Web.Services;

/// <summary>Englische Übersetzungen. Schlüssel = deutscher Originaltext.</summary>
internal static class Uebersetzungen
{
    public static readonly IReadOnlyDictionary<string, string> Englisch = new Dictionary<string, string>
    {
        // Platzhalter – wird mit der vollständigen Übersetzung befüllt
        ["Darstellung und Sprache wurden gespeichert."] = "Appearance and language have been saved.",
        ["Das Dashboard wurde angepasst."] = "The dashboard has been updated.",
        ["Das Dashboard wurde auf den Standard zurückgesetzt."] = "The dashboard has been reset to the default."
    };
}
