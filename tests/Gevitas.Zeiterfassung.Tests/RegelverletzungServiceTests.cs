using System.Text;
using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Services;

namespace Gevitas.Zeiterfassung.Tests;

public class RegelverletzungServiceTests : IDisposable
{
    private readonly TestDatenbank _datenbank = new();

    public void Dispose() => _datenbank.Dispose();

    [Fact]
    public async Task Verletzungen_werden_erkannt_und_koennen_quittiert_werden()
    {
        using var db = _datenbank.NeuerKontext();
        var ma = TestDaten.Mitarbeiter("ma");
        ma.Eintrittsdatum = new DateOnly(2026, 10, 12);
        db.Mitarbeiter.Add(ma);
        await db.SaveChangesAsync();

        // Mo 12.10.: bis 21:00 gearbeitet, Di 13.10.: schon um 06:00 begonnen (nur 9 Std. Ruhezeit)
        db.Zeitbuchungen.Add(new Zeitbuchung { MitarbeiterId = ma.Id, Datum = new DateOnly(2026, 10, 12), Beginn = new TimeOnly(12, 0), Ende = new TimeOnly(21, 0) });
        db.Zeitbuchungen.Add(new Zeitbuchung { MitarbeiterId = ma.Id, Datum = new DateOnly(2026, 10, 13), Beginn = new TimeOnly(6, 0), Ende = new TimeOnly(12, 0) });
        // Mi 14.10.: fehlende Gehen-Buchung
        db.Zeitbuchungen.Add(new Zeitbuchung { MitarbeiterId = ma.Id, Datum = new DateOnly(2026, 10, 14), Beginn = new TimeOnly(8, 0) });
        await db.SaveChangesAsync();

        var uhr = new FesteUhr(new DateTime(2026, 10, 16, 12, 0, 0));
        var optionen = TestDaten.Als(TestDaten.Optionen());
        var kalender = new Arbeitstagkalender(new FeiertagsService(optionen), optionen);
        var konten = new KontenService(db, kalender, new Tagesberechnung(ArbeitszeitRegeln.Standard), uhr, optionen);
        var service = new RegelverletzungService(db, konten, uhr, optionen);

        var liste = await service.ErmittelnAsync(ma.Id, new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 31), mitQuittierten: false);

        Assert.Contains(liste, r => r.Datum == new DateOnly(2026, 10, 12) && r.Code == Regelcodes.ArbeitNachRahmen);
        Assert.Contains(liste, r => r.Datum == new DateOnly(2026, 10, 13) && r.Code == Regelcodes.Ruhezeit);
        Assert.Contains(liste, r => r.Datum == new DateOnly(2026, 10, 14) && r.Code == Regelcodes.FehlendeGehenBuchung && !r.Quittierbar);
        Assert.Contains(liste, r => r.Datum == new DateOnly(2026, 10, 15) && r.Code == Regelcodes.FehltUnentschuldigt);
        Assert.DoesNotContain(liste, r => r.Datum >= new DateOnly(2026, 10, 16)); // heute und Zukunft werden nicht geprüft

        Assert.True((await service.QuittierenAsync(ma.Id, new DateOnly(2026, 10, 13), Regelcodes.Ruhezeit, true)).Erfolg);
        Assert.False((await service.QuittierenAsync(ma.Id, new DateOnly(2026, 10, 14), Regelcodes.FehlendeGehenBuchung, true)).Erfolg);

        var offen = await service.ErmittelnAsync(ma.Id, new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 31), mitQuittierten: false);
        var alle = await service.ErmittelnAsync(ma.Id, new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 31), mitQuittierten: true);
        Assert.DoesNotContain(offen, r => r.Code == Regelcodes.Ruhezeit);
        Assert.Contains(alle, r => r.Code == Regelcodes.Ruhezeit && r.Quittiert);
    }

    [Theory]
    [InlineData(21 * 60, 6 * 60, 540)]
    [InlineData(18 * 60, 8 * 60, 840)]
    public void Ruhezeit_wird_ueber_Mitternacht_berechnet(int ende, int beginn, int erwartet)
    {
        Assert.Equal(erwartet, RegelverletzungService.RuhezeitMinuten(ende, beginn));
    }

    [Fact]
    public void Csv_Export_maskiert_Sonderzeichen_und_hat_BOM()
    {
        var bericht = new BerichtDaten
        {
            Definition = BerichtService.Definition(500)!,
            Untertitel = "Test",
            Spalten = new[] { "A", "B" }
        };
        bericht.Zeilen.Add(new BerichtZeile(new[] { "x;y", "Zitat \"z\"" }));

        var bytes = BerichtService.AlsCsv(bericht);
        var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
        Assert.Contains("\"x;y\";\"Zitat \"\"z\"\"\"", text);
    }
}
