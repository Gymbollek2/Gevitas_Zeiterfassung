using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Infrastructure;
using Gevitas.Zeiterfassung.Web.Services;

namespace Gevitas.Zeiterfassung.Web.Pages;

public class MeineDatenModel : AppSeite
{
    private readonly MitarbeiterService _mitarbeiter;

    public MeineDatenModel(MitarbeiterService mitarbeiter)
    {
        _mitarbeiter = mitarbeiter;
    }

    public Mitarbeiter Ich { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        Ich = (await _mitarbeiter.LadenAsync(BenutzerId, HttpContext.RequestAborted))!;
    }
}
