using static Gevitas.Zeiterfassung.Web.Services.Texte;

namespace Gevitas.Zeiterfassung.Web.Domain;

/// <summary>Rolle eines Benutzers. Höhere Rollen schließen die Rechte der niedrigeren ein.</summary>
public enum Rolle
{
    Mitarbeiter = 0,
    Fuehrungskraft = 1,
    Personal = 2,
    Administrator = 3
}

public enum BuchungsArt
{
    Arbeit = 0,
    Wegezeit = 1,
    KrankUntertaegig = 2
}

public enum BuchungsQuelle
{
    Terminal = 0,
    Manuell = 1
}

/// <summary>Wie eine ganztägige Abwesenheit auf die Konten wirkt.</summary>
public enum AbwesenheitsWirkung
{
    /// <summary>Der Tag wird mit der Sollarbeitszeit gutgeschrieben (Urlaub, Krankheit, Sonderurlaub).</summary>
    SollGutschrift = 0,

    /// <summary>Keine Gutschrift – das Gleitzeitkonto wird um die Sollzeit reduziert.</summary>
    Gleitzeitabbau = 1,

    /// <summary>Gutschrift auf das Gleitzeitkonto, Abbuchung vom Sonderarbeitszeitkonto.</summary>
    AbbauSonderarbeitszeit = 2,

    /// <summary>Gutschrift auf das Gleitzeitkonto, Abbuchung vom Wegezeitkonto.</summary>
    AbbauWegezeit = 3
}

public enum AbwesenheitsStatus
{
    Beantragt = 0,
    Genehmigt = 1,
    Abgelehnt = 2,
    Storniert = 3,
    Zurueckgezogen = 4
}

public enum AntragsArt
{
    Abwesenheit = 0,
    AbwesenheitLoeschen = 1,
    ArbeitUeber10Stunden = 2,
    ArbeitVorRahmen = 3,
    ArbeitNachRahmen = 4,
    UrlaubsUebertrag = 5
}

public enum AntragsStatus
{
    Offen = 0,
    Genehmigt = 1,
    Abgelehnt = 2,
    Zurueckgezogen = 3
}

public enum Konto
{
    Saldo = 0,
    Sonderarbeitszeit = 1,
    Wegezeit = 2
}

public static class EnumTexte
{
    public static string Text(this Rolle rolle) => rolle switch
    {
        Rolle.Mitarbeiter => T("Mitarbeiter/in"),
        Rolle.Fuehrungskraft => T("Führungskraft"),
        Rolle.Personal => T("Personalabteilung"),
        Rolle.Administrator => T("Administrator/in"),
        _ => rolle.ToString()
    };

    public static string Text(this BuchungsArt art) => art switch
    {
        BuchungsArt.Arbeit => T("Arbeitszeit"),
        BuchungsArt.Wegezeit => T("Wegezeit"),
        BuchungsArt.KrankUntertaegig => T("Krank untertägig"),
        _ => art.ToString()
    };

    public static string Text(this AbwesenheitsStatus status) => status switch
    {
        AbwesenheitsStatus.Beantragt => T("Beantragt"),
        AbwesenheitsStatus.Genehmigt => T("Genehmigt"),
        AbwesenheitsStatus.Abgelehnt => T("Abgelehnt"),
        AbwesenheitsStatus.Storniert => T("Storniert"),
        AbwesenheitsStatus.Zurueckgezogen => T("Zurückgezogen"),
        _ => status.ToString()
    };

    public static string Text(this AntragsArt art) => art switch
    {
        AntragsArt.Abwesenheit => T("Abwesenheit"),
        AntragsArt.AbwesenheitLoeschen => T("Löschung Abwesenheit"),
        AntragsArt.ArbeitUeber10Stunden => T("Arbeit > 10 Std."),
        AntragsArt.ArbeitVorRahmen => T("Arbeit vor Rahmen"),
        AntragsArt.ArbeitNachRahmen => T("Arbeit nach Rahmen"),
        AntragsArt.UrlaubsUebertrag => T("Übertragung Resturlaub"),
        _ => art.ToString()
    };

    public static string Text(this AntragsStatus status) => status switch
    {
        AntragsStatus.Offen => T("Beantragt"),
        AntragsStatus.Genehmigt => T("Genehmigt"),
        AntragsStatus.Abgelehnt => T("Abgelehnt"),
        AntragsStatus.Zurueckgezogen => T("Zurückgezogen"),
        _ => status.ToString()
    };

    public static string Text(this Konto konto) => konto switch
    {
        Konto.Saldo => T("Gleitzeitsaldo"),
        Konto.Sonderarbeitszeit => T("Sonderarbeitszeit"),
        Konto.Wegezeit => T("Wegezeit"),
        _ => konto.ToString()
    };
}
