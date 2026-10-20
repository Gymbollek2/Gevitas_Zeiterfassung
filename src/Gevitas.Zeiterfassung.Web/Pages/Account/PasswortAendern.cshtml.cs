using Gevitas.Zeiterfassung.Web.Infrastructure;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using static Gevitas.Zeiterfassung.Web.Services.Texte;

namespace Gevitas.Zeiterfassung.Web.Pages.Account;

public class PasswortAendernModel : AppSeite
{
    private readonly MitarbeiterService _mitarbeiter;

    public PasswortAendernModel(MitarbeiterService mitarbeiter)
    {
        _mitarbeiter = mitarbeiter;
    }

    [BindProperty]
    public string AltesPasswort { get; set; } = string.Empty;

    [BindProperty]
    public string NeuesPasswort { get; set; } = string.Empty;

    [BindProperty]
    public string Wiederholung { get; set; } = string.Empty;

    public bool Pflicht => User.HasClaim(Claimtypen.PasswortAendern, "1");

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrEmpty(AltesPasswort))
        {
            ModelState.AddModelError(nameof(AltesPasswort), T("Bitte das aktuelle Passwort eingeben."));
        }

        if (string.IsNullOrEmpty(NeuesPasswort))
        {
            ModelState.AddModelError(nameof(NeuesPasswort), T("Bitte ein neues Passwort eingeben."));
        }

        if (NeuesPasswort != Wiederholung)
        {
            ModelState.AddModelError(nameof(Wiederholung), T("Die Passwörter stimmen nicht überein."));
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var ergebnis = await _mitarbeiter.PasswortAendernAsync(BenutzerId, AltesPasswort, NeuesPasswort, HttpContext.RequestAborted);
        if (!ergebnis.Erfolg)
        {
            ModelState.AddModelError(string.Empty, ergebnis.Meldung);
            return Page();
        }

        // Anmeldung erneuern, damit die Pflicht zur Passwortänderung aus dem Cookie entfernt wird
        var mitarbeiter = await _mitarbeiter.LadenAsync(BenutzerId, HttpContext.RequestAborted);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, AnmeldeService.IdentitaetErzeugen(mitarbeiter!));

        Melden(ergebnis);
        return RedirectToPage("/Index");
    }
}
