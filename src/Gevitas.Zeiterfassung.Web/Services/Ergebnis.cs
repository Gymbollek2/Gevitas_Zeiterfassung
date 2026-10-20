namespace Gevitas.Zeiterfassung.Web.Services;

/// <summary>Ergebnis einer fachlichen Operation mit einer für Benutzer lesbaren Meldung.</summary>
public sealed record Ergebnis(bool Erfolg, string Meldung)
{
    public static Ergebnis Ok(string meldung) => new(true, meldung);
    public static Ergebnis Fehler(string meldung) => new(false, meldung);
}
