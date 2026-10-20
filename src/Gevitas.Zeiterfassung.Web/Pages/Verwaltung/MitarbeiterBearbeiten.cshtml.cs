using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Infrastructure;
using Gevitas.Zeiterfassung.Web.Options;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using static Gevitas.Zeiterfassung.Web.Services.Texte;

namespace Gevitas.Zeiterfassung.Web.Pages.Verwaltung;

public class MitarbeiterBearbeitenModel : AppSeite
{
    private readonly MitarbeiterService _mitarbeiter;
    private readonly AntragsService _antraege;
    private readonly ZeiterfassungOptions _optionen;
    private readonly IAppUhr _uhr;

    public MitarbeiterBearbeitenModel(MitarbeiterService mitarbeiter, AntragsService antraege, IOptions<ZeiterfassungOptions> optionen, IAppUhr uhr)
    {
        _mitarbeiter = mitarbeiter;
        _antraege = antraege;
        _optionen = optionen.Value;
        _uhr = uhr;
    }

    [BindProperty]
    public MitarbeiterEingabe Eingabe { get; set; } = new();

    /// <summary>Wählbare Führungskräfte bzw. Genehmiger (nur aktive Personen mit Führungsrolle).</summary>
    public List<Mitarbeiter> Auswahl { get; private set; } = new();

    /// <summary>Bisher gespeicherte, inzwischen unzulässige Zuordnungen (werden markiert angezeigt).</summary>
    public Dictionary<int, string> UngueltigeZuordnungen { get; private set; } = new();

    /// <summary>Darf die angemeldete Person die Rolle Administrator vergeben?</summary>
    public bool DarfAdministratorVergeben => BenutzerRolle >= Rolle.Administrator;

    /// <summary>Die angemeldete Person bearbeitet ihren eigenen Datensatz.</summary>
    public bool IstEigenerDatensatz => Eingabe.Id == BenutzerId;

    public DateOnly Folgetag => _uhr.Heute.AddDays(1);

    public bool IstNeu => Eingabe.Id is null;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is null)
        {
            Eingabe = new MitarbeiterEingabe
            {
                SollProTag = Zeitformat.Dauer(_optionen.Sollarbeitszeit.StandardMinutenProTag),
                Jahresurlaub = _optionen.Urlaub.StandardJahresurlaub,
                Eintrittsdatum = _uhr.Heute,
                ErfassungAbFolgetag = true
            };
        }
        else
        {
            var m = await _mitarbeiter.LadenAsync(id.Value, HttpContext.RequestAborted);
            if (m is null)
            {
                return NotFound();
            }

            if (m.Rolle == Rolle.Administrator && !DarfAdministratorVergeben)
            {
                Fehler(T("Nur Administratoren dürfen Administratoren anlegen, bearbeiten oder die Rolle Administrator vergeben."));
                return RedirectToPage("/Verwaltung/Mitarbeiter");
            }

            Eingabe = new MitarbeiterEingabe
            {
                Id = m.Id,
                Personalnummer = m.Personalnummer,
                Benutzername = m.Benutzername,
                Vorname = m.Vorname,
                Nachname = m.Nachname,
                Email = m.Email,
                Abteilung = m.Abteilung,
                Rolle = m.Rolle,
                VorgesetzterId = m.VorgesetzterId,
                AbweichenderGenehmigerId = m.AbweichenderGenehmigerId,
                SollProTag = Zeitformat.Dauer(m.SollMinutenProTag),
                Jahresurlaub = m.Jahresurlaub,
                Eintrittsdatum = m.Eintrittsdatum,
                ErfassungAbFolgetag = false,
                ErfassungAb = m.Erfassungsbeginn,
                Aktiv = m.Aktiv
            };
        }

        await AuswahlLadenAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var ct = HttpContext.RequestAborted;
        var ergebnis = await _mitarbeiter.SpeichernAsync(Eingabe, BenutzerId, BenutzerRolle, ct);
        if (!ergebnis.Erfolg)
        {
            ModelState.AddModelError(string.Empty, ergebnis.Meldung);
            await AuswahlLadenAsync();
            return Page();
        }

        // Offene Anträge an die (ggf. neue) Führungskraft bzw. Vertretung umhängen
        var meldung = ergebnis.Meldung;
        if (Eingabe.Id is int id)
        {
            var umgehaengt = await _antraege.GenehmigerAktualisierenAsync(id, ct);
            if (umgehaengt > 0)
            {
                meldung += " " + F("{0} offene(r) Antrag/Anträge wurde(n) an die neue Genehmigerin bzw. den neuen Genehmiger weitergeleitet.", umgehaengt);
            }
        }

        Erfolg(meldung);
        return RedirectToPage("/Verwaltung/Mitarbeiter");
    }

    private async Task AuswahlLadenAsync()
    {
        var ct = HttpContext.RequestAborted;
        Auswahl = await _mitarbeiter.MoeglicheFuehrungskraefteAsync(Eingabe.Id, ct);

        // Bisherige, nicht mehr zulässige Zuordnungen sichtbar machen, damit sie bewusst korrigiert werden
        foreach (var id in new[] { Eingabe.VorgesetzterId, Eingabe.AbweichenderGenehmigerId })
        {
            if (id is int x && Auswahl.All(m => m.Id != x) && !UngueltigeZuordnungen.ContainsKey(x)
                && await _mitarbeiter.LadenAsync(x, ct) is { } person)
            {
                UngueltigeZuordnungen[x] = person.AnzeigeName;
            }
        }
    }
}
