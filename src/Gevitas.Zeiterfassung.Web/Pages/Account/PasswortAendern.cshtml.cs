using System.ComponentModel.DataAnnotations;
using Gevitas.Zeiterfassung.Web.Infrastructure;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace Gevitas.Zeiterfassung.Web.Pages.Account;

public class PasswortAendernModel : AppSeite
{
    private readonly MitarbeiterService _mitarbeiter;

    public PasswortAendernModel(MitarbeiterService mitarbeiter)
    {
        _mitarbeiter = mitarbeiter;
    }

    [BindProperty]
    [Required(ErrorMessage = "Bitte das aktuelle Passwort eingeben.")]
    [DataType(DataType.Password)]
    public string AltesPasswort { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Bitte ein neues Passwort eingeben.")]
    [DataType(DataType.Password)]
    public string NeuesPasswort { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Bitte das neue Passwort wiederholen.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NeuesPasswort), ErrorMessage = "Die Passwörter stimmen nicht überein.")]
    public string Wiederholung { get; set; } = string.Empty;

    public bool Pflicht => User.HasClaim(Claimtypen.PasswortAendern, "1");

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
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
