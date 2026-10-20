using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Infrastructure;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gevitas.Zeiterfassung.Web.Pages.Antraege;

public class AbwesenheitLoeschenModel : AppSeite
{
    private readonly AntragsService _antraege;
    private readonly KalenderService _kalender;
    private readonly IAppUhr _uhr;

    public AbwesenheitLoeschenModel(AntragsService antraege, KalenderService kalender, IAppUhr uhr)
    {
        _antraege = antraege;
        _kalender = kalender;
        _uhr = uhr;
    }

    [BindProperty(SupportsGet = true)]
    public DateOnly? Datum { get; set; }

    public List<Abwesenheit> Liste { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var heute = _uhr.Heute;
        var von = Datum ?? new DateOnly(heute.Year, 1, 1);
        var bis = Datum ?? new DateOnly(heute.Year + 1, 12, 31);
        Liste = await _kalender.AbwesenheitenAsync(new[] { BenutzerId }, von, bis, HttpContext.RequestAborted);
    }

    public async Task<IActionResult> OnPostAsync(int abwesenheitId, string? bemerkung)
    {
        Melden(await _antraege.AbwesenheitLoeschenAsync(BenutzerId, abwesenheitId, bemerkung, HttpContext.RequestAborted));
        return RedirectToPage("/Antraege/Index", new { sicht = "Antragsteller", filter = "Alle" });
    }
}
