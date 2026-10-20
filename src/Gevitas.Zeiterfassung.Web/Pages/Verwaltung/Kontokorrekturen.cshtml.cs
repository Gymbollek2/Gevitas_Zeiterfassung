using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Infrastructure;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gevitas.Zeiterfassung.Web.Pages.Verwaltung;

public class KontokorrekturenModel : AppSeite
{
    private readonly KontobuchungService _kontobuchungen;
    private readonly KontenService _konten;
    private readonly MitarbeiterService _mitarbeiter;
    private readonly IAppUhr _uhr;

    public KontokorrekturenModel(KontobuchungService kontobuchungen, KontenService konten, MitarbeiterService mitarbeiter, IAppUhr uhr)
    {
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

        Melden(await _kontobuchungen.KorrigierenAsync(Ma.Value, konto, stunden, datum, bemerkung, BenutzerId, HttpContext.RequestAborted));
        return RedirectToPage(new { ma = Ma });
    }

    public async Task<IActionResult> OnPostStornoAsync(Guid vorgang)
    {
        Melden(await _kontobuchungen.StornierenAsync(vorgang, HttpContext.RequestAborted));
        return RedirectToPage(new { ma = Ma });
    }
}
