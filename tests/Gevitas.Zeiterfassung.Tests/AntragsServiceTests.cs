using Gevitas.Zeiterfassung.Web.Data;
using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace Gevitas.Zeiterfassung.Tests;

public sealed class KeineEmails : IEmailVersand
{
    public List<string> Gesendet { get; } = new();

    public Task SendenAsync(string empfaenger, string betreff, string text, CancellationToken ct = default)
    {
        Gesendet.Add($"{empfaenger}: {betreff}");
        return Task.CompletedTask;
    }
}

public class AntragsServiceTests : IDisposable
{
    private readonly TestDatenbank _datenbank = new();
    private readonly FesteUhr _uhr = new(new DateTime(2026, 10, 20, 10, 0, 0));
    private readonly ZeiterfassungDbContext _db;
    private readonly Mitarbeiter _gbl;
    private readonly Mitarbeiter _chef;
    private readonly Mitarbeiter _ma;
    private readonly Mitarbeiter _personal;

    public AntragsServiceTests()
    {
        _db = _datenbank.NeuerKontext();
        foreach (var art in TestDaten.Optionen().Abwesenheitsarten)
        {
            _db.Abwesenheitsarten.Add(new Abwesenheitsart
            {
                Code = art.Code,
                Name = art.Name,
                Wirkung = Enum.Parse<AbwesenheitsWirkung>(art.Wirkung),
                ZaehltAlsUrlaub = art.ZaehltAlsUrlaub,
                MaxKalendertage = art.MaxKalendertage
            });
        }

        _gbl = TestDaten.Mitarbeiter("gbl", Rolle.Fuehrungskraft);
        _chef = TestDaten.Mitarbeiter("chef", Rolle.Fuehrungskraft, _gbl);
        _ma = TestDaten.Mitarbeiter("ma", Rolle.Mitarbeiter, _chef);
        _personal = TestDaten.Mitarbeiter("personal", Rolle.Personal);
        _db.Mitarbeiter.AddRange(_gbl, _chef, _ma, _personal);
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _datenbank.Dispose();
    }

    private AntragsService Service()
    {
        var optionen = TestDaten.Als(TestDaten.Optionen());
        var kalender = new Arbeitstagkalender(new FeiertagsService(optionen), optionen);
        var benachrichtigungen = new BenachrichtigungsService(_db, new KeineEmails(), _uhr, optionen);
        return new AntragsService(_db, _uhr, benachrichtigungen, new UrlaubService(_db, kalender, _uhr, optionen), optionen);
    }

    private async Task<Antrag> LetzterAntragAsync() => await _db.Antraege.OrderByDescending(a => a.Id).FirstAsync();

    [Fact]
    public async Task Urlaubsantrag_geht_an_die_Fuehrungskraft_und_wird_genehmigt()
    {
        var service = Service();

        var ergebnis = await service.AbwesenheitBeantragenAsync(_ma.Id, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 6), "U", "Herbsturlaub");
        Assert.True(ergebnis.Erfolg, ergebnis.Meldung);

        var antrag = await LetzterAntragAsync();
        Assert.Equal(_chef.Id, antrag.GenehmigerId);
        Assert.Equal(1, await _db.Benachrichtigungen.CountAsync(b => b.MitarbeiterId == _chef.Id));

        Assert.False((await service.EntscheidenAsync(_ma.Id, Rolle.Mitarbeiter, antrag.Id, true, null)).Erfolg);
        Assert.True((await service.EntscheidenAsync(_chef.Id, Rolle.Fuehrungskraft, antrag.Id, true, null)).Erfolg);

        var abwesenheit = await _db.Abwesenheiten.SingleAsync();
        Assert.Equal(AbwesenheitsStatus.Genehmigt, abwesenheit.Status);

        // Antragsteller: Bestätigung der Antragstellung und Entscheidung
        var titel = await _db.Benachrichtigungen.Where(b => b.MitarbeiterId == _ma.Id).OrderBy(b => b.Id).Select(b => b.Titel).ToListAsync();
        Assert.Equal(new[] { "Antrag gestellt", "Antrag genehmigt" }, titel);
        var genehmigt = await _db.Benachrichtigungen.Where(b => b.MitarbeiterId == _ma.Id).OrderBy(b => b.Id).LastAsync();
        Assert.Contains("Urlaub", genehmigt.Text);
        Assert.Contains("chef Test", genehmigt.Text);
    }

    [Fact]
    public async Task Benachrichtigung_wird_in_der_Sprache_des_Empfaengers_erstellt()
    {
        _chef.Sprache = "en";
        await _db.SaveChangesAsync();

        await Service().AbwesenheitBeantragenAsync(_ma.Id, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 3), "U", null);

        var anChef = await _db.Benachrichtigungen.SingleAsync(b => b.MitarbeiterId == _chef.Id);
        var anMa = await _db.Benachrichtigungen.SingleAsync(b => b.MitarbeiterId == _ma.Id);
        Assert.Equal("Neuer Antrag zur Genehmigung", anChef.Titel); // Titel als Schlüssel, Übersetzung bei der Anzeige
        Assert.Contains("Absence: Leave 02/11/2026", anChef.Text);
        Assert.Contains("Abwesenheit: Urlaub 02.11.2026", anMa.Text);
        Assert.Equal("de", Texte.Sprache);
    }

    [Fact]
    public async Task Zurueckgezogener_Antrag_wird_dem_Genehmiger_gemeldet()
    {
        var service = Service();
        await service.AbwesenheitBeantragenAsync(_ma.Id, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 3), "U", null);
        var antrag = await LetzterAntragAsync();

        Assert.True((await service.ZurueckziehenAsync(_ma.Id, antrag.Id)).Erfolg);

        Assert.True(await _db.Benachrichtigungen.AnyAsync(b => b.MitarbeiterId == _chef.Id && b.Titel == "Antrag zurückgezogen"));
    }

    [Fact]
    public async Task Offene_Antraege_gehen_nach_Wechsel_der_Fuehrungskraft_an_die_neue()
    {
        var service = Service();
        await service.AbwesenheitBeantragenAsync(_ma.Id, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 3), "U", null);
        _ma.VorgesetzterId = _gbl.Id;
        await _db.SaveChangesAsync();

        Assert.Equal(1, await service.GenehmigerAktualisierenAsync(_ma.Id));

        Assert.Equal(_gbl.Id, (await LetzterAntragAsync()).GenehmigerId);
        Assert.True(await _db.Benachrichtigungen.AnyAsync(b => b.MitarbeiterId == _gbl.Id && b.Titel == "Neuer Antrag zur Genehmigung"));
        Assert.Equal(0, await service.GenehmigerAktualisierenAsync(_ma.Id));
    }

    [Fact]
    public async Task Bei_Abwesenheit_der_Fuehrungskraft_genehmigt_die_naechsthoehere()
    {
        _db.Abwesenheiten.Add(new Abwesenheit
        {
            MitarbeiterId = _chef.Id, ArtCode = "U", Von = _uhr.Heute.AddDays(-1), Bis = _uhr.Heute.AddDays(3), Status = AbwesenheitsStatus.Genehmigt
        });
        await _db.SaveChangesAsync();

        await Service().AbwesenheitBeantragenAsync(_ma.Id, new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 9), "U", null);

        Assert.Equal(_gbl.Id, (await LetzterAntragAsync()).GenehmigerId);
    }

    [Fact]
    public async Task Abweichender_Genehmiger_hat_Vorrang()
    {
        _ma.AbweichenderGenehmigerId = _personal.Id;
        await _db.SaveChangesAsync();

        await Service().AbwesenheitBeantragenAsync(_ma.Id, new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 9), "GZ", null);

        Assert.Equal(_personal.Id, (await LetzterAntragAsync()).GenehmigerId);
    }

    [Fact]
    public async Task Krank_ohne_Attest_hoechstens_drei_zusammenhaengende_Kalendertage()
    {
        var service = Service();

        Assert.False((await service.AbwesenheitBeantragenAsync(_ma.Id, new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 15), "KR", null)).Erfolg);
        Assert.True((await service.AbwesenheitBeantragenAsync(_ma.Id, new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 13), "KR", null)).Erfolg);

        var anschluss = await service.AbwesenheitBeantragenAsync(_ma.Id, new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 15), "KR", null);
        Assert.False(anschluss.Erfolg);
        Assert.Contains("eAU", anschluss.Meldung);

        Assert.True((await service.AbwesenheitBeantragenAsync(_ma.Id, new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 15), "KRE", null)).Erfolg);
    }

    [Fact]
    public async Task Ueberschneidende_Abwesenheiten_werden_abgelehnt()
    {
        var service = Service();
        await service.AbwesenheitBeantragenAsync(_ma.Id, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 6), "U", null);

        Assert.False((await service.AbwesenheitBeantragenAsync(_ma.Id, new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 10), "GZ", null)).Erfolg);
    }

    [Fact]
    public async Task Ablehnung_erfordert_eine_Begruendung()
    {
        var service = Service();
        await service.AbwesenheitBeantragenAsync(_ma.Id, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 2), "U", null);
        var antrag = await LetzterAntragAsync();

        Assert.False((await service.EntscheidenAsync(_chef.Id, Rolle.Fuehrungskraft, antrag.Id, false, " ")).Erfolg);
        Assert.True((await service.EntscheidenAsync(_chef.Id, Rolle.Fuehrungskraft, antrag.Id, false, "Projektphase")).Erfolg);
        Assert.Equal(AbwesenheitsStatus.Abgelehnt, (await _db.Abwesenheiten.SingleAsync()).Status);
    }

    [Fact]
    public async Task Genehmigte_Abwesenheit_wird_erst_nach_Loeschantrag_storniert()
    {
        var service = Service();
        await service.AbwesenheitBeantragenAsync(_ma.Id, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 2), "U", null);
        await service.EntscheidenAsync(_chef.Id, Rolle.Fuehrungskraft, (await LetzterAntragAsync()).Id, true, null);
        var abwesenheit = await _db.Abwesenheiten.SingleAsync();

        Assert.True((await service.AbwesenheitLoeschenAsync(_ma.Id, abwesenheit.Id, "doch gearbeitet")).Erfolg);
        Assert.Equal(AbwesenheitsStatus.Genehmigt, abwesenheit.Status);

        var loeschantrag = await LetzterAntragAsync();
        Assert.Equal(AntragsArt.AbwesenheitLoeschen, loeschantrag.Art);
        await service.EntscheidenAsync(_chef.Id, Rolle.Fuehrungskraft, loeschantrag.Id, true, null);

        Assert.Equal(AbwesenheitsStatus.Storniert, abwesenheit.Status);
    }

    [Fact]
    public async Task Offener_Antrag_kann_zurueckgezogen_werden()
    {
        var service = Service();
        await service.AbwesenheitBeantragenAsync(_ma.Id, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 2), "U", null);
        var antrag = await LetzterAntragAsync();

        Assert.True((await service.ZurueckziehenAsync(_ma.Id, antrag.Id)).Erfolg);
        Assert.Equal(AntragsStatus.Zurueckgezogen, antrag.Status);
        Assert.Equal(AbwesenheitsStatus.Zurueckgezogen, (await _db.Abwesenheiten.SingleAsync()).Status);
        Assert.Equal(0, await service.OffeneZurGenehmigungAsync(_chef.Id, Rolle.Fuehrungskraft));
    }

    [Fact]
    public async Task Freigabe_ueber_zehn_Stunden_wirkt_sofort_und_wird_bei_Ablehnung_entzogen()
    {
        var service = Service();
        var gestern = _uhr.Heute.AddDays(-1);

        Assert.True((await service.TagesfreigabeBeantragenAsync(_ma.Id, gestern, ueber10: true, vorRahmen: false, nachRahmen: false, "Release")).Erfolg);
        var berechtigung = await _db.Tagesberechtigungen.SingleAsync();
        Assert.True(berechtigung.ArbeitUeber10Stunden);

        await service.EntscheidenAsync(_chef.Id, Rolle.Fuehrungskraft, (await LetzterAntragAsync()).Id, false, "Nicht abgestimmt");

        Assert.False(berechtigung.ArbeitUeber10Stunden);
    }

    [Fact]
    public async Task Urlaubsuebertrag_ist_zweistufig_und_nur_im_Antragszeitraum_moeglich()
    {
        var service = Service();
        Assert.False((await service.UrlaubsuebertragBeantragenAsync(_ma.Id, null)).Erfolg);

        _uhr.Jetzt = new DateTime(2026, 2, 10, 9, 0, 0);
        Assert.True((await service.UrlaubsuebertragBeantragenAsync(_ma.Id, "Projektabschluss")).Erfolg);
        var antrag = await LetzterAntragAsync();
        Assert.Equal(2, antrag.Stufen);
        Assert.Equal(_chef.Id, antrag.GenehmigerId);

        await service.EntscheidenAsync(_chef.Id, Rolle.Fuehrungskraft, antrag.Id, true, null);
        Assert.Equal(AntragsStatus.Offen, antrag.Status);
        Assert.Equal(2, antrag.Stufe);
        Assert.Equal(_gbl.Id, antrag.GenehmigerId);

        await service.EntscheidenAsync(_gbl.Id, Rolle.Fuehrungskraft, antrag.Id, true, null);
        Assert.Equal(AntragsStatus.Genehmigt, antrag.Status);
        Assert.True(await service.UrlaubsuebertragBeantragtAsync(_ma.Id, 2026));
    }

    [Fact]
    public async Task Ohne_Fuehrungskraft_entscheidet_die_Personalabteilung()
    {
        var service = Service();
        await service.AbwesenheitBeantragenAsync(_gbl.Id, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 2), "U", null);
        var antrag = await LetzterAntragAsync();

        Assert.Null(antrag.GenehmigerId);
        Assert.Equal(1, await service.OffeneZurGenehmigungAsync(_personal.Id, Rolle.Personal));
        Assert.True((await service.EntscheidenAsync(_personal.Id, Rolle.Personal, antrag.Id, true, null)).Erfolg);
    }
}
