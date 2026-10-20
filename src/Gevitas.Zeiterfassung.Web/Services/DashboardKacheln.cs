namespace Gevitas.Zeiterfassung.Web.Services;

/// <summary>Konfigurierbare Inhalte des Dashboards (je Benutzer gespeichert).</summary>
public static class DashboardKacheln
{
    public const string Terminal = "terminal";
    public const string KrankUntertaegig = "krank";
    public const string Wegezeit = "wegezeit";
    public const string Konten = "konten";
    public const string Benachrichtigungen = "benachrichtigungen";

    public static readonly IReadOnlyList<(string Schluessel, string Bezeichnung, string Beschreibung)> Alle = new[]
    {
        (Terminal, "Terminal: Kommen oder Gehen", "Ein-/Ausstempeln mit der aktuellen Uhrzeit"),
        (KrankUntertaegig, "Terminal: Krank untertägig", "Füllt nach dem Arbeitsbeginn die restliche Sollzeit auf"),
        (Wegezeit, "Terminal: Wegezeit (Kommen oder Gehen)", "Start und Ende einer Wegezeit buchen"),
        (Konten, "Information (Konten)", "Saldo, Arbeitszeit, Soll, Wegezeit, Sonderarbeitszeit und Urlaub"),
        (Benachrichtigungen, "Benachrichtigungen", "Neue Anträge und Entscheidungen")
    };

    public static readonly IReadOnlySet<string> Standard = new HashSet<string> { Terminal, Konten, Benachrichtigungen };

    public static IReadOnlySet<string> Lesen(string? gespeichert) =>
        gespeichert is null
            ? Standard
            : gespeichert.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();

    public static string Schreiben(IEnumerable<string> auswahl) =>
        string.Join(',', auswahl.Where(a => Alle.Any(k => k.Schluessel == a)).Distinct());
}
