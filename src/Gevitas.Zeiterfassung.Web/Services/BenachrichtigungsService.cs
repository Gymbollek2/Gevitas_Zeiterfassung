using System.Net;
using System.Net.Mail;
using Gevitas.Zeiterfassung.Web.Data;
using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Infrastructure;
using Gevitas.Zeiterfassung.Web.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Gevitas.Zeiterfassung.Web.Services;

public interface IEmailVersand
{
    Task SendenAsync(string empfaenger, string betreff, string text, CancellationToken ct = default);
}

/// <summary>Versendet E-Mails per SMTP, sofern in der appsettings.json (Abschnitt "Smtp") aktiviert.</summary>
public sealed class SmtpEmailVersand : IEmailVersand
{
    private readonly SmtpOptions _optionen;
    private readonly ILogger<SmtpEmailVersand> _log;

    public SmtpEmailVersand(IOptions<SmtpOptions> optionen, ILogger<SmtpEmailVersand> log)
    {
        _optionen = optionen.Value;
        _log = log;
    }

    public async Task SendenAsync(string empfaenger, string betreff, string text, CancellationToken ct = default)
    {
        if (!_optionen.Aktiv || string.IsNullOrWhiteSpace(_optionen.Host) || string.IsNullOrWhiteSpace(empfaenger))
        {
            _log.LogDebug("E-Mail-Versand deaktiviert – Nachricht an {Empfaenger} nicht versendet: {Betreff}", empfaenger, betreff);
            return;
        }

        try
        {
            using var client = new SmtpClient(_optionen.Host, _optionen.Port) { EnableSsl = _optionen.SslAktiv };
            if (!string.IsNullOrEmpty(_optionen.Benutzer))
            {
                client.Credentials = new NetworkCredential(_optionen.Benutzer, _optionen.Passwort);
            }

            using var nachricht = new MailMessage(_optionen.Absender, empfaenger, betreff, text);
            await client.SendMailAsync(nachricht, ct);
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException)
        {
            // Ein Mailfehler darf die fachliche Aktion (z. B. einen Antrag) nicht verhindern
            _log.LogError(ex, "E-Mail an {Empfaenger} konnte nicht versendet werden.", empfaenger);
        }
    }
}

/// <summary>
/// Benachrichtigungen in der Anwendung (Glocke oben rechts) und – falls konfiguriert – per E-Mail.
/// Der Titel wird als deutscher Schlüssel gespeichert und bei der Anzeige übersetzt; der Text wird in der
/// Sprache der Empfängerin bzw. des Empfängers erzeugt.
/// </summary>
public sealed class BenachrichtigungsService
{
    public const int AnzahlInDerGlocke = 8;

    private readonly ZeiterfassungDbContext _db;
    private readonly IEmailVersand _email;
    private readonly IAppUhr _uhr;
    private readonly ZeiterfassungOptions _optionen;

    public BenachrichtigungsService(ZeiterfassungDbContext db, IEmailVersand email, IAppUhr uhr, IOptions<ZeiterfassungOptions> optionen)
    {
        _db = db;
        _email = email;
        _uhr = uhr;
        _optionen = optionen.Value;
    }

    /// <summary>Legt eine Benachrichtigung an. Wird mit dem nächsten SaveChanges gespeichert; die E-Mail geht sofort raus.</summary>
    public Task BenachrichtigenAsync(int mitarbeiterId, string titel, string text, string? link, CancellationToken ct = default) =>
        BenachrichtigenAsync(mitarbeiterId, titel, () => text, link, ct);

    /// <summary>
    /// Legt eine Benachrichtigung an. <paramref name="text"/> wird in der Sprache der Empfängerin bzw. des Empfängers
    /// ausgewertet (T/F innerhalb der Funktion verwenden). Inaktive Personen werden nicht benachrichtigt.
    /// </summary>
    public async Task BenachrichtigenAsync(int mitarbeiterId, string titel, Func<string> text, string? link, CancellationToken ct = default)
    {
        var empfaenger = await _db.Mitarbeiter.AsNoTracking()
            .Where(m => m.Id == mitarbeiterId)
            .Select(m => new { m.Email, m.Sprache, m.Aktiv })
            .FirstOrDefaultAsync(ct);
        if (empfaenger is null || !empfaenger.Aktiv)
        {
            return;
        }

        string inhalt;
        string betreff;
        var bisher = Texte.Sprache;
        try
        {
            Texte.Sprache = Benutzereinstellungen.GueltigeSprache(empfaenger.Sprache) ?? Benutzereinstellungen.Deutsch;
            inhalt = text();
            betreff = Texte.T(titel);
        }
        finally
        {
            Texte.Sprache = bisher;
        }

        _db.Benachrichtigungen.Add(new Benachrichtigung
        {
            MitarbeiterId = mitarbeiterId,
            Titel = titel,
            Text = inhalt.Length > 2000 ? inhalt[..2000] : inhalt,
            Link = link,
            ErstelltAm = _uhr.Jetzt
        });

        if (!string.IsNullOrWhiteSpace(empfaenger.Email))
        {
            await _email.SendenAsync(empfaenger.Email, $"{_optionen.Anwendungsname}: {betreff}", inhalt, ct);
        }
    }

    /// <summary>Speichert angelegte Benachrichtigungen, wenn keine weitere fachliche Änderung gespeichert wird.</summary>
    public Task SpeichernAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public Task<int> AnzahlUngelesenAsync(int mitarbeiterId, CancellationToken ct = default) =>
        _db.Benachrichtigungen.CountAsync(b => b.MitarbeiterId == mitarbeiterId && !b.Gelesen, ct);

    public Task<List<Benachrichtigung>> UngeleseneAsync(int mitarbeiterId, CancellationToken ct = default) =>
        _db.Benachrichtigungen.AsNoTracking()
            .Where(b => b.MitarbeiterId == mitarbeiterId && !b.Gelesen)
            .OrderByDescending(b => b.ErstelltAm).ThenByDescending(b => b.Id)
            .Take(20)
            .ToListAsync(ct);

    /// <summary>Die neuesten Benachrichtigungen (gelesen und ungelesen), z. B. für die Glocke.</summary>
    public Task<List<Benachrichtigung>> NeuesteAsync(int mitarbeiterId, int anzahl, CancellationToken ct = default) =>
        _db.Benachrichtigungen.AsNoTracking()
            .Where(b => b.MitarbeiterId == mitarbeiterId)
            .OrderByDescending(b => b.ErstelltAm).ThenByDescending(b => b.Id)
            .Take(anzahl)
            .ToListAsync(ct);

    /// <summary>
    /// Markiert eine eigene Benachrichtigung als gelesen und liefert sie zurück (null, wenn sie nicht existiert
    /// oder einer anderen Person gehört).
    /// </summary>
    public async Task<Benachrichtigung?> GelesenAsync(int mitarbeiterId, int benachrichtigungId, CancellationToken ct = default)
    {
        var b = await _db.Benachrichtigungen.FirstOrDefaultAsync(x => x.Id == benachrichtigungId && x.MitarbeiterId == mitarbeiterId, ct);
        if (b is { Gelesen: false })
        {
            b.Gelesen = true;
            await _db.SaveChangesAsync(ct);
        }

        return b;
    }

    public async Task AlleGelesenAsync(int mitarbeiterId, CancellationToken ct = default)
    {
        await _db.Benachrichtigungen
            .Where(b => b.MitarbeiterId == mitarbeiterId && !b.Gelesen)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Gelesen, true), ct);
    }

    /// <summary>Löscht gelesene Benachrichtigungen, die älter als die konfigurierte Aufbewahrungsdauer sind.</summary>
    public async Task<int> AufraeumenAsync(CancellationToken ct = default)
    {
        var grenze = _uhr.Jetzt.AddDays(-Math.Max(1, _optionen.BenachrichtigungenAufbewahrenTage));
        return await _db.Benachrichtigungen
            .Where(b => b.Gelesen && b.ErstelltAm < grenze)
            .ExecuteDeleteAsync(ct);
    }
}
