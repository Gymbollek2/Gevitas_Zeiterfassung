using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Infrastructure;
using Gevitas.Zeiterfassung.Web.Options;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Gevitas.Zeiterfassung.Web.Pages.Verwaltung;

public class MitarbeiterBearbeitenModel : AppSeite
{
    private readonly MitarbeiterService _mitarbeiter;
    private readonly ZeiterfassungOptions _optionen;
    private readonly IAppUhr _uhr;

    public MitarbeiterBearbeitenModel(MitarbeiterService mitarbeiter, IOptions<ZeiterfassungOptions> optionen, IAppUhr uhr)
    {
        _mitarbeiter = mitarbeiter;
        _optionen = optionen.Value;
        _uhr = uhr;
    }

    [BindProperty]
    public MitarbeiterEingabe Eingabe { get; set; } = new();

    public List<Mitarbeiter> Auswahl { get; private set; } = new();

    public bool IstNeu => Eingabe.Id is null;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is null)
        {
            Eingabe = new MitarbeiterEingabe
            {
                SollProTag = Zeitformat.Dauer(_optionen.Sollarbeitszeit.StandardMinutenProTag),
                Jahresurlaub = _optionen.Urlaub.StandardJahresurlaub,
                Eintrittsdatum = _uhr.Heute
            };
        }
        else
        {
            var m = await _mitarbeiter.LadenAsync(id.Value, HttpContext.RequestAborted);
            if (m is null)
            {
                return NotFound();
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
                Aktiv = m.Aktiv
            };
        }

        await AuswahlLadenAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Nur Administratoren dürfen Administratoren ernennen
        if (Eingabe.Rolle == Rolle.Administrator && BenutzerRolle < Rolle.Administrator)
        {
            Fehler("Nur Administratoren dürfen die Rolle Administrator vergeben.");
            await AuswahlLadenAsync();
            return Page();
        }

        var ergebnis = await _mitarbeiter.SpeichernAsync(Eingabe, HttpContext.RequestAborted);
        if (!ergebnis.Erfolg)
        {
            ModelState.AddModelError(string.Empty, ergebnis.Meldung);
            await AuswahlLadenAsync();
            return Page();
        }

        Melden(ergebnis);
        return RedirectToPage("/Verwaltung/Mitarbeiter");
    }

    private async Task AuswahlLadenAsync()
    {
        Auswahl = (await _mitarbeiter.AlleAsync(nurAktive: true, HttpContext.RequestAborted))
            .Where(m => m.Id != Eingabe.Id)
            .ToList();
    }
}
