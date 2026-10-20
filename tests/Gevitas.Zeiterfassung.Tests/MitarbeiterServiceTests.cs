using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Options;
using Gevitas.Zeiterfassung.Web.Services;

namespace Gevitas.Zeiterfassung.Tests;

public class MitarbeiterServiceTests : IDisposable
{
    private readonly TestDatenbank _datenbank = new();
    private readonly PasswortService _passwoerter = new(TestDaten.Als(new AuthentifizierungOptions()));

    public void Dispose() => _datenbank.Dispose();

    [Fact]
    public async Task Unterstellte_werden_rekursiv_ermittelt()
    {
        using var db = _datenbank.NeuerKontext();
        var chef = TestDaten.Mitarbeiter("chef", Rolle.Fuehrungskraft);
        var teamleitung = TestDaten.Mitarbeiter("teamleitung", Rolle.Fuehrungskraft, chef);
        var ma = TestDaten.Mitarbeiter("ma", Rolle.Mitarbeiter, teamleitung);
        var fremd = TestDaten.Mitarbeiter("fremd");
        db.Mitarbeiter.AddRange(chef, teamleitung, ma, fremd);
        await db.SaveChangesAsync();

        var service = new MitarbeiterService(db, _passwoerter);
        var team = await service.UnterstellteAsync(chef.Id);

        Assert.Equal(new[] { "ma", "teamleitung" }, team.Select(m => m.Benutzername).OrderBy(n => n));
        Assert.True(await service.DarfSehenAsync(chef.Id, Rolle.Fuehrungskraft, ma.Id));
        Assert.False(await service.DarfSehenAsync(chef.Id, Rolle.Fuehrungskraft, fremd.Id));
        Assert.False(await service.DarfSehenAsync(ma.Id, Rolle.Mitarbeiter, chef.Id));
    }

    [Fact]
    public async Task Neuer_Mitarbeiter_braucht_gueltiges_Startpasswort()
    {
        using var db = _datenbank.NeuerKontext();
        var service = new MitarbeiterService(db, _passwoerter);
        var eingabe = new MitarbeiterEingabe
        {
            Personalnummer = "4711",
            Benutzername = "Neu.Person",
            Vorname = "Neu",
            Nachname = "Person",
            SollProTag = "7:30",
            Jahresurlaub = 28,
            Eintrittsdatum = new DateOnly(2026, 1, 1),
            NeuesPasswort = "kurz"
        };

        Assert.False((await service.SpeichernAsync(eingabe)).Erfolg);

        eingabe.NeuesPasswort = "Sicher2026";
        var ergebnis = await service.SpeichernAsync(eingabe);

        Assert.True(ergebnis.Erfolg, ergebnis.Meldung);
        var gespeichert = db.Mitarbeiter.Single(m => m.Personalnummer == "4711");
        Assert.Equal("neu.person", gespeichert.Benutzername);
        Assert.Equal(450, gespeichert.SollMinutenProTag);
        Assert.True(gespeichert.PasswortAendernErforderlich);
        Assert.True(_passwoerter.Pruefen(gespeichert, "Sicher2026"));
    }

    [Fact]
    public async Task Zirkelbezug_in_der_Hierarchie_wird_verhindert()
    {
        using var db = _datenbank.NeuerKontext();
        var chef = TestDaten.Mitarbeiter("chef", Rolle.Fuehrungskraft);
        var ma = TestDaten.Mitarbeiter("ma", Rolle.Mitarbeiter, chef);
        db.Mitarbeiter.AddRange(chef, ma);
        await db.SaveChangesAsync();

        var service = new MitarbeiterService(db, _passwoerter);
        var ergebnis = await service.SpeichernAsync(new MitarbeiterEingabe
        {
            Id = chef.Id,
            Personalnummer = chef.Personalnummer,
            Benutzername = chef.Benutzername,
            Vorname = chef.Vorname,
            Nachname = chef.Nachname,
            SollProTag = "8:00",
            Jahresurlaub = 30,
            Eintrittsdatum = chef.Eintrittsdatum,
            VorgesetzterId = ma.Id
        });

        Assert.False(ergebnis.Erfolg);
    }

    [Theory]
    [InlineData("Abcdefg1", true)]
    [InlineData("abcdefg1", false)]
    [InlineData("ABCDEFG1", false)]
    [InlineData("Abcdefgh", false)]
    [InlineData("Ab1", false)]
    public void Passwortrichtlinie_wird_geprueft(string passwort, bool gueltig)
    {
        Assert.Equal(gueltig, _passwoerter.RichtlinieVerletzt(passwort) is null);
    }
}
