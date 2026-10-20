using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Infrastructure;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gevitas.Zeiterfassung.Web.Pages;

public class BenachrichtigungenModel : AppSeite
{
    private const int MaxAnzahl = 200;
    private readonly BenachrichtigungsService _benachrichtigungen;

    public BenachrichtigungenModel(BenachrichtigungsService benachrichtigungen)
    {
        _benachrichtigungen = benachrichtigungen;
    }

    public List<Benachrichtigung> Liste { get; private set; } = new();
    public int Ungelesen { get; private set; }

    public async Task OnGetAsync()
    {
        var ct = HttpContext.RequestAborted;
        Liste = await _benachrichtigungen.NeuesteAsync(BenutzerId, MaxAnzahl, ct);
        Ungelesen = await _benachrichtigungen.AnzahlUngelesenAsync(BenutzerId, ct);
    }

    /// <summary>Markiert die Benachrichtigung als gelesen und öffnet ihr Ziel (bzw. die vorherige Seite).</summary>
    public async Task<IActionResult> OnPostOeffnenAsync(int id, string? zurueck)
    {
        var b = await _benachrichtigungen.GelesenAsync(BenutzerId, id, HttpContext.RequestAborted);
        var ziel = b?.Link is { } link && Url.IsLocalUrl(link) ? link : zurueck;
        return LocalRedirect(Url.IsLocalUrl(ziel) ? ziel! : "/Benachrichtigungen");
    }

    public async Task<IActionResult> OnPostAlleGelesenAsync(string? zurueck)
    {
        await _benachrichtigungen.AlleGelesenAsync(BenutzerId, HttpContext.RequestAborted);
        return LocalRedirect(Url.IsLocalUrl(zurueck) ? zurueck! : "/Benachrichtigungen");
    }
}
