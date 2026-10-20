using Gevitas.Zeiterfassung.Web.Data;
using Gevitas.Zeiterfassung.Web.Domain;
using Microsoft.EntityFrameworkCore;

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
    public bool Aktiv { get; set; } = true;

    /// <summary>Nur beim Anlegen oder Zurücksetzen.</summary>
    public string? NeuesPasswort { get; set; }
}

public sealed class MitarbeiterService
{
    private readonly ZeiterfassungDbContext _db;
    private readonly PasswortService _passwoerter;

    public MitarbeiterService(ZeiterfassungDbContext db, PasswortService passwoerter)
    {
        _db = db;
        _passwoerter = passwoerter;
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

    public async Task<Ergebnis> SpeichernAsync(MitarbeiterEingabe e, CancellationToken ct = default)
    {
        var benutzername = e.Benutzername.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(benutzername) || string.IsNullOrWhiteSpace(e.Personalnummer)
            || string.IsNullOrWhiteSpace(e.Vorname) || string.IsNullOrWhiteSpace(e.Nachname))
        {
            return Ergebnis.Fehler("Personalnummer, Benutzername, Vor- und Nachname sind Pflichtfelder.");
        }

        if (!Zeitformat.TryParseDauer(e.SollProTag, out var sollMinuten) || sollMinuten < 0 || sollMinuten > 24 * 60)
        {
            return Ergebnis.Fehler("Bitte die Sollarbeitszeit im Format Std:Min angeben (z. B. 8:00).");
        }

        if (e.Jahresurlaub is < 0 or > 60)
        {
            return Ergebnis.Fehler("Der Jahresurlaub muss zwischen 0 und 60 Tagen liegen.");
        }

        if (await _db.Mitarbeiter.AnyAsync(m => m.Id != e.Id && m.Benutzername == benutzername, ct))
        {
            return Ergebnis.Fehler("Der Benutzername ist bereits vergeben.");
        }

        if (await _db.Mitarbeiter.AnyAsync(m => m.Id != e.Id && m.Personalnummer == e.Personalnummer.Trim(), ct))
        {
            return Ergebnis.Fehler("Die Personalnummer ist bereits vergeben.");
        }

        if (e.Id is not null && (e.VorgesetzterId == e.Id || e.AbweichenderGenehmigerId == e.Id))
        {
            return Ergebnis.Fehler("Eine Person kann nicht ihre eigene Führungskraft bzw. Genehmigerin sein.");
        }

        if (e.Id is not null && e.VorgesetzterId is not null
            && (await UnterstellteAsync(e.Id.Value, ct)).Any(m => m.Id == e.VorgesetzterId))
        {
            return Ergebnis.Fehler("Die gewählte Führungskraft ist dieser Person unterstellt (Zirkelbezug).");
        }

        Mitarbeiter mitarbeiter;
        if (e.Id is null)
        {
            var passwortFehler = _passwoerter.RichtlinieVerletzt(e.NeuesPasswort);
            if (passwortFehler is not null)
            {
                return Ergebnis.Fehler("Startpasswort: " + passwortFehler);
            }

            mitarbeiter = new Mitarbeiter { PasswortAendernErforderlich = true };
            _db.Mitarbeiter.Add(mitarbeiter);
        }
        else
        {
            mitarbeiter = await _db.Mitarbeiter.FirstOrDefaultAsync(m => m.Id == e.Id, ct)
                          ?? throw new InvalidOperationException("Mitarbeiter nicht gefunden.");
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
        return Ergebnis.Ok($"{mitarbeiter.VollerName} wurde gespeichert.");
    }

    public async Task<Ergebnis> PasswortAendernAsync(int id, string altesPasswort, string neuesPasswort, CancellationToken ct = default)
    {
        var mitarbeiter = await _db.Mitarbeiter.FirstAsync(m => m.Id == id, ct);
        if (!_passwoerter.Pruefen(mitarbeiter, altesPasswort))
        {
            return Ergebnis.Fehler("Das aktuelle Passwort ist nicht korrekt.");
        }

        if (altesPasswort == neuesPasswort)
        {
            return Ergebnis.Fehler("Das neue Passwort muss sich vom bisherigen unterscheiden.");
        }

        var fehler = _passwoerter.RichtlinieVerletzt(neuesPasswort);
        if (fehler is not null)
        {
            return Ergebnis.Fehler(fehler);
        }

        mitarbeiter.PasswortHash = _passwoerter.Hashen(mitarbeiter, neuesPasswort);
        mitarbeiter.PasswortAendernErforderlich = false;
        await _db.SaveChangesAsync(ct);
        return Ergebnis.Ok("Ihr Passwort wurde geändert.");
    }
}
