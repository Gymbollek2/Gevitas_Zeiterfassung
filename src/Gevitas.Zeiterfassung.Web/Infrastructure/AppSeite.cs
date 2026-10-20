using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gevitas.Zeiterfassung.Web.Infrastructure;

/// <summary>Basisklasse für alle Seiten mit Zugriff auf den angemeldeten Benutzer und Meldungen.</summary>
public abstract class AppSeite : PageModel
{
    protected int BenutzerId => User.MitarbeiterId();
    protected Rolle BenutzerRolle => User.Rolle();

    protected void Melden(Ergebnis ergebnis)
    {
        TempData[ergebnis.Erfolg ? "Meldung" : "Fehler"] = ergebnis.Meldung;
    }

    protected void Erfolg(string text) => TempData["Meldung"] = text;
    protected void Fehler(string text) => TempData["Fehler"] = text;
}
