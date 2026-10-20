using Gevitas.Zeiterfassung.Web.Infrastructure;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gevitas.Zeiterfassung.Web.Pages;

public class TerminalModel : AppSeite
{
    private readonly BuchungsService _buchungen;
    private readonly KontenService _konten;

    public TerminalModel(BuchungsService buchungen, KontenService konten)
    {
        _buchungen = buchungen;
        _konten = konten;
    }

    public Kontostand Stand { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        Stand = await _konten.KontostandAsync(BenutzerId, HttpContext.RequestAborted);
    }

    public async Task<IActionResult> OnPostAsync(TerminalAktion aktion, string? zurueck)
    {
        Melden(await _buchungen.TerminalAsync(BenutzerId, aktion, HttpContext.RequestAborted));
        return zurueck == "dashboard" ? RedirectToPage("/Index") : RedirectToPage();
    }
}
