using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Infrastructure;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.AspNetCore.Mvc;
using static Gevitas.Zeiterfassung.Web.Services.Texte;

namespace Gevitas.Zeiterfassung.Web.Pages.Verwaltung;

public class KontokorrekturenModel : AppSeite
{
    private readonly KontobuchungService _kontobuchungen;
    private readonly KontenService _konten;
    private readonly MitarbeiterService _mitarbeiter;
    private readonly BenachrichtigungsService _benachrichtigungen;
    private readonly IAppUhr _uhr;

    public KontokorrekturenModel(KontobuchungService kontobuchungen, KontenService konten, MitarbeiterService mitarbeiter, BenachrichtigungsService benachrichtigungen, IAppUhr uhr)
    {
        _benachrichtigungen = benachrichtigungen;
        _kontobuchungen = kontobuchungen;
        _konten = konten;
        _mitarbeiter = mitarbeiter;
        _uhr = uhr;
    }

    [BindProperty(SupportsGet = true)]
    public int? Ma { get; set; }

    public List<Mitarbeiter> Personen { get; private set; } = new();
    public Mitarbeiter? Person { get; private set; }
    public Kontostand? Stand { get; private set; }
    public List<Kontobuchung> Buchungen { get; private set; } = new();
    public DateOnly Heute => _uhr.Heute;

    public async Task OnGetAsync()
    {
        var ct = HttpContext.RequestAborted;
        Personen = await _mitarbeiter.AlleAsync(nurAktive: true, ct);
        Ma ??= Personen.FirstOrDefault()?.Id;
        if (Ma is int id)
        {
            Person = Personen.FirstOrDefault(p => p.Id == id);
            if (Person is not null)
            {
                Stand = await _konten.KontostandAsync(id, ct);
                Buchungen = await _kontobuchungen.ListeAsync(id, ct);
            }
        }
    }

    public async Task<IActionResult> OnPostKorrekturAsync(Konto konto, string? stunden, DateOnly datum, string? bemerkung)
    {
        if (Ma is null)
        {
            return BadRequest();
        }

        var ct = HttpContext.RequestAborted;
        var ergebnis = await _kontobuchungen.KorrigierenAsync(Ma.Value, konto, stunden, datum, bemerkung, BenutzerId, ct);
        if (ergebnis.Erfolg && Ma != BenutzerId)
        {
            var wert = stunden?.Trim() ?? string.Empty;
            var grund = bemerkung?.Trim() ?? string.Empty;
            await _benachrichtigungen.BenachrichtigenAsync(Ma.Value, "Kontokorrektur",
                () => F("Die Personalabteilung hat auf Ihrem Konto „{0}“ eine Korrektur von {1} Std. zum {2} gebucht. Begründung: {3}", konto.Text(), wert, Zeitformat.Datum(datum), grund),
                "/Zeitwirtschaft/Zeitkorrekturen", ct);
            await _benachrichtigungen.SpeichernAsync(ct);
        }

        Melden(ergebnis);
        return RedirectToPage(new { ma = Ma });
    }

    public async Task<IActionResult> OnPostStornoAsync(Guid vorgang)
    {
        var ct = HttpContext.RequestAborted;
        var ergebnis = await _kontobuchungen.StornierenAsync(vorgang, ct);
        if (ergebnis.Erfolg && Ma is int id && id != BenutzerId)
        {
            await _benachrichtigungen.BenachrichtigenAsync(id, "Kontobuchung storniert",
                () => T("Die Personalabteilung hat eine Buchung auf Ihren Zeitkonten storniert. Ihre Kontostände wurden neu berechnet."),
                "/Zeitwirtschaft/Zeitkorrekturen", ct);
            await _benachrichtigungen.SpeichernAsync(ct);
        }

        Melden(ergebnis);
        return RedirectToPage(new { ma = Ma });
    }
}
