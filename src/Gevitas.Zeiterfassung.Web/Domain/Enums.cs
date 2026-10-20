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
        Rolle.Mitarbeiter => "Mitarbeiter/in",
        Rolle.Fuehrungskraft => "Führungskraft",
        Rolle.Personal => "Personalabteilung",
        Rolle.Administrator => "Administrator/in",
        _ => rolle.ToString()
    };

    public static string Text(this BuchungsArt art) => art switch
    {
        BuchungsArt.Arbeit => "Arbeitszeit",
        BuchungsArt.Wegezeit => "Wegezeit",
        BuchungsArt.KrankUntertaegig => "Krank untertägig",
        _ => art.ToString()
    };

    public static string Text(this AbwesenheitsStatus status) => status switch
    {
        AbwesenheitsStatus.Beantragt => "Beantragt",
        AbwesenheitsStatus.Genehmigt => "Genehmigt",
        AbwesenheitsStatus.Abgelehnt => "Abgelehnt",
        AbwesenheitsStatus.Storniert => "Storniert",
        AbwesenheitsStatus.Zurueckgezogen => "Zurückgezogen",
        _ => status.ToString()
    };

    public static string Text(this AntragsArt art) => art switch
    {
        AntragsArt.Abwesenheit => "Abwesenheit",
        AntragsArt.AbwesenheitLoeschen => "Löschung Abwesenheit",
        AntragsArt.ArbeitUeber10Stunden => "Arbeit > 10 Std.",
        AntragsArt.ArbeitVorRahmen => "Arbeit vor Rahmen",
        AntragsArt.ArbeitNachRahmen => "Arbeit nach Rahmen",
        AntragsArt.UrlaubsUebertrag => "Übertragung Resturlaub",
        _ => art.ToString()
    };

    public static string Text(this AntragsStatus status) => status switch
    {
        AntragsStatus.Offen => "Beantragt",
        AntragsStatus.Genehmigt => "Genehmigt",
        AntragsStatus.Abgelehnt => "Abgelehnt",
        AntragsStatus.Zurueckgezogen => "Zurückgezogen",
        _ => status.ToString()
    };

    public static string Text(this Konto konto) => konto switch
    {
        Konto.Saldo => "Gleitzeitsaldo",
        Konto.Sonderarbeitszeit => "Sonderarbeitszeit",
        Konto.Wegezeit => "Wegezeit",
        _ => konto.ToString()
    };
}
