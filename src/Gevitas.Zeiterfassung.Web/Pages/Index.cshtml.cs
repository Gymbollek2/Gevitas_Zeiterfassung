using Gevitas.Zeiterfassung.Web.Infrastructure;
using Gevitas.Zeiterfassung.Web.Services;

namespace Gevitas.Zeiterfassung.Web.Pages;

public class IndexModel : AppSeite
{
    private readonly KontenService _konten;

    public IndexModel(KontenService konten)
    {
        _konten = konten;
    }

    public Kontostand Stand { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        Stand = await _konten.KontostandAsync(BenutzerId, HttpContext.RequestAborted);
    }
}
