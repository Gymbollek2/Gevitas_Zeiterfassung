using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Infrastructure;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gevitas.Zeiterfassung.Web.Pages.Antraege;

public class IndexModel : AppSeite
{
    private readonly AntragsService _antraege;
    private readonly IAppUhr _uhr;

    public IndexModel(AntragsService antraege, IAppUhr uhr)
    {
        _antraege = antraege;
        _uhr = uhr;
    }

    [BindProperty(SupportsGet = true)]
    public AntragsSicht Sicht { get; set; } = AntragsSicht.Antragsteller;

    [BindProperty(SupportsGet = true)]
    public AntragsFilter Filter { get; set; } = AntragsFilter.Offen;

    [BindProperty(SupportsGet = true)]
    public DateOnly? Ab { get; set; }

    public List<Antrag> Liste { get; private set; } = new();

    public bool DarfGenehmigen => User.IstFuehrungskraft();

    public async Task OnGetAsync()
    {
        if (!DarfGenehmigen)
        {
            Sicht = AntragsSicht.Antragsteller;
        }

        Ab ??= new DateOnly(_uhr.Heute.Year, _uhr.Heute.Month, 1).AddMonths(-2);
        Liste = await _antraege.ListeAsync(BenutzerId, BenutzerRolle, Sicht, Filter, Ab.Value, HttpContext.RequestAborted);
    }

    public bool DarfEntscheiden(Antrag antrag) =>
        antrag.Status == AntragsStatus.Offen && AntragsService.DarfEntscheiden(antrag, BenutzerId, BenutzerRolle);

    public async Task<IActionResult> OnPostGenehmigenAsync(int antragId, string? bemerkung) =>
        await EntscheidenAsync(antragId, true, bemerkung);

    public async Task<IActionResult> OnPostAblehnenAsync(int antragId, string? bemerkung) =>
        await EntscheidenAsync(antragId, false, bemerkung);

    public async Task<IActionResult> OnPostZurueckziehenAsync(int antragId)
    {
        Melden(await _antraege.ZurueckziehenAsync(BenutzerId, antragId, HttpContext.RequestAborted));
        return RedirectToPage(new { sicht = Sicht, filter = Filter });
    }

    private async Task<IActionResult> EntscheidenAsync(int antragId, bool genehmigen, string? bemerkung)
    {
        Melden(await _antraege.EntscheidenAsync(BenutzerId, BenutzerRolle, antragId, genehmigen, bemerkung, HttpContext.RequestAborted));
        return RedirectToPage(new { sicht = AntragsSicht.Genehmiger, filter = Filter });
    }
}
