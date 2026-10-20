using System.Security.Claims;
using Gevitas.Zeiterfassung.Web.Data;
using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Options;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Gevitas.Zeiterfassung.Web.Services;

public static class Claimtypen
{
    public const string PasswortAendern = "gevitas:pw_aendern";
    public const string Hauptrolle = "gevitas:rolle";
}

public static class ClaimsPrincipalErweiterungen
{
    public static int MitarbeiterId(this ClaimsPrincipal benutzer) =>
        int.Parse(benutzer.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? throw new InvalidOperationException("Kein angemeldeter Benutzer."));

    public static Rolle Rolle(this ClaimsPrincipal benutzer) =>
        Enum.TryParse<Rolle>(benutzer.FindFirstValue(Claimtypen.Hauptrolle), out var rolle) ? rolle : Domain.Rolle.Mitarbeiter;

    public static bool IstFuehrungskraft(this ClaimsPrincipal benutzer) => benutzer.Rolle() >= Domain.Rolle.Fuehrungskraft;
    public static bool IstPersonal(this ClaimsPrincipal benutzer) => benutzer.Rolle() >= Domain.Rolle.Personal;
}

/// <summary>Kapselt Passwort-Hashing (PBKDF2 aus ASP.NET Core Identity).</summary>
public sealed class PasswortService
{
    private readonly PasswordHasher<Mitarbeiter> _hasher = new();
    private readonly AuthentifizierungOptions _optionen;

    public PasswortService(IOptions<AuthentifizierungOptions> optionen)
    {
        _optionen = optionen.Value;
    }

    public string Hashen(Mitarbeiter mitarbeiter, string passwort) => _hasher.HashPassword(mitarbeiter, passwort);

    public bool Pruefen(Mitarbeiter mitarbeiter, string passwort)
    {
        if (string.IsNullOrEmpty(mitarbeiter.PasswortHash))
        {
            return false;
        }

        return _hasher.VerifyHashedPassword(mitarbeiter, mitarbeiter.PasswortHash, passwort) != PasswordVerificationResult.Failed;
    }

    /// <summary>Prüft die Passwortrichtlinie. Liefert null, wenn das Passwort gültig ist, sonst die Fehlermeldung.</summary>
    public string? RichtlinieVerletzt(string? passwort)
    {
        if (string.IsNullOrEmpty(passwort) || passwort.Length < _optionen.MinPasswortLaenge)
        {
            return $"Das Passwort muss mindestens {_optionen.MinPasswortLaenge} Zeichen lang sein.";
        }

        if (!passwort.Any(char.IsUpper) || !passwort.Any(char.IsLower) || !passwort.Any(char.IsDigit))
        {
            return "Das Passwort muss Groß- und Kleinbuchstaben sowie mindestens eine Ziffer enthalten.";
        }

        return null;
    }
}

public sealed class AnmeldeService
{
    private readonly ZeiterfassungDbContext _db;
    private readonly PasswortService _passwoerter;
    private readonly IAppUhr _uhr;

    public AnmeldeService(ZeiterfassungDbContext db, PasswortService passwoerter, IAppUhr uhr)
    {
        _db = db;
        _passwoerter = passwoerter;
        _uhr = uhr;
    }

    public async Task<Mitarbeiter?> PruefenAsync(string benutzername, string passwort, CancellationToken ct = default)
    {
        // Anmeldung wahlweise mit Benutzername oder Personalnummer (Benutzername hat Vorrang)
        var eingabe = benutzername.Trim();
        var name = eingabe.ToLowerInvariant();
        var treffer = await _db.Mitarbeiter
            .Where(m => m.Aktiv && (m.Benutzername == name || m.Personalnummer == eingabe))
            .ToListAsync(ct);
        var mitarbeiter = treffer.FirstOrDefault(m => m.Benutzername == name) ?? treffer.FirstOrDefault();
        if (mitarbeiter is null || !_passwoerter.Pruefen(mitarbeiter, passwort))
        {
            return null;
        }

        mitarbeiter.LetzteAnmeldung = _uhr.Jetzt;
        await _db.SaveChangesAsync(ct);
        return mitarbeiter;
    }

    /// <summary>
    /// Erzeugt die Identität. Jede Rolle enthält auch alle niedrigeren Rollen als Claim,
    /// damit z. B. [Authorize(Roles = "Fuehrungskraft")] auch für die Personalabteilung greift.
    /// </summary>
    public static ClaimsPrincipal IdentitaetErzeugen(Mitarbeiter mitarbeiter)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, mitarbeiter.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, mitarbeiter.VollerName),
            new(Claimtypen.Hauptrolle, mitarbeiter.Rolle.ToString())
        };

        foreach (var rolle in Enum.GetValues<Rolle>().Where(r => r <= mitarbeiter.Rolle))
        {
            claims.Add(new Claim(ClaimTypes.Role, rolle.ToString()));
        }

        if (mitarbeiter.PasswortAendernErforderlich)
        {
            claims.Add(new Claim(Claimtypen.PasswortAendern, "1"));
        }

        var identitaet = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identitaet);
    }
}
