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
    public string Design { get; private set; } = Benutzereinstellungen.DesignSystem;
    public string Sprache { get; private set; } = Benutzereinstellungen.Deutsch;

    public async Task OnGetAsync()
    {
        var ich = await _db.Mitarbeiter.AsNoTracking().FirstAsync(m => m.Id == BenutzerId, HttpContext.RequestAborted);
        Aktiv = DashboardKacheln.Lesen(ich.DashboardKacheln);
        Design = Benutzereinstellungen.GueltigesDesign(ich.Design) ?? Benutzereinstellungen.Design(HttpContext);
        Sprache = Benutzereinstellungen.GueltigeSprache(ich.Sprache) ?? Benutzereinstellungen.Sprache(HttpContext);
    }

    /// <summary>Darstellung und Sprache speichern.</summary>
    public async Task<IActionResult> OnPostDarstellungAsync(string? design, string? sprache)
    {
        var ich = await _db.Mitarbeiter.FirstAsync(m => m.Id == BenutzerId, HttpContext.RequestAborted);
        ich.Design = Benutzereinstellungen.GueltigesDesign(design) ?? Benutzereinstellungen.DesignSystem;
        ich.Sprache = Benutzereinstellungen.GueltigeSprache(sprache) ?? Benutzereinstellungen.Deutsch;
        await _db.SaveChangesAsync(HttpContext.RequestAborted);

        Benutzereinstellungen.Uebernehmen(HttpContext, ich.Design, ich.Sprache);
        // Die Meldung erscheint bereits in der neu gewählten Sprache
        Texte.Sprache = ich.Sprache;
        Erfolg(Texte.T("Darstellung und Sprache wurden gespeichert."));
        return RedirectToPage();
    }

    /// <summary>Schneller Wechsel zwischen hell und dunkel über die Kopfzeile.</summary>
    public async Task<IActionResult> OnPostDesignAsync(string? wert, string? zurueck)
    {
        var design = Benutzereinstellungen.GueltigesDesign(wert) ?? Benutzereinstellungen.DesignSystem;
        var ich = await _db.Mitarbeiter.FirstAsync(m => m.Id == BenutzerId, HttpContext.RequestAborted);
        ich.Design = design;
        await _db.SaveChangesAsync(HttpContext.RequestAborted);
        Benutzereinstellungen.Setzen(HttpContext, Benutzereinstellungen.DesignCookie, design);
        return LocalRedirect(Url.IsLocalUrl(zurueck) ? zurueck! : "/");
    }

    public async Task<IActionResult> OnPostAsync(List<string> kacheln)
    {
        var mitarbeiter = await _db.Mitarbeiter.FirstAsync(m => m.Id == BenutzerId, HttpContext.RequestAborted);
        mitarbeiter.DashboardKacheln = DashboardKacheln.Schreiben(kacheln);
        await _db.SaveChangesAsync(HttpContext.RequestAborted);
        Erfolg(Texte.T("Das Dashboard wurde angepasst."));
        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostStandardAsync()
    {
        var mitarbeiter = await _db.Mitarbeiter.FirstAsync(m => m.Id == BenutzerId, HttpContext.RequestAborted);
        mitarbeiter.DashboardKacheln = null;
        await _db.SaveChangesAsync(HttpContext.RequestAborted);
        Erfolg(Texte.T("Das Dashboard wurde auf den Standard zurückgesetzt."));
        return RedirectToPage("/Index");
    }
}
