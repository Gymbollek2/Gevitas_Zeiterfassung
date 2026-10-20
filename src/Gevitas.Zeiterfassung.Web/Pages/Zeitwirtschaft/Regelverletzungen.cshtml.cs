using System.Globalization;
using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Infrastructure;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gevitas.Zeiterfassung.Web.Pages.Zeitwirtschaft;

public class RegelverletzungenModel : AppSeite
{
    private readonly RegelverletzungService _regeln;
    private readonly MitarbeiterService _mitarbeiter;
    private readonly IAppUhr _uhr;

    public RegelverletzungenModel(RegelverletzungService regeln, MitarbeiterService mitarbeiter, IAppUhr uhr)
    {
        _regeln = regeln;
        _mitarbeiter = mitarbeiter;
        _uhr = uhr;
    }

    [BindProperty(SupportsGet = true)]
    public string? Monat { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Ma { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool MitQuittierten { get; set; }

    public DateOnly MonatsBeginn { get; private set; }
    public List<Mitarbeiter> Personen { get; private set; } = new();
    public Mitarbeiter Person { get; private set; } = null!;
    public List<Regelverletzung> Liste { get; private set; } = new();
    public bool IstEigene => Person.Id == BenutzerId;
    public string MonatText => MonatsBeginn.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    public async Task<IActionResult> OnGetAsync()
    {
        var ct = HttpContext.RequestAborted;
        Personen = await _mitarbeiter.SichtbareAsync(BenutzerId, BenutzerRolle, ct);
        var zielId = Ma ?? BenutzerId;
        if (!await _mitarbeiter.DarfSehenAsync(BenutzerId, BenutzerRolle, zielId, ct))
        {
            return Forbid();
        }

        Person = Personen.FirstOrDefault(p => p.Id == zielId) ?? (await _mitarbeiter.LadenAsync(zielId, ct))!;
        MonatsBeginn = !string.IsNullOrEmpty(Monat)
                       && DateOnly.TryParseExact(Monat + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var m)
            ? m
            : new DateOnly(_uhr.Heute.Year, _uhr.Heute.Month, 1);
        Liste = await _regeln.ErmittelnAsync(zielId, MonatsBeginn, MonatsBeginn.AddMonths(1).AddDays(-1), MitQuittierten, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostQuittierenAsync(DateOnly datum, string code, bool quittieren)
    {
        // Quittieren darf jede Person nur ihre eigenen Hinweise
        Melden(await _regeln.QuittierenAsync(BenutzerId, datum, code, quittieren, HttpContext.RequestAborted));
        return RedirectToPage(new { monat = Monat, mitQuittierten = MitQuittierten });
    }
}
