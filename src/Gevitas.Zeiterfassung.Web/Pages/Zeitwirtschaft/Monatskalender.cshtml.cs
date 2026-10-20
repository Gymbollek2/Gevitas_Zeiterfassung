using System.Globalization;
using Gevitas.Zeiterfassung.Web.Infrastructure;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gevitas.Zeiterfassung.Web.Pages.Zeitwirtschaft;

public class MonatskalenderModel : AppSeite
{
    private readonly KalenderService _kalender;
    private readonly IAppUhr _uhr;

    public MonatskalenderModel(KalenderService kalender, IAppUhr uhr)
    {
        _kalender = kalender;
        _uhr = uhr;
    }

    [BindProperty(SupportsGet = true)]
    public string? Monat { get; set; }

    public DateOnly MonatsBeginn { get; private set; }
    public DateOnly Heute => _uhr.Heute;

    /// <summary>Kalendertage einschließlich der angrenzenden Tage, damit volle Wochen (Mo–So) entstehen.</summary>
    public List<KalenderTag> Tage { get; private set; } = new();

    public async Task OnGetAsync()
    {
        MonatsBeginn = !string.IsNullOrEmpty(Monat)
                       && DateOnly.TryParseExact(Monat + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var m)
            ? m
            : new DateOnly(Heute.Year, Heute.Month, 1);

        var start = MonatsBeginn.AddDays(-(((int)MonatsBeginn.DayOfWeek + 6) % 7));
        var monatsEnde = MonatsBeginn.AddMonths(1).AddDays(-1);
        var ende = monatsEnde.AddDays((7 - (int)monatsEnde.DayOfWeek) % 7);
        Tage = await _kalender.TageAsync(BenutzerId, start, ende, HttpContext.RequestAborted);
    }

    public static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public static string MonatIso(DateOnly d) => d.ToString("yyyy-MM", CultureInfo.InvariantCulture);
}
