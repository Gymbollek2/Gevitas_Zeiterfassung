using System.Globalization;

namespace Gevitas.Zeiterfassung.Web.Options;

/// <summary>
/// Fachliche Einstellungen der Zeiterfassung (Abschnitt "Zeiterfassung" in der appsettings.json).
/// </summary>
public sealed class ZeiterfassungOptions
{
    public const string Abschnitt = "Zeiterfassung";

    public string Firmenname { get; set; } = "Gevitas";
    public string Anwendungsname { get; set; } = "Gevitas Zeiterfassung";

    /// <summary>IANA-Zeitzone, in der gebucht wird (wichtig im Docker-Container, der in UTC läuft).</summary>
    public string Zeitzone { get; set; } = "Europe/Berlin";

    public SollarbeitszeitOptions Sollarbeitszeit { get; set; } = new();
    public RahmenarbeitszeitOptions Rahmenarbeitszeit { get; set; } = new();

    /// <summary>Kappungsgrenze der täglichen Arbeitszeit (Standard 10 Stunden = 600 Minuten).</summary>
    public int MaxArbeitszeitMinuten { get; set; } = 600;

    public PausenOptions Pausen { get; set; } = new();

    /// <summary>Wie viele Tage rückwirkend Mitarbeitende selbst Zeiten erfassen dürfen.</summary>
    public int MaxRueckwirkendeTage { get; set; } = 9;

    /// <summary>Gelesene Benachrichtigungen werden nach dieser Anzahl Tage beim Start der Anwendung gelöscht.</summary>
    public int BenachrichtigungenAufbewahrenTage { get; set; } = 180;

    public SaldoOptions Saldo { get; set; } = new();
    public UrlaubOptions Urlaub { get; set; } = new();

    /// <summary>"Krank ohne Attest" ist für maximal so viele zusammenhängende Kalendertage erlaubt.</summary>
    public int KrankOhneAttestMaxTage { get; set; } = 3;

    /// <summary>Mindestruhezeit zwischen zwei Arbeitstagen (ArbZG: 11 Stunden).</summary>
    public int MinRuhezeitMinuten { get; set; } = 660;

    /// <summary>Gesetzliche Feiertage, die als arbeitsfrei gelten (Schlüssel siehe FeiertagsService).</summary>
    public List<string> Feiertage { get; set; } = new();

    public List<AbwesenheitsartOptions> Abwesenheitsarten { get; set; } = new();

    public SupportOptions Support { get; set; } = new();
}

public sealed class SollarbeitszeitOptions
{
    /// <summary>Standard-Sollarbeitszeit pro Arbeitstag für neue Mitarbeitende.</summary>
    public int StandardMinutenProTag { get; set; } = 480;

    /// <summary>Arbeitstage der Woche (englische Namen, z. B. "Monday").</summary>
    public List<DayOfWeek> Arbeitstage { get; set; } = new();

    public IReadOnlyCollection<DayOfWeek> ArbeitstageOderStandard =>
        Arbeitstage.Count > 0
            ? Arbeitstage
            : new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
}

public sealed class RahmenarbeitszeitOptions
{
    public string Beginn { get; set; } = "06:00";
    public string Ende { get; set; } = "20:00";

    public TimeOnly BeginnZeit => TimeOnly.ParseExact(Beginn, "HH:mm", CultureInfo.InvariantCulture);
    public TimeOnly EndeZeit => TimeOnly.ParseExact(Ende, "HH:mm", CultureInfo.InvariantCulture);
}

public sealed class PausenOptions
{
    /// <summary>Eine Unterbrechung zählt erst ab dieser Dauer als Ruhepause.</summary>
    public int MinPauseMinuten { get; set; } = 15;
    public int Schwelle1Minuten { get; set; } = 360;
    public int Pause1Minuten { get; set; } = 30;
    public int Schwelle2Minuten { get; set; } = 540;
    public int Pause2Minuten { get; set; } = 45;

    /// <summary>Länger als diese Dauer darf ohne Pause nicht am Stück gearbeitet werden.</summary>
    public int MaxBlockOhnePauseMinuten { get; set; } = 360;
    public int BlockPauseMinuten { get; set; } = 15;
}

public sealed class SaldoOptions
{
    public int UntergrenzeMinuten { get; set; } = -2400;
    public int ObergrenzeMinuten { get; set; } = 4800;

    /// <summary>Saldo-Ampel: ab diesem Betrag (positiv oder negativ) gelb.</summary>
    public int AmpelGelbAbMinuten { get; set; } = 1200;

    /// <summary>Saldo-Ampel: ab diesem Betrag (positiv oder negativ) rot.</summary>
    public int AmpelRotAbMinuten { get; set; } = 2400;
}

public sealed class UrlaubOptions
{
    public int StandardJahresurlaub { get; set; } = 30;

    /// <summary>Resturlaub des Vorjahres verfällt nach diesem Tag (Format MM-TT).</summary>
    public string Verfall { get; set; } = "03-31";

    /// <summary>Verfallstag bei genehmigter Übertragung (Format MM-TT).</summary>
    public string VerlaengerterVerfall { get; set; } = "05-31";

    /// <summary>Zeitraum, in dem die Übertragung beantragt werden kann (Format MM-TT).</summary>
    public string UebertragAntragVon { get; set; } = "01-01";
    public string UebertragAntragBis { get; set; } = "03-01";

    public static DateOnly Datum(int jahr, string monatTag)
    {
        var teile = monatTag.Split('-');
        return new DateOnly(jahr, int.Parse(teile[0], CultureInfo.InvariantCulture), int.Parse(teile[1], CultureInfo.InvariantCulture));
    }
}

public sealed class AbwesenheitsartOptions
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Farbe { get; set; } = "#7fb3e6";

    /// <summary>SollGutschrift | Gleitzeitabbau | AbbauSonderarbeitszeit | AbbauWegezeit</summary>
    public string Wirkung { get; set; } = "SollGutschrift";

    public bool ZaehltAlsUrlaub { get; set; }
    public int? MaxKalendertage { get; set; }
    public bool NachweisErforderlich { get; set; }
}

public sealed class SupportOptions
{
    public string PersonalabteilungEmail { get; set; } = "personalabteilung@gevitas.de";
    public string ItSupportHinweis { get; set; } = "Bei technischen Problemen bitte ein Ticket bei der IT erstellen (Kategorie: Software/Zeiterfassung).";
}

public sealed class SmtpOptions
{
    public const string Abschnitt = "Smtp";

    public bool Aktiv { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool SslAktiv { get; set; } = true;
    public string Benutzer { get; set; } = string.Empty;
    public string Passwort { get; set; } = string.Empty;
    public string Absender { get; set; } = "zeiterfassung@gevitas.de";
}

public sealed class SeedOptions
{
    public const string Abschnitt = "Seed";

    public bool Aktiv { get; set; } = true;
    public string AdminBenutzername { get; set; } = "admin";
    public string AdminPasswort { get; set; } = string.Empty;

    /// <summary>Legt Demo-Benutzer (Führungskraft, Mitarbeiter, Personal) an – nur für Entwicklung.</summary>
    public bool Demodaten { get; set; }
}

public sealed class AuthentifizierungOptions
{
    public const string Abschnitt = "Authentifizierung";

    public string CookieName { get; set; } = "Gevitas.Zeiterfassung.Auth";
    public int SitzungsdauerMinuten { get; set; } = 480;
    public int MinPasswortLaenge { get; set; } = 8;
}
