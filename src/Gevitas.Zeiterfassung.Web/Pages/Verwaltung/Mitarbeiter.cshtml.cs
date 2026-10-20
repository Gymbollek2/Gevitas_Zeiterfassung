using Gevitas.Zeiterfassung.Web.Infrastructure;
using Gevitas.Zeiterfassung.Web.Services;

namespace Gevitas.Zeiterfassung.Web.Pages.Verwaltung;

public class MitarbeiterModel : AppSeite
{
    private readonly MitarbeiterService _mitarbeiter;

    public MitarbeiterModel(MitarbeiterService mitarbeiter)
    {
        _mitarbeiter = mitarbeiter;
    }

    public List<Domain.Mitarbeiter> Liste { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Liste = await _mitarbeiter.AlleAsync(ct: HttpContext.RequestAborted);
    }
}
