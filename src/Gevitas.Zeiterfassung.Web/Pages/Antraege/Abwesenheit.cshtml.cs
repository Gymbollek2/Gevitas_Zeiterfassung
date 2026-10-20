using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Infrastructure;
using Gevitas.Zeiterfassung.Web.Options;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using static Gevitas.Zeiterfassung.Web.Services.Texte;

namespace Gevitas.Zeiterfassung.Web.Pages.Antraege;

public class AbwesenheitModel : AppSeite
{
    private readonly AntragsService _antraege;
    private readonly KalenderService _kalender;
    private readonly UrlaubService _urlaub;
    private readonly IAppUhr _uhr;

    public AbwesenheitModel(AntragsService antraege, KalenderService kalender, UrlaubService urlaub, IAppUhr uhr, IOptions<ZeiterfassungOptions> optionen)
    {
        _antraege = antraege;
        _kalender = kalender;
        _urlaub = urlaub;
        _uhr = uhr;
        Optionen = optionen.Value;
    }

    public ZeiterfassungOptions Optionen { get; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? Von { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? Bis { get; set; }

    [BindProperty]
    public string? Art { get; set; }

    [BindProperty]
    public string? Bemerkung { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Zurueck { get; set; }

    public List<Abwesenheitsart> Arten { get; private set; } = new();
    public UrlaubsUebersicht Urlaub { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        Von ??= _uhr.Heute;
        Bis ??= Von;
        await LadenAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Von is null || Bis is null || string.IsNullOrEmpty(Art))
        {
            ModelState.AddModelError(string.Empty, T("Bitte Zeitraum und Art der Abwesenheit angeben (Pflichtfelder)."));
            await LadenAsync();
            return Page();
        }

        var ergebnis = await _antraege.AbwesenheitBeantragenAsync(BenutzerId, Von.Value, Bis.Value, Art, Bemerkung, HttpContext.RequestAborted);
        if (!ergebnis.Erfolg)
        {
            ModelState.AddModelError(string.Empty, ergebnis.Meldung);
            await LadenAsync();
            return Page();
        }

        Melden(ergebnis);
        return Zurueck switch
        {
            "jahr" => RedirectToPage("/Zeitwirtschaft/Jahreskalender", new { jahr = Von.Value.Year }),
            "monat" => RedirectToPage("/Zeitwirtschaft/Monatskalender", new { monat = Von.Value.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture) }),
            _ => RedirectToPage("/Antraege/Index", new { sicht = "Antragsteller", filter = "Offen" })
        };
    }

    private async Task LadenAsync()
    {
        Arten = await _kalender.ArtenAsync(HttpContext.RequestAborted);
        Urlaub = await _urlaub.UebersichtAsync(BenutzerId, ct: HttpContext.RequestAborted);
    }
}
