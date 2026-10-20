using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Infrastructure;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gevitas.Zeiterfassung.Web.Pages;

public class IndexModel : AppSeite
{
    private readonly KontenService _konten;
    private readonly UrlaubService _urlaub;
    private readonly AntragsService _antraege;
    private readonly BenachrichtigungsService _benachrichtigungen;

    public IndexModel(KontenService konten, UrlaubService urlaub, AntragsService antraege, BenachrichtigungsService benachrichtigungen)
    {
        _konten = konten;
        _urlaub = urlaub;
        _antraege = antraege;
        _benachrichtigungen = benachrichtigungen;
    }

    public Kontostand Stand { get; private set; } = null!;
    public UrlaubsUebersicht Urlaub { get; private set; } = null!;
    public int OffeneGenehmigungen { get; private set; }
    public List<Benachrichtigung> Benachrichtigungen { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var ct = HttpContext.RequestAborted;
        Stand = await _konten.KontostandAsync(BenutzerId, ct);
        Urlaub = await _urlaub.UebersichtAsync(BenutzerId, ct: ct);
        OffeneGenehmigungen = User.IstFuehrungskraft() ? await _antraege.OffeneZurGenehmigungAsync(BenutzerId, BenutzerRolle, ct) : 0;
        Benachrichtigungen = await _benachrichtigungen.UngeleseneAsync(BenutzerId, ct);
    }

    public async Task<IActionResult> OnPostGelesenAsync()
    {
        await _benachrichtigungen.AlleGelesenAsync(BenutzerId, HttpContext.RequestAborted);
        return RedirectToPage();
    }
}
