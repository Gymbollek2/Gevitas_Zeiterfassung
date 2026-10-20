using Gevitas.Zeiterfassung.Web.Data;
using Gevitas.Zeiterfassung.Web.Infrastructure;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gevitas.Zeiterfassung.Web.Pages;

public class EinstellungenModel : AppSeite
{
    private readonly ZeiterfassungDbContext _db;

    public EinstellungenModel(ZeiterfassungDbContext db)
    {
        _db = db;
    }

    public IReadOnlySet<string> Aktiv { get; private set; } = DashboardKacheln.Standard;

    public async Task OnGetAsync()
    {
        var gespeichert = await _db.Mitarbeiter.Where(m => m.Id == BenutzerId).Select(m => m.DashboardKacheln).FirstOrDefaultAsync(HttpContext.RequestAborted);
        Aktiv = DashboardKacheln.Lesen(gespeichert);
    }

    public async Task<IActionResult> OnPostAsync(List<string> kacheln)
    {
        var mitarbeiter = await _db.Mitarbeiter.FirstAsync(m => m.Id == BenutzerId, HttpContext.RequestAborted);
        mitarbeiter.DashboardKacheln = DashboardKacheln.Schreiben(kacheln);
        await _db.SaveChangesAsync(HttpContext.RequestAborted);
        Erfolg("Das Dashboard wurde angepasst.");
        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostStandardAsync()
    {
        var mitarbeiter = await _db.Mitarbeiter.FirstAsync(m => m.Id == BenutzerId, HttpContext.RequestAborted);
        mitarbeiter.DashboardKacheln = null;
        await _db.SaveChangesAsync(HttpContext.RequestAborted);
        Erfolg("Das Dashboard wurde auf den Standard zurückgesetzt.");
        return RedirectToPage("/Index");
    }
}
