using Gevitas.Zeiterfassung.Web.Data;
using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Options;
using Gevitas.Zeiterfassung.Web.Services;

namespace Gevitas.Zeiterfassung.Tests;

public class MitarbeiterServiceTests : IDisposable
{
    private readonly TestDatenbank _datenbank = new();
    private readonly PasswortService _passwoerter = new(TestDaten.Als(new AuthentifizierungOptions()));

    private readonly FesteUhr _uhr = new(new DateTime(2026, 10, 20, 9, 0, 0));

    public void Dispose() => _datenbank.Dispose();

    private MitarbeiterService Service(ZeiterfassungDbContext db) =>
        new(db, _passwoerter, _uhr, new BenachrichtigungsService(db, new KeineEmails(), _uhr, TestDaten.Als(TestDaten.Optionen())));

    private static MitarbeiterEingabe EingabeAus(Mitarbeiter m) => new()
    {
        Id = m.Id,
        Personalnummer = m.Personalnummer,
        Benutzername = m.Benutzername,
        Vorname = m.Vorname,
        Nachname = m.Nachname,
        Rolle = m.Rolle,
        VorgesetzterId = m.VorgesetzterId,
        AbweichenderGenehmigerId = m.AbweichenderGenehmigerId,
        SollProTag = "8:00",
        Jahresurlaub = 30,
        Eintrittsdatum = m.Eintrittsdatum,
        ErfassungAbFolgetag = false,
        ErfassungAb = m.Erfassungsbeginn,
        Aktiv = m.Aktiv
    };

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

        var service = Service(db);
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
        var service = Service(db);
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

        Assert.False((await service.SpeichernAsync(eingabe, 1, Rolle.Personal)).Erfolg);

        eingabe.NeuesPasswort = "Sicher2026";
        var ergebnis = await service.SpeichernAsync(eingabe, 1, Rolle.Personal);

        Assert.True(ergebnis.Erfolg, ergebnis.Meldung);
        var gespeichert = db.Mitarbeiter.Single(m => m.Personalnummer == "4711");
        Assert.Equal("neu.person", gespeichert.Benutzername);
        Assert.Equal(450, gespeichert.SollMinutenProTag);
        Assert.True(gespeichert.PasswortAendernErforderlich);
        Assert.True(_passwoerter.Pruefen(gespeichert, "Sicher2026"));

        // Standard: Zeiterfassung beginnt am Folgetag der Accounterstellung
        Assert.Equal(new DateOnly(2026, 10, 21), gespeichert.ErfassungAb);
        Assert.Equal(new DateOnly(2026, 10, 21), gespeichert.Erfassungsbeginn);
        Assert.True(db.Benachrichtigungen.Any(b => b.MitarbeiterId == gespeichert.Id && b.Titel == "Willkommen"));
    }

    [Fact]
    public async Task Zeiterfassung_kann_ab_einem_anderen_Tag_beginnen()
    {
        using var db = _datenbank.NeuerKontext();
        var service = Service(db);
        var eingabe = new MitarbeiterEingabe
        {
            Personalnummer = "4712",
            Benutzername = "spaeter",
            Vorname = "Sven",
            Nachname = "Später",
            SollProTag = "8:00",
            Eintrittsdatum = new DateOnly(2020, 4, 1),
            ErfassungAbFolgetag = false,
            NeuesPasswort = "Sicher2026"
        };

        Assert.False((await service.SpeichernAsync(eingabe, 1, Rolle.Personal)).Erfolg); // Datum fehlt

        eingabe.ErfassungAb = new DateOnly(2026, 11, 1);
        Assert.True((await service.SpeichernAsync(eingabe, 1, Rolle.Personal)).Erfolg);
        Assert.Equal(new DateOnly(2026, 11, 1), db.Mitarbeiter.Single(m => m.Personalnummer == "4712").Erfassungsbeginn);
    }

    [Fact]
    public async Task Nur_Fuehrungskraefte_koennen_als_Fuehrungskraft_gewaehlt_werden()
    {
        using var db = _datenbank.NeuerKontext();
        var chef = TestDaten.Mitarbeiter("chef", Rolle.Fuehrungskraft);
        var kollege = TestDaten.Mitarbeiter("kollege");
        var ma = TestDaten.Mitarbeiter("ma", Rolle.Mitarbeiter, chef);
        var personal = TestDaten.Mitarbeiter("personal", Rolle.Personal);
        db.Mitarbeiter.AddRange(chef, kollege, ma, personal);
        await db.SaveChangesAsync();
        var service = Service(db);

        var auswahl = await service.MoeglicheFuehrungskraefteAsync(ma.Id);
        Assert.Equal(new[] { "chef", "personal" }, auswahl.Select(m => m.Benutzername).OrderBy(n => n));

        var eingabe = EingabeAus(ma);
        eingabe.VorgesetzterId = kollege.Id;
        var ergebnis = await service.SpeichernAsync(eingabe, personal.Id, Rolle.Personal);
        Assert.False(ergebnis.Erfolg);
        Assert.Contains("Führungskraft", ergebnis.Meldung);

        eingabe.VorgesetzterId = chef.Id;
        eingabe.AbweichenderGenehmigerId = kollege.Id;
        Assert.False((await service.SpeichernAsync(eingabe, personal.Id, Rolle.Personal)).Erfolg);

        eingabe.AbweichenderGenehmigerId = personal.Id;
        Assert.True((await service.SpeichernAsync(eingabe, personal.Id, Rolle.Personal)).Erfolg);
        Assert.True(db.Benachrichtigungen.Any(b => b.MitarbeiterId == personal.Id && b.Titel == "Vertretung übernommen"));
    }

    [Fact]
    public async Task Fuehrungskraft_mit_Team_kann_nicht_herabgestuft_oder_deaktiviert_werden()
    {
        using var db = _datenbank.NeuerKontext();
        var chef = TestDaten.Mitarbeiter("chef", Rolle.Fuehrungskraft);
        var ma = TestDaten.Mitarbeiter("ma", Rolle.Mitarbeiter, chef);
        db.Mitarbeiter.AddRange(chef, ma);
        await db.SaveChangesAsync();
        var service = Service(db);

        var herabstufen = EingabeAus(chef);
        herabstufen.Rolle = Rolle.Mitarbeiter;
        Assert.False((await service.SpeichernAsync(herabstufen, 99, Rolle.Administrator)).Erfolg);

        var deaktivieren = EingabeAus(chef);
        deaktivieren.Aktiv = false;
        Assert.False((await service.SpeichernAsync(deaktivieren, 99, Rolle.Administrator)).Erfolg);

        // Nach dem Wechsel der Führungskraft ist die Herabstufung möglich
        var neueFk = TestDaten.Mitarbeiter("neu", Rolle.Fuehrungskraft);
        db.Mitarbeiter.Add(neueFk);
        await db.SaveChangesAsync();
        var umhaengen = EingabeAus(ma);
        umhaengen.VorgesetzterId = neueFk.Id;
        Assert.True((await service.SpeichernAsync(umhaengen, 99, Rolle.Administrator)).Erfolg);
        Assert.True(db.Benachrichtigungen.Any(b => b.MitarbeiterId == neueFk.Id && b.Titel == "Neue Person im Team"));
        Assert.True(db.Benachrichtigungen.Any(b => b.MitarbeiterId == ma.Id && b.Titel == "Neue Führungskraft"));
        Assert.True((await service.SpeichernAsync(herabstufen, 99, Rolle.Administrator)).Erfolg);
    }

    [Fact]
    public async Task Eigene_Rolle_und_Administratoren_sind_geschuetzt()
    {
        using var db = _datenbank.NeuerKontext();
        var admin = TestDaten.Mitarbeiter("admin", Rolle.Administrator);
        var personal = TestDaten.Mitarbeiter("personal", Rolle.Personal);
        db.Mitarbeiter.AddRange(admin, personal);
        await db.SaveChangesAsync();
        var service = Service(db);

        var selbstBefoerdern = EingabeAus(personal);
        selbstBefoerdern.Rolle = Rolle.Administrator;
        Assert.False((await service.SpeichernAsync(selbstBefoerdern, personal.Id, Rolle.Personal)).Erfolg);

        var selbstDeaktivieren = EingabeAus(admin);
        selbstDeaktivieren.Aktiv = false;
        Assert.False((await service.SpeichernAsync(selbstDeaktivieren, admin.Id, Rolle.Administrator)).Erfolg);

        // Personal darf Administratoren nicht bearbeiten (z. B. kein Passwort zurücksetzen)
        var adminAendern = EingabeAus(admin);
        adminAendern.NeuesPasswort = "Uebernahme2026";
        Assert.False((await service.SpeichernAsync(adminAendern, personal.Id, Rolle.Personal)).Erfolg);

        var herabstufen = EingabeAus(personal);
        herabstufen.Rolle = Rolle.Mitarbeiter;
        Assert.True((await service.SpeichernAsync(herabstufen, admin.Id, Rolle.Administrator)).Erfolg);
    }

    [Fact]
    public async Task Zirkelbezug_in_der_Hierarchie_wird_verhindert()
    {
        using var db = _datenbank.NeuerKontext();
        var chef = TestDaten.Mitarbeiter("chef", Rolle.Fuehrungskraft);
        var ma = TestDaten.Mitarbeiter("ma", Rolle.Mitarbeiter, chef);
        db.Mitarbeiter.AddRange(chef, ma);
        await db.SaveChangesAsync();

        var service = Service(db);
        var ergebnis = await service.SpeichernAsync(new MitarbeiterEingabe
        {
            ErfassungAbFolgetag = false,
            Id = chef.Id,
            Personalnummer = chef.Personalnummer,
            Benutzername = chef.Benutzername,
            Vorname = chef.Vorname,
            Nachname = chef.Nachname,
            SollProTag = "8:00",
            Jahresurlaub = 30,
            Eintrittsdatum = chef.Eintrittsdatum,
            VorgesetzterId = ma.Id
        }, 99, Rolle.Administrator);

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
