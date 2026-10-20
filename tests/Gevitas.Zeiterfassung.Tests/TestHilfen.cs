using Gevitas.Zeiterfassung.Web.Data;
using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Options;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Gevitas.Zeiterfassung.Tests;

/// <summary>Steuerbare Uhr für Tests.</summary>
public sealed class FesteUhr : IAppUhr
{
    public FesteUhr(DateTime jetzt)
    {
        Jetzt = jetzt;
    }

    public DateTime Jetzt { get; set; }
    public DateOnly Heute => DateOnly.FromDateTime(Jetzt);
    public TimeOnly UhrzeitMinute => new(Jetzt.Hour, Jetzt.Minute);
}

/// <summary>SQLite-In-Memory-Datenbank, die für die Dauer eines Tests geöffnet bleibt.</summary>
public sealed class TestDatenbank : IDisposable
{
    private readonly SqliteConnection _verbindung;

    public TestDatenbank()
    {
        _verbindung = new SqliteConnection("DataSource=:memory:");
        _verbindung.Open();
        using var db = NeuerKontext();
        db.Database.EnsureCreated();
    }

    public ZeiterfassungDbContext NeuerKontext()
    {
        var optionen = new DbContextOptionsBuilder<ZeiterfassungDbContext>().UseSqlite(_verbindung).Options;
        return new ZeiterfassungDbContext(optionen);
    }

    public void Dispose() => _verbindung.Dispose();
}

public static class TestDaten
{
    public static ZeiterfassungOptions Optionen()
    {
        var o = new ZeiterfassungOptions
        {
            Feiertage = new List<string>
            {
                "Neujahr", "HeiligeDreiKoenige", "Karfreitag", "Ostermontag", "TagDerArbeit", "ChristiHimmelfahrt",
                "Pfingstmontag", "Fronleichnam", "TagDerDeutschenEinheit", "Allerheiligen", "ErsterWeihnachtstag", "ZweiterWeihnachtstag"
            },
            Abwesenheitsarten = new List<AbwesenheitsartOptions>
            {
                new() { Code = "U", Name = "Urlaub", Wirkung = "SollGutschrift", ZaehltAlsUrlaub = true },
                new() { Code = "GZ", Name = "Gleitzeitabbau", Wirkung = "Gleitzeitabbau" },
                new() { Code = "AS", Name = "Abbau Sonderarbeitszeit", Wirkung = "AbbauSonderarbeitszeit" },
                new() { Code = "AW", Name = "Abbau Wegezeit", Wirkung = "AbbauWegezeit" },
                new() { Code = "KR", Name = "Krank ohne Attest", Wirkung = "SollGutschrift", MaxKalendertage = 3 },
                new() { Code = "KRE", Name = "Krank mit eAU", Wirkung = "SollGutschrift" }
            }
        };
        return o;
    }

    public static Microsoft.Extensions.Options.IOptions<T> Als<T>(T wert) where T : class => MsOptions.Create(wert);

    public static Mitarbeiter Mitarbeiter(string benutzer, Rolle rolle = Rolle.Mitarbeiter, Mitarbeiter? chef = null) => new()
    {
        Personalnummer = benutzer.ToUpperInvariant(),
        Benutzername = benutzer,
        Vorname = benutzer,
        Nachname = "Test",
        Email = $"{benutzer}@test.local",
        Rolle = rolle,
        Vorgesetzter = chef,
        SollMinutenProTag = 480,
        Jahresurlaub = 30,
        Eintrittsdatum = new DateOnly(2025, 1, 1),
        PasswortHash = "x"
    };
}
