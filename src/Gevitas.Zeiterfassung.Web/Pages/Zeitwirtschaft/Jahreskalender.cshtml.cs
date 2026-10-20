using Gevitas.Zeiterfassung.Web.Infrastructure;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gevitas.Zeiterfassung.Web.Pages.Zeitwirtschaft;

public class JahreskalenderModel : AppSeite
{
    private readonly KalenderService _kalender;
    private readonly UrlaubService _urlaub;
    private readonly IAppUhr _uhr;

    public JahreskalenderModel(KalenderService kalender, UrlaubService urlaub, IAppUhr uhr)
    {
        _kalender = kalender;
        _urlaub = urlaub;
        _uhr = uhr;
    }

    [BindProperty(SupportsGet = true)]
    public int? Jahr { get; set; }

    public DateOnly Heute => _uhr.Heute;
    public Dictionary<DateOnly, KalenderTag> Tage { get; private set; } = new();
    public UrlaubsUebersicht Urlaub { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        Jahr ??= Heute.Year;
        var tage = await _kalender.TageAsync(BenutzerId, new DateOnly(Jahr.Value, 1, 1), new DateOnly(Jahr.Value, 12, 31), HttpContext.RequestAborted);
        Tage = tage.ToDictionary(t => t.Datum);
        Urlaub = await _urlaub.UebersichtAsync(BenutzerId, Jahr, HttpContext.RequestAborted);
    }
}
