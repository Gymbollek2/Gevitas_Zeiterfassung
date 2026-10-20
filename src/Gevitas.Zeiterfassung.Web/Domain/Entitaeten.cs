namespace Gevitas.Zeiterfassung.Web.Domain;

public class Mitarbeiter
{
    public int Id { get; set; }
    public string Personalnummer { get; set; } = string.Empty;
    public string Benutzername { get; set; } = string.Empty;
    public string Vorname { get; set; } = string.Empty;
    public string Nachname { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Abteilung { get; set; }
    public string PasswortHash { get; set; } = string.Empty;
    public Rolle Rolle { get; set; } = Rolle.Mitarbeiter;

    /// <summary>Disziplinarische Führungskraft – automatisch Genehmiger aller Anträge.</summary>
    public int? VorgesetzterId { get; set; }
    public Mitarbeiter? Vorgesetzter { get; set; }

    /// <summary>Durch die Personalabteilung hinterlegter abweichender Genehmiger (Vertretung).</summary>
    public int? AbweichenderGenehmigerId { get; set; }
    public Mitarbeiter? AbweichenderGenehmiger { get; set; }

    public int SollMinutenProTag { get; set; } = 480;
    public int Jahresurlaub { get; set; } = 30;
    public DateOnly Eintrittsdatum { get; set; }
    public bool Aktiv { get; set; } = true;
    public bool PasswortAendernErforderlich { get; set; }
    public DateTime? LetzteAnmeldung { get; set; }

    /// <summary>Kommagetrennte Liste der sichtbaren Dashboard-Kacheln (null = Standard).</summary>
    public string? DashboardKacheln { get; set; }

    /// <summary>Darstellung: system, hell oder dunkel (null = system).</summary>
    public string? Design { get; set; }

    /// <summary>Sprache der Oberfläche: de oder en (null = de).</summary>
    public string? Sprache { get; set; }

    public string AnzeigeName => $"{Nachname}, {Vorname}";
    public string VollerName => $"{Vorname} {Nachname}";
}

public class Zeitbuchung
{
    public int Id { get; set; }
    public int MitarbeiterId { get; set; }
    public Mitarbeiter? Mitarbeiter { get; set; }
    public DateOnly Datum { get; set; }
    public TimeOnly Beginn { get; set; }
    public TimeOnly? Ende { get; set; }
    public BuchungsArt Art { get; set; } = BuchungsArt.Arbeit;
    public BuchungsQuelle Quelle { get; set; } = BuchungsQuelle.Manuell;
    public DateTime ErstelltAm { get; set; }
    public DateTime GeaendertAm { get; set; }
}

/// <summary>Protokoll aller Änderungen an Zeitbuchungen (Buchungsverlauf des Tages).</summary>
public class Buchungsprotokoll
{
    public int Id { get; set; }
    public int MitarbeiterId { get; set; }
    public DateOnly Datum { get; set; }
    public string Typ { get; set; } = "Zeitereignis";
    public string Details { get; set; } = string.Empty;
    public string? Bemerkung { get; set; }
    public DateTime Zeitpunkt { get; set; }
    public int AusgefuehrtVonId { get; set; }
    public string AusgefuehrtVonName { get; set; } = string.Empty;
}

public class Tagesbemerkung
{
    public int Id { get; set; }
    public int MitarbeiterId { get; set; }
    public DateOnly Datum { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime GeaendertAm { get; set; }
}

public class Abwesenheitsart
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Farbe { get; set; } = "#7fb3e6";
    public AbwesenheitsWirkung Wirkung { get; set; }
    public bool ZaehltAlsUrlaub { get; set; }
    public int? MaxKalendertage { get; set; }
    public bool NachweisErforderlich { get; set; }
    public int Sortierung { get; set; }
    public bool Aktiv { get; set; } = true;
}

public class Abwesenheit
{
    public int Id { get; set; }
    public int MitarbeiterId { get; set; }
    public Mitarbeiter? Mitarbeiter { get; set; }
    public string ArtCode { get; set; } = string.Empty;
    public Abwesenheitsart? Art { get; set; }
    public DateOnly Von { get; set; }
    public DateOnly Bis { get; set; }
    public AbwesenheitsStatus Status { get; set; } = AbwesenheitsStatus.Beantragt;
    public string? Bemerkung { get; set; }
    public DateTime ErstelltAm { get; set; }

    public bool IstWirksam => Status is AbwesenheitsStatus.Beantragt or AbwesenheitsStatus.Genehmigt;
    public bool Umfasst(DateOnly datum) => datum >= Von && datum <= Bis;
}

public class Antrag
{
    public int Id { get; set; }
    public int AntragstellerId { get; set; }
    public Mitarbeiter? Antragsteller { get; set; }
    public AntragsArt Art { get; set; }
    public AntragsStatus Status { get; set; } = AntragsStatus.Offen;
    public DateOnly Von { get; set; }
    public DateOnly Bis { get; set; }
    public int? AbwesenheitId { get; set; }
    public Abwesenheit? Abwesenheit { get; set; }
    public string? Bemerkung { get; set; }

    /// <summary>Aktuell zuständiger Genehmiger (null = Personalabteilung).</summary>
    public int? GenehmigerId { get; set; }
    public Mitarbeiter? Genehmiger { get; set; }

    /// <summary>Aktuelle Genehmigungsstufe (1-basiert) und Anzahl der Stufen.</summary>
    public int Stufe { get; set; } = 1;
    public int Stufen { get; set; } = 1;

    public string? Entscheidungsbemerkung { get; set; }
    public int? EntschiedenVonId { get; set; }
    public Mitarbeiter? EntschiedenVon { get; set; }
    public DateTime ErstelltAm { get; set; }
    public DateTime? EntschiedenAm { get; set; }
}

/// <summary>Tagesbezogene Freigaben (Arbeit &gt; 10 Std., vor/nach Rahmen). Werden bei Antragstellung sofort gesetzt.</summary>
public class Tagesberechtigung
{
    public int Id { get; set; }
    public int MitarbeiterId { get; set; }
    public DateOnly Datum { get; set; }
    public bool ArbeitUeber10Stunden { get; set; }
    public bool ArbeitVorRahmen { get; set; }
    public bool ArbeitNachRahmen { get; set; }
}

/// <summary>Manuelle Buchung auf ein Konto (Umbuchung, Korrektur der Personalabteilung).</summary>
public class Kontobuchung
{
    public int Id { get; set; }
    public int MitarbeiterId { get; set; }
    public DateOnly Datum { get; set; }
    public Konto Konto { get; set; }
    public int Minuten { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Bemerkung { get; set; }
    public Guid Vorgang { get; set; }
    public bool Storniert { get; set; }
    public int ErstelltVonId { get; set; }
    public DateTime ErstelltAm { get; set; }
}

public class Regelquittierung
{
    public int Id { get; set; }
    public int MitarbeiterId { get; set; }
    public DateOnly Datum { get; set; }
    public string Regelcode { get; set; } = string.Empty;
    public DateTime QuittiertAm { get; set; }
}

public class Benachrichtigung
{
    public int Id { get; set; }
    public int MitarbeiterId { get; set; }
    public string Titel { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? Link { get; set; }
    public DateTime ErstelltAm { get; set; }
    public bool Gelesen { get; set; }
}
