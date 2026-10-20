using Gevitas.Zeiterfassung.Web.Data;
using Gevitas.Zeiterfassung.Web.Domain;
using Microsoft.EntityFrameworkCore;
using static Gevitas.Zeiterfassung.Web.Services.Texte;

namespace Gevitas.Zeiterfassung.Web.Services;

public sealed class MitarbeiterEingabe
{
    public int? Id { get; set; }
    public string Personalnummer { get; set; } = string.Empty;
    public string Benutzername { get; set; } = string.Empty;
    public string Vorname { get; set; } = string.Empty;
    public string Nachname { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Abteilung { get; set; }
    public Rolle Rolle { get; set; } = Rolle.Mitarbeiter;
    public int? VorgesetzterId { get; set; }
    public int? AbweichenderGenehmigerId { get; set; }
    public string SollProTag { get; set; } = "8:00";
    public int Jahresurlaub { get; set; } = 30;
    public DateOnly Eintrittsdatum { get; set; }

    /// <summary>Beim Anlegen: Zeiterfassung beginnt am Folgetag der Accounterstellung (Standard).</summary>
    public bool ErfassungAbFolgetag { get; set; } = true;

    /// <summary>Beginn der Zeiterfassung, wenn nicht der Folgetag gewählt ist (bzw. beim Bearbeiten).</summary>
    public DateOnly? ErfassungAb { get; set; }

    public bool Aktiv { get; set; } = true;

    /// <summary>Nur beim Anlegen oder Zurücksetzen.</summary>
    public string? NeuesPasswort { get; set; }
}

public sealed class MitarbeiterService
{
    private readonly ZeiterfassungDbContext _db;
    private readonly PasswortService _passwoerter;
    private readonly IAppUhr _uhr;
    private readonly BenachrichtigungsService _benachrichtigungen;

    public MitarbeiterService(ZeiterfassungDbContext db, PasswortService passwoerter, IAppUhr uhr, BenachrichtigungsService benachrichtigungen)
    {
        _db = db;
        _passwoerter = passwoerter;
        _uhr = uhr;
        _benachrichtigungen = benachrichtigungen;
    }

    /// <summary>
    /// Personen, die als disziplinarische Führungskraft bzw. abweichender Genehmiger gewählt werden können:
    /// aktive Personen mit der Rolle Führungskraft, Personal oder Administrator (ohne die Person selbst und ohne ihre Unterstellten).
    /// </summary>
    public async Task<List<Mitarbeiter>> MoeglicheFuehrungskraefteAsync(int? fuerMitarbeiterId, CancellationToken ct = default)
    {
        var ausgeschlossen = new HashSet<int>();
        if (fuerMitarbeiterId is int id)
        {
            ausgeschlossen.Add(id);
            ausgeschlossen.UnionWith((await UnterstellteAsync(id, ct)).Select(m => m.Id));
        }

        return (await _db.Mitarbeiter.AsNoTracking()
                .Where(m => m.Aktiv && m.Rolle >= Rolle.Fuehrungskraft)
                .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
                .ToListAsync(ct))
            .Where(m => !ausgeschlossen.Contains(m.Id))
            .ToList();
    }

    public Task<Mitarbeiter?> LadenAsync(int id, CancellationToken ct = default) =>
        _db.Mitarbeiter.Include(m => m.Vorgesetzter).Include(m => m.AbweichenderGenehmiger).FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<List<Mitarbeiter>> AlleAsync(bool nurAktive = false, CancellationToken ct = default) =>
        _db.Mitarbeiter
            .Include(m => m.Vorgesetzter)
            .Where(m => !nurAktive || m.Aktiv)
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .ToListAsync(ct);

    /// <summary>Alle direkt und indirekt unterstellten aktiven Mitarbeitenden einer Führungskraft.</summary>
    public async Task<List<Mitarbeiter>> UnterstellteAsync(int fuehrungskraftId, CancellationToken ct = default)
    {
        var alle = await _db.Mitarbeiter.Where(m => m.Aktiv).ToListAsync(ct);
        var ergebnis = new List<Mitarbeiter>();
        var besucht = new HashSet<int> { fuehrungskraftId };
        var warteschlange = new Queue<int>();
        warteschlange.Enqueue(fuehrungskraftId);

        while (warteschlange.Count > 0)
        {
            var chef = warteschlange.Dequeue();
            foreach (var m in alle.Where(x => x.VorgesetzterId == chef))
            {
                if (besucht.Add(m.Id))
                {
                    ergebnis.Add(m);
                    warteschlange.Enqueue(m.Id);
                }
            }
        }

        return ergebnis.OrderBy(m => m.Nachname).ThenBy(m => m.Vorname).ToList();
    }

    /// <summary>
    /// Mitarbeitende, deren Daten der Benutzer sehen darf: Personal/Admin alle, Führungskräfte ihr Team, sonst nur sich selbst.
    /// </summary>
    public async Task<List<Mitarbeiter>> SichtbareAsync(int benutzerId, Rolle rolle, CancellationToken ct = default)
    {
        if (rolle >= Rolle.Personal)
        {
            return await AlleAsync(nurAktive: true, ct);
        }

        var selbst = await _db.Mitarbeiter.FirstAsync(m => m.Id == benutzerId, ct);
        var liste = new List<Mitarbeiter> { selbst };
        if (rolle >= Rolle.Fuehrungskraft)
        {
            liste.AddRange(await UnterstellteAsync(benutzerId, ct));
        }

        return liste;
    }

    public async Task<bool> DarfSehenAsync(int benutzerId, Rolle rolle, int mitarbeiterId, CancellationToken ct = default)
    {
        if (benutzerId == mitarbeiterId || rolle >= Rolle.Personal)
        {
            return true;
        }

        if (rolle < Rolle.Fuehrungskraft)
        {
            return false;
        }

        return (await UnterstellteAsync(benutzerId, ct)).Any(m => m.Id == mitarbeiterId);
    }

    /// <summary>Legt eine Person an oder speichert ihre Stammdaten – inklusive aller fachlichen und Berechtigungsprüfungen.</summary>
    /// <param name="ausfuehrenderId">Angemeldete Person, die speichert.</param>
    /// <param name="ausfuehrendeRolle">Rolle der angemeldeten Person.</param>
    public async Task<Ergebnis> SpeichernAsync(MitarbeiterEingabe e, int ausfuehrenderId, Rolle ausfuehrendeRolle, CancellationToken ct = default)
    {
        var benutzername = e.Benutzername.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(benutzername) || string.IsNullOrWhiteSpace(e.Personalnummer)
            || string.IsNullOrWhiteSpace(e.Vorname) || string.IsNullOrWhiteSpace(e.Nachname))
        {
            return Ergebnis.Fehler(T("Personalnummer, Benutzername, Vor- und Nachname sind Pflichtfelder."));
        }

        if (!Zeitformat.TryParseDauer(e.SollProTag, out var sollMinuten) || sollMinuten < 0 || sollMinuten > 24 * 60)
        {
            return Ergebnis.Fehler(T("Bitte die Sollarbeitszeit im Format Std:Min angeben (z. B. 8:00)."));
        }

        if (e.Jahresurlaub is < 0 or > 60)
        {
            return Ergebnis.Fehler(T("Der Jahresurlaub muss zwischen 0 und 60 Tagen liegen."));
        }

        if (e.Eintrittsdatum.Year < 1950 || e.Eintrittsdatum > _uhr.Heute.AddYears(2))
        {
            return Ergebnis.Fehler(T("Bitte ein gültiges Eintrittsdatum angeben."));
        }

        var bestehend = e.Id is null ? null : await _db.Mitarbeiter.AsNoTracking().FirstOrDefaultAsync(m => m.Id == e.Id, ct);
        if (e.Id is not null && bestehend is null)
        {
            return Ergebnis.Fehler(T("Die Person wurde nicht gefunden."));
        }

        // ---------- Berechtigungen ----------
        if (ausfuehrendeRolle < Rolle.Administrator && (e.Rolle == Rolle.Administrator || bestehend?.Rolle == Rolle.Administrator))
        {
            return Ergebnis.Fehler(T("Nur Administratoren dürfen Administratoren anlegen, bearbeiten oder die Rolle Administrator vergeben."));
        }

        if (bestehend is not null && bestehend.Id == ausfuehrenderId)
        {
            if (e.Rolle != bestehend.Rolle)
            {
                return Ergebnis.Fehler(T("Die eigene Rolle kann nicht geändert werden. Bitte eine andere berechtigte Person darum bitten."));
            }

            if (!e.Aktiv)
            {
                return Ergebnis.Fehler(T("Der eigene Zugang kann nicht deaktiviert werden."));
            }
        }

        // ---------- Eindeutigkeit ----------
        if (await _db.Mitarbeiter.AnyAsync(m => m.Id != e.Id && m.Benutzername == benutzername, ct))
        {
            return Ergebnis.Fehler(T("Der Benutzername ist bereits vergeben."));
        }

        if (await _db.Mitarbeiter.AnyAsync(m => m.Id != e.Id && m.Personalnummer == e.Personalnummer.Trim(), ct))
        {
            return Ergebnis.Fehler(T("Die Personalnummer ist bereits vergeben."));
        }

        // ---------- Führungskraft und Genehmiger ----------
        if (e.Id is not null && (e.VorgesetzterId == e.Id || e.AbweichenderGenehmigerId == e.Id))
        {
            return Ergebnis.Fehler(T("Eine Person kann nicht ihre eigene Führungskraft bzw. Genehmigerin sein."));
        }

        var unterstellte = e.Id is int eigeneId ? await UnterstellteAsync(eigeneId, ct) : new List<Mitarbeiter>();
        if (e.VorgesetzterId is int fkId)
        {
            var fehler = await FuehrungskraftPruefenAsync(fkId, unterstellte, ct);
            if (fehler is not null)
            {
                return Ergebnis.Fehler(F("Disziplinarische Führungskraft: {0}", fehler));
            }
        }

        if (e.AbweichenderGenehmigerId is int genehmigerId)
        {
            var fehler = await FuehrungskraftPruefenAsync(genehmigerId, unterstellte, ct);
            if (fehler is not null)
            {
                return Ergebnis.Fehler(F("Abweichender Genehmiger: {0}", fehler));
            }
        }

        // ---------- Herabstufen oder Deaktivieren einer Führungskraft ----------
        if (bestehend is not null && (e.Rolle < Rolle.Fuehrungskraft || !e.Aktiv))
        {
            var zugeordnet = await _db.Mitarbeiter.CountAsync(m => m.Aktiv && m.Id != bestehend.Id
                                                                 && (m.VorgesetzterId == bestehend.Id || m.AbweichenderGenehmigerId == bestehend.Id), ct);
            if (zugeordnet > 0)
            {
                return Ergebnis.Fehler(F("{0} ist noch für {1} Person(en) als Führungskraft bzw. Genehmiger hinterlegt. Bitte diesen Personen zuerst eine andere Führungskraft zuweisen.", bestehend.VollerName, zugeordnet));
            }

            var offen = await _db.Antraege.CountAsync(a => a.Status == AntragsStatus.Offen && a.GenehmigerId == bestehend.Id, ct);
            if (offen > 0)
            {
                return Ergebnis.Fehler(F("{0} hat noch {1} offene(n) Antrag/Anträge zur Genehmigung. Bitte diese zuerst bearbeiten lassen.", bestehend.VollerName, offen));
            }
        }

        // ---------- Beginn der Zeiterfassung ----------
        DateOnly? erfassungAb;
        if (bestehend is null)
        {
            if (e.ErfassungAbFolgetag)
            {
                erfassungAb = _uhr.Heute.AddDays(1);
            }
            else if (e.ErfassungAb is null)
            {
                return Ergebnis.Fehler(T("Bitte angeben, ab welchem Tag die Zeit erfasst wird."));
            }
            else
            {
                erfassungAb = e.ErfassungAb;
            }
        }
        else
        {
            erfassungAb = e.ErfassungAb ?? bestehend.ErfassungAb;
        }

        if (erfassungAb is { } ab && (ab.Year < 2000 || ab > _uhr.Heute.AddYears(2)))
        {
            return Ergebnis.Fehler(T("Bitte ein gültiges Datum für den Beginn der Zeiterfassung angeben."));
        }

        Mitarbeiter mitarbeiter;
        if (bestehend is null)
        {
            var passwortFehler = _passwoerter.RichtlinieVerletzt(e.NeuesPasswort);
            if (passwortFehler is not null)
            {
                return Ergebnis.Fehler(T("Startpasswort: ") + passwortFehler);
            }

            mitarbeiter = new Mitarbeiter { PasswortAendernErforderlich = true };
            _db.Mitarbeiter.Add(mitarbeiter);
        }
        else
        {
            mitarbeiter = await _db.Mitarbeiter.FirstAsync(m => m.Id == bestehend.Id, ct);
        }

        mitarbeiter.Personalnummer = e.Personalnummer.Trim();
        mitarbeiter.Benutzername = benutzername;
        mitarbeiter.Vorname = e.Vorname.Trim();
        mitarbeiter.Nachname = e.Nachname.Trim();
        mitarbeiter.Email = e.Email.Trim();
        mitarbeiter.Abteilung = string.IsNullOrWhiteSpace(e.Abteilung) ? null : e.Abteilung.Trim();
        mitarbeiter.Rolle = e.Rolle;
        mitarbeiter.VorgesetzterId = e.VorgesetzterId;
        mitarbeiter.AbweichenderGenehmigerId = e.AbweichenderGenehmigerId;
        mitarbeiter.SollMinutenProTag = sollMinuten;
        mitarbeiter.Jahresurlaub = e.Jahresurlaub;
        mitarbeiter.Eintrittsdatum = e.Eintrittsdatum;
        mitarbeiter.ErfassungAb = erfassungAb;
        mitarbeiter.Aktiv = e.Aktiv;

        if (!string.IsNullOrEmpty(e.NeuesPasswort))
        {
            var passwortFehler = _passwoerter.RichtlinieVerletzt(e.NeuesPasswort);
            if (passwortFehler is not null)
            {
                return Ergebnis.Fehler(passwortFehler);
            }

            mitarbeiter.PasswortHash = _passwoerter.Hashen(mitarbeiter, e.NeuesPasswort);
            mitarbeiter.PasswortAendernErforderlich = true;
        }

        await _db.SaveChangesAsync(ct);
        await AenderungenMeldenAsync(bestehend, mitarbeiter, ct);
        await _db.SaveChangesAsync(ct);

        var meldung = F("{0} wurde gespeichert.", mitarbeiter.VollerName);
        if (bestehend is null)
        {
            meldung += " " + F("Die Zeiterfassung beginnt am {0}.", Zeitformat.Datum(mitarbeiter.Erfassungsbeginn));
        }

        return Ergebnis.Ok(meldung);
    }

    /// <returns>Fehlertext oder null, wenn die Person als Führungskraft/Genehmiger zulässig ist.</returns>
    private async Task<string?> FuehrungskraftPruefenAsync(int id, List<Mitarbeiter> unterstellte, CancellationToken ct)
    {
        var fk = await _db.Mitarbeiter.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
        if (fk is null)
        {
            return T("Die gewählte Person wurde nicht gefunden.");
        }

        if (unterstellte.Any(m => m.Id == id))
        {
            return F("{0} ist dieser Person unterstellt (Zirkelbezug).", fk.VollerName);
        }

        if (!fk.Aktiv)
        {
            return F("{0} ist nicht mehr aktiv.", fk.VollerName);
        }

        return fk.Rolle < Rolle.Fuehrungskraft
            ? F("{0} hat die Rolle „{1}“. Es können nur Personen mit der Rolle Führungskraft, Personal oder Administrator gewählt werden.", fk.VollerName, fk.Rolle.Text())
            : null;
    }

    /// <summary>Informiert die betroffenen Personen über einen neuen Zugang bzw. eine neue Führungskraft.</summary>
    private async Task AenderungenMeldenAsync(Mitarbeiter? vorher, Mitarbeiter nachher, CancellationToken ct)
    {
        const string link = "/MeineDaten";
        var fkName = await NameAsync(nachher.VorgesetzterId, ct);

        if (vorher is null)
        {
            var beginn = nachher.Erfassungsbeginn;
            await _benachrichtigungen.BenachrichtigenAsync(nachher.Id, "Willkommen",
                () => F("Ihr Zugang zur Zeiterfassung wurde eingerichtet. Ihre Zeiterfassung beginnt am {0}.", Zeitformat.Datum(beginn))
                      + (fkName is null ? string.Empty : " " + F("Ihre Führungskraft ist {0}.", fkName)),
                link, ct);
        }
        else if (vorher.VorgesetzterId != nachher.VorgesetzterId)
        {
            await _benachrichtigungen.BenachrichtigenAsync(nachher.Id, "Neue Führungskraft",
                () => fkName is null ? T("Für Sie ist keine disziplinarische Führungskraft mehr hinterlegt.") : F("Ihre disziplinarische Führungskraft ist jetzt {0}.", fkName),
                link, ct);
        }

        if (nachher.VorgesetzterId is int fk && vorher?.VorgesetzterId != fk)
        {
            await _benachrichtigungen.BenachrichtigenAsync(fk, "Neue Person im Team",
                () => F("{0} ist Ihnen jetzt disziplinarisch unterstellt. Sie genehmigen künftig ihre bzw. seine Anträge.", nachher.VollerName),
                "/Zeitwirtschaft/Gruppenkalender", ct);
        }

        if (nachher.AbweichenderGenehmigerId is int g && vorher?.AbweichenderGenehmigerId != g)
        {
            await _benachrichtigungen.BenachrichtigenAsync(g, "Vertretung übernommen",
                () => F("Sie genehmigen ab sofort als Vertretung die Anträge von {0}.", nachher.VollerName),
                AntragsService.LinkGenehmiger, ct);
        }
    }

    private async Task<string?> NameAsync(int? id, CancellationToken ct) =>
        id is null ? null : await _db.Mitarbeiter.AsNoTracking().Where(m => m.Id == id).Select(m => m.Vorname + " " + m.Nachname).FirstOrDefaultAsync(ct);

    public async Task<Ergebnis> PasswortAendernAsync(int id, string altesPasswort, string neuesPasswort, CancellationToken ct = default)
    {
        var mitarbeiter = await _db.Mitarbeiter.FirstAsync(m => m.Id == id, ct);
        if (!_passwoerter.Pruefen(mitarbeiter, altesPasswort))
        {
            return Ergebnis.Fehler(T("Das aktuelle Passwort ist nicht korrekt."));
        }

        if (altesPasswort == neuesPasswort)
        {
            return Ergebnis.Fehler(T("Das neue Passwort muss sich vom bisherigen unterscheiden."));
        }

        var fehler = _passwoerter.RichtlinieVerletzt(neuesPasswort);
        if (fehler is not null)
        {
            return Ergebnis.Fehler(fehler);
        }

        mitarbeiter.PasswortHash = _passwoerter.Hashen(mitarbeiter, neuesPasswort);
        mitarbeiter.PasswortAendernErforderlich = false;
        await _db.SaveChangesAsync(ct);
        return Ergebnis.Ok(T("Ihr Passwort wurde geändert."));
    }
}
