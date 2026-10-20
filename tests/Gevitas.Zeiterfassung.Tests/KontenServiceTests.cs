using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Services;

namespace Gevitas.Zeiterfassung.Tests;

public class KontenServiceTests : IDisposable
{
    private readonly TestDatenbank _datenbank = new();

    public void Dispose() => _datenbank.Dispose();

    private KontenService Service(Web.Data.ZeiterfassungDbContext db, FesteUhr uhr)
    {
        var optionen = TestDaten.Als(TestDaten.Optionen());
        var kalender = new Arbeitstagkalender(new FeiertagsService(optionen), optionen);
        return new KontenService(db, kalender, new Tagesberechnung(ArbeitszeitRegeln.Standard), uhr, optionen);
    }

    [Fact]
    public async Task Saldo_wird_tageweise_fortgeschrieben_und_ist_minutenaktuell()
    {
        using var db = _datenbank.NeuerKontext();
        var ma = TestDaten.Mitarbeiter("ma");
        ma.Eintrittsdatum = new DateOnly(2026, 10, 19); // Montag
        db.Mitarbeiter.Add(ma);
        await db.SaveChangesAsync();

        db.Zeitbuchungen.Add(new Zeitbuchung { MitarbeiterId = ma.Id, Datum = new DateOnly(2026, 10, 19), Beginn = new TimeOnly(8, 0), Ende = new TimeOnly(16, 30) });
        db.Zeitbuchungen.Add(new Zeitbuchung { MitarbeiterId = ma.Id, Datum = new DateOnly(2026, 10, 20), Beginn = new TimeOnly(8, 0) });
        db.Kontobuchungen.Add(new Kontobuchung { MitarbeiterId = ma.Id, Datum = new DateOnly(2026, 10, 19), Konto = Konto.Saldo, Minuten = 60, Text = "Übernahme" });
        await db.SaveChangesAsync();

        var uhr = new FesteUhr(new DateTime(2026, 10, 20, 9, 15, 0));
        var service = Service(db, uhr);

        var tage = await service.AuswertenAsync(ma.Id, new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 21));
        Assert.Equal(3, tage.Count);
        Assert.Equal(480, tage[0].Ergebnis.ArbeitszeitMinuten); // 8:30 abzüglich 30 Min. Pause
        Assert.Equal(60, tage[0].SaldoKontostand);
        Assert.Equal(60 + 75 - 480, tage[1].SaldoKontostand);
        Assert.True(tage[2].IstZukunft);
        Assert.Equal(tage[1].SaldoKontostand, tage[2].SaldoKontostand);

        var stand = await service.KontostandAsync(ma.Id);
        Assert.True(stand.Anwesend);
        Assert.Equal(75, stand.HeuteArbeitszeit);
        Assert.Equal(-345, stand.Saldo);
    }

    [Fact]
    public async Task Wochenenden_und_Feiertage_haben_keine_Sollzeit()
    {
        using var db = _datenbank.NeuerKontext();
        var ma = TestDaten.Mitarbeiter("ma");
        ma.Eintrittsdatum = new DateOnly(2026, 9, 30);
        db.Mitarbeiter.Add(ma);
        await db.SaveChangesAsync();

        var service = Service(db, new FesteUhr(new DateTime(2026, 10, 20, 12, 0, 0)));
        var tage = await service.AuswertenAsync(ma.Id, new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 5));

        Assert.Equal(480, tage[0].Ergebnis.SollMinuten);   // Freitag
        Assert.Equal(0, tage[1].Ergebnis.SollMinuten);     // Samstag, 3.10. (Feiertag)
        Assert.Equal("Tag der Deutschen Einheit", tage[1].Feiertag);
        Assert.Equal(0, tage[2].Ergebnis.SollMinuten);     // Sonntag
        Assert.Equal(480, tage[3].Ergebnis.SollMinuten);   // Montag
    }
}
