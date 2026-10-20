using Gevitas.Zeiterfassung.Web.Data;
using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Options;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gevitas.Zeiterfassung.Tests;

public class DatenbankInitialisierungTests : IDisposable
{
    private readonly TestDatenbank _datenbank = new();

    public void Dispose() => _datenbank.Dispose();

    [Fact]
    public async Task Bestehende_Datenbank_erhaelt_neue_Spalten_und_Stammdaten()
    {
        using var db = _datenbank.NeuerKontext();

        // Datenbank einer älteren Version simulieren (ohne die Spalten aus Version 1.1.0)
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Mitarbeiter\" DROP COLUMN \"Design\"");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Mitarbeiter\" DROP COLUMN \"Sprache\"");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Mitarbeiter\" DROP COLUMN \"ErfassungAb\"");

        var initialisierung = new DatenbankInitialisierung(
            db,
            TestDaten.Als(TestDaten.Optionen()),
            TestDaten.Als(new SeedOptions { AdminPasswort = "Start1234" }),
            new PasswortService(TestDaten.Als(new AuthentifizierungOptions())),
            new FesteUhr(new DateTime(2026, 10, 20, 8, 0, 0)),
            NullLogger<DatenbankInitialisierung>.Instance);

        await initialisierung.AusfuehrenAsync();

        var admin = await db.Mitarbeiter.SingleAsync();
        Assert.Equal("admin", admin.Benutzername);
        Assert.Null(admin.Design);
        Assert.True(admin.PasswortAendernErforderlich);
        Assert.Equal(new DateOnly(2026, 10, 21), admin.ErfassungAb); // Folgetag der Einrichtung
        Assert.Equal(TestDaten.Optionen().Abwesenheitsarten.Count, await db.Abwesenheitsarten.CountAsync());

        // Bestehende Personen: Erfassungsbeginn = erste Zeitbuchung (bzw. heute ohne Buchungen)
        var alt = TestDaten.Mitarbeiter("alt");
        var neu = TestDaten.Mitarbeiter("neu");
        db.Mitarbeiter.AddRange(alt, neu);
        await db.SaveChangesAsync();
        db.Zeitbuchungen.Add(new Zeitbuchung { MitarbeiterId = alt.Id, Datum = new DateOnly(2026, 3, 2), Beginn = new TimeOnly(8, 0), Ende = new TimeOnly(12, 0) });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Mitarbeiter\" DROP COLUMN \"ErfassungAb\"");
        db.ChangeTracker.Clear();

        await initialisierung.AusfuehrenAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(new DateOnly(2026, 3, 2), (await db.Mitarbeiter.SingleAsync(m => m.Benutzername == "alt")).ErfassungAb);
        Assert.Equal(new DateOnly(2026, 10, 20), (await db.Mitarbeiter.SingleAsync(m => m.Benutzername == "neu")).ErfassungAb);

        // Ein zweiter Lauf ist idempotent
        await initialisierung.AusfuehrenAsync();
        Assert.Equal(3, await db.Mitarbeiter.CountAsync());
    }
}
