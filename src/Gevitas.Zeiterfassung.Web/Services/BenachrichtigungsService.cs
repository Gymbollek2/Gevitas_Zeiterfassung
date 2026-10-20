using System.Net;
using System.Net.Mail;
using Gevitas.Zeiterfassung.Web.Data;
using Gevitas.Zeiterfassung.Web.Domain;
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

/// <summary>Benachrichtigungen in der Anwendung und – falls konfiguriert – per E-Mail.</summary>
public sealed class BenachrichtigungsService
{
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
    public async Task BenachrichtigenAsync(int mitarbeiterId, string titel, string text, string? link, CancellationToken ct = default)
    {
        _db.Benachrichtigungen.Add(new Benachrichtigung
        {
            MitarbeiterId = mitarbeiterId,
            Titel = titel,
            Text = text,
            Link = link,
            ErstelltAm = _uhr.Jetzt
        });

        var empfaenger = await _db.Mitarbeiter.AsNoTracking().Where(m => m.Id == mitarbeiterId).Select(m => m.Email).FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(empfaenger))
        {
            await _email.SendenAsync(empfaenger, $"{_optionen.Anwendungsname}: {titel}", text, ct);
        }
    }

    public Task<List<Benachrichtigung>> UngeleseneAsync(int mitarbeiterId, CancellationToken ct = default) =>
        _db.Benachrichtigungen.AsNoTracking()
            .Where(b => b.MitarbeiterId == mitarbeiterId && !b.Gelesen)
            .OrderByDescending(b => b.ErstelltAm)
            .Take(20)
            .ToListAsync(ct);

    public async Task AlleGelesenAsync(int mitarbeiterId, CancellationToken ct = default)
    {
        await _db.Benachrichtigungen
            .Where(b => b.MitarbeiterId == mitarbeiterId && !b.Gelesen)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Gelesen, true), ct);
    }
}
