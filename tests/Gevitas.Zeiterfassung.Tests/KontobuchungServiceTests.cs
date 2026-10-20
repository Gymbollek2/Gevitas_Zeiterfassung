using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace Gevitas.Zeiterfassung.Tests;

public class KontobuchungServiceTests : IDisposable
{
    private readonly TestDatenbank _datenbank = new();

    public void Dispose() => _datenbank.Dispose();

    [Fact]
    public async Task Umbuchung_erzeugt_Gegenbuchungen_und_kann_storniert_werden()
    {
        using var db = _datenbank.NeuerKontext();
        var ma = TestDaten.Mitarbeiter("ma");
        db.Mitarbeiter.Add(ma);
        await db.SaveChangesAsync();
        var service = new KontobuchungService(db, new FesteUhr(new DateTime(2026, 10, 20, 8, 0, 0)));

        var ergebnis = await service.UmbuchenAsync(ma.Id, Konto.Wegezeit, "2:30", new DateOnly(2026, 10, 19), null, ma.Id);

        Assert.True(ergebnis.Erfolg, ergebnis.Meldung);
        var buchungen = await db.Kontobuchungen.ToListAsync();
        Assert.Equal(-150, buchungen.Single(b => b.Konto == Konto.Wegezeit).Minuten);
        Assert.Equal(150, buchungen.Single(b => b.Konto == Konto.Saldo).Minuten);
        Assert.Single(buchungen.Select(b => b.Vorgang).Distinct());

        Assert.True((await service.StornierenAsync(buchungen[0].Vorgang)).Erfolg);
        Assert.All(await db.Kontobuchungen.ToListAsync(), b => Assert.True(b.Storniert));
    }

    [Theory]
    [InlineData(Konto.Saldo, "1:00")]
    [InlineData(Konto.Wegezeit, "0:00")]
    [InlineData(Konto.Wegezeit, "abc")]
    public async Task Ungueltige_Umbuchungen_werden_abgelehnt(Konto quelle, string wert)
    {
        using var db = _datenbank.NeuerKontext();
        var service = new KontobuchungService(db, new FesteUhr(new DateTime(2026, 10, 20, 8, 0, 0)));

        Assert.False((await service.UmbuchenAsync(1, quelle, wert, new DateOnly(2026, 10, 19), null, 1)).Erfolg);
    }
}
