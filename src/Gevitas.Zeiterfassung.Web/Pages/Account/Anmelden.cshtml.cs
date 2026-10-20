using Gevitas.Zeiterfassung.Web.Infrastructure;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static Gevitas.Zeiterfassung.Web.Services.Texte;

namespace Gevitas.Zeiterfassung.Web.Pages.Account;

public class AnmeldenModel : PageModel
{
    private readonly AnmeldeService _anmeldung;
    private readonly ILogger<AnmeldenModel> _log;

    public AnmeldenModel(AnmeldeService anmeldung, ILogger<AnmeldenModel> log)
    {
        _anmeldung = anmeldung;
        _log = log;
    }

    /// <summary>Benutzername oder Personalnummer.</summary>
    [BindProperty]
    public string Benutzername { get; set; } = string.Empty;

    [BindProperty]
    public string Passwort { get; set; } = string.Empty;

    [BindProperty]
    public bool AngemeldetBleiben { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }

        return Page();
    }

    /// <summary>Sprachwahl bereits vor der Anmeldung.</summary>
    public IActionResult OnGetSprache(string? wert)
    {
        if (Benutzereinstellungen.GueltigeSprache(wert) is { } sprache)
        {
            Benutzereinstellungen.Setzen(HttpContext, Benutzereinstellungen.SpracheCookie, sprache);
        }

        return RedirectToPage(new { returnUrl = ReturnUrl });
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Benutzername))
        {
            ModelState.AddModelError(nameof(Benutzername), T("Bitte Benutzername oder Personalnummer eingeben."));
        }

        if (string.IsNullOrEmpty(Passwort))
        {
            ModelState.AddModelError(nameof(Passwort), T("Bitte das Passwort eingeben."));
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var mitarbeiter = await _anmeldung.PruefenAsync(Benutzername, Passwort, HttpContext.RequestAborted);
        if (mitarbeiter is null)
        {
            _log.LogWarning("Fehlgeschlagene Anmeldung für Benutzer {Benutzer}.", Benutzername);
            ModelState.AddModelError(string.Empty, T("Benutzername/Personalnummer oder Passwort ist nicht korrekt."));
            Passwort = string.Empty;
            return Page();
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            AnmeldeService.IdentitaetErzeugen(mitarbeiter),
            new AuthenticationProperties { IsPersistent = AngemeldetBleiben });
        Benutzereinstellungen.Uebernehmen(HttpContext, mitarbeiter.Design, mitarbeiter.Sprache);

        if (mitarbeiter.PasswortAendernErforderlich)
        {
            return RedirectToPage("/Account/PasswortAendern");
        }

        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/");
    }
}
