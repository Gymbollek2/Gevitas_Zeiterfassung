using Gevitas.Zeiterfassung.Web.Data;
using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace Gevitas.Zeiterfassung.Tests;

public class BuchungsServiceTests : IDisposable
{
    private readonly TestDatenbank _datenbank = new();
    private readonly FesteUhr _uhr = new(new DateTime(2026, 10, 20, 9, 15, 0));
    private readonly ZeiterfassungDbContext _db;
    private readonly BuchungsService _service;
    private readonly Mitarbeiter _ma;

    public BuchungsServiceTests()
    {
        _db = _datenbank.NeuerKontext();
        _ma = TestDaten.Mitarbeiter("ma");
        _db.Mitarbeiter.Add(_ma);
        _db.SaveChanges();
        _service = new BuchungsService(_db, _uhr, TestDaten.Als(TestDaten.Optionen()));
    }

    public void Dispose()
    {
        _db.Dispose();
        _datenbank.Dispose();
    }

    [Fact]
    public async Task Kommen_und_Gehen_ueber_das_Terminal()
    {
        Assert.True((await _service.TerminalAsync(_ma.Id, TerminalAktion.KommenOderGehen)).Erfolg);
        Assert.False((await _service.TerminalAsync(_ma.Id, TerminalAktion.Kommen)).Erfolg);

        _uhr.Jetzt = _uhr.Jetzt.AddHours(3);
        Assert.True((await _service.TerminalAsync(_ma.Id, TerminalAktion.KommenOderGehen)).Erfolg);

        var buchung = await _db.Zeitbuchungen.SingleAsync();
        Assert.Equal(new TimeOnly(9, 15), buchung.Beginn);
        Assert.Equal(new TimeOnly(12, 15), buchung.Ende);
        Assert.Equal(2, await _db.Buchungsprotokolle.CountAsync());
    }

    [Fact]
    public async Task Wechselbuchung_schliesst_und_oeffnet_neue_Zeile()
    {
        await _service.TerminalAsync(_ma.Id, TerminalAktion.Kommen);
        _uhr.Jetzt = _uhr.Jetzt.AddHours(1);
        var ergebnis = await _service.TerminalAsync(_ma.Id, TerminalAktion.Aendern);

        Assert.True(ergebnis.Erfolg);
        var buchungen = await _db.Zeitbuchungen.OrderBy(b => b.Beginn).ToListAsync();
        Assert.Equal(2, buchungen.Count);
        Assert.Equal(new TimeOnly(10, 15), buchungen[0].Ende);
        Assert.Null(buchungen[1].Ende);
    }

    [Fact]
    public async Task Krank_untertaegig_erfordert_vorherigen_Arbeitsbeginn()
    {
        Assert.False((await _service.TerminalAsync(_ma.Id, TerminalAktion.KrankUntertaegig)).Erfolg);

        await _service.TerminalAsync(_ma.Id, TerminalAktion.Kommen);
        _uhr.Jetzt = _uhr.Jetzt.AddMinutes(45);
        Assert.True((await _service.TerminalAsync(_ma.Id, TerminalAktion.KrankUntertaegig)).Erfolg);

        var krank = await _db.Zeitbuchungen.SingleAsync(b => b.Art == BuchungsArt.KrankUntertaegig);
        Assert.Null(krank.Ende);
        Assert.Equal(new TimeOnly(10, 0), krank.Beginn);
    }

    [Fact]
    public async Task Buchungen_in_der_Zukunft_sind_nicht_erlaubt()
    {
        var morgen = await _service.SpeichernAsync(_ma.Id, Eingabe(_uhr.Heute.AddDays(1), "08:00", "12:00"), _ma.Id, false);
        var spaeterHeute = await _service.SpeichernAsync(_ma.Id, Eingabe(_uhr.Heute, "08:00", "17:00"), _ma.Id, false);

        Assert.False(morgen.Erfolg);
        Assert.False(spaeterHeute.Erfolg);
    }

    [Fact]
    public async Task Maximal_neun_Tage_rueckwirkend_ausser_fuer_die_Personalabteilung()
    {
        var zehnTage = _uhr.Heute.AddDays(-10);

        Assert.False((await _service.SpeichernAsync(_ma.Id, Eingabe(zehnTage, "08:00", "12:00"), _ma.Id, false)).Erfolg);
        Assert.True((await _service.SpeichernAsync(_ma.Id, Eingabe(_uhr.Heute.AddDays(-9), "08:00", "12:00"), _ma.Id, false)).Erfolg);
        Assert.True((await _service.SpeichernAsync(_ma.Id, Eingabe(zehnTage, "08:00", "12:00"), _ma.Id, ohneZeitgrenze: true)).Erfolg);
    }

    [Fact]
    public async Task Ueberschneidungen_werden_abgelehnt()
    {
        var gestern = _uhr.Heute.AddDays(-1);
        Assert.True((await _service.SpeichernAsync(_ma.Id, Eingabe(gestern, "08:00", "12:00"), _ma.Id, false)).Erfolg);

        var ergebnis = await _service.SpeichernAsync(_ma.Id, Eingabe(gestern, "11:30", "15:00"), _ma.Id, false);

        Assert.False(ergebnis.Erfolg);
        Assert.Contains("überschneidet", ergebnis.Meldung);
    }

    [Fact]
    public async Task Korrektur_und_Loeschung_werden_im_Verlauf_protokolliert()
    {
        var gestern = _uhr.Heute.AddDays(-1);
        await _service.SpeichernAsync(_ma.Id, Eingabe(gestern, "15:00", "16:00"), _ma.Id, false);
        var buchung = await _db.Zeitbuchungen.SingleAsync();

        var korrektur = Eingabe(gestern, "15:00", "17:00");
        korrektur.Id = buchung.Id;
        korrektur.Bemerkung = "Vergessen auszustempeln";
        Assert.True((await _service.SpeichernAsync(_ma.Id, korrektur, _ma.Id, false)).Erfolg);
        Assert.True((await _service.LoeschenAsync(_ma.Id, buchung.Id, "doppelt", _ma.Id, false)).Erfolg);

        var verlauf = await _service.VerlaufAsync(_ma.Id, gestern);
        Assert.Equal(3, verlauf.Count);
        Assert.StartsWith("Hinzugefügt", verlauf[0].Details);
        Assert.Contains("15:00 – 16:00", verlauf[1].Details);
        Assert.Contains("15:00 – 17:00", verlauf[1].Details);
        Assert.Equal("Vergessen auszustempeln", verlauf[1].Bemerkung);
        Assert.StartsWith("Gelöscht", verlauf[2].Details);
    }

    [Fact]
    public void Krank_untertaegig_darf_kein_Ende_haben()
    {
        var e = Eingabe(_uhr.Heute.AddDays(-1), "10:00", "12:00");
        e.Art = BuchungsArt.KrankUntertaegig;

        Assert.NotNull(_service.Pruefen(e, false));
    }

    private static BuchungEingabe Eingabe(DateOnly datum, string beginn, string? ende) => new()
    {
        Datum = datum,
        Beginn = TimeOnly.Parse(beginn),
        Ende = ende is null ? null : TimeOnly.Parse(ende),
        Art = BuchungsArt.Arbeit
    };
}
