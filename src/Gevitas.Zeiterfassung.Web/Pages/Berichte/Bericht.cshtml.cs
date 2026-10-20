using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Infrastructure;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.AspNetCore.Mvc;
using static Gevitas.Zeiterfassung.Web.Services.Texte;

namespace Gevitas.Zeiterfassung.Web.Pages.Berichte;

public class BerichtModel : AppSeite
{
    private readonly BerichtService _berichte;
    private readonly MitarbeiterService _mitarbeiter;
    private readonly IAppUhr _uhr;

    public BerichtModel(BerichtService berichte, MitarbeiterService mitarbeiter, IAppUhr uhr)
    {
        _berichte = berichte;
        _mitarbeiter = mitarbeiter;
        _uhr = uhr;
    }

    [BindProperty(SupportsGet = true)]
    public int Nr { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Ma { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? Von { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? Bis { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Jahr { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Format { get; set; }

    public BerichtDefinition Definition { get; private set; } = null!;
    public List<Mitarbeiter> Auswahl { get; private set; } = new();
    public BerichtDaten? Daten { get; private set; }
    public bool PersonWaehlbar => Auswahl.Count > 1 && Definition.Nummer != 539;

    public async Task<IActionResult> OnGetAsync()
    {
        var ct = HttpContext.RequestAborted;
        var definition = BerichtService.Definition(Nr);
        if (definition is null)
        {
            return NotFound();
        }

        Definition = definition;
        var heute = _uhr.Heute;
        Von ??= new DateOnly(heute.Year, heute.Month, 1);
        Bis ??= Von.Value.AddMonths(1).AddDays(-1);
        Jahr ??= heute.Year;
        if (Bis < Von)
        {
            (Von, Bis) = (Bis, Von);
        }

        if (Bis.Value.DayNumber - Von.Value.DayNumber > 400)
        {
            Fehler(T("Der Zeitraum darf höchstens ein Jahr umfassen."));
            Bis = Von.Value.AddYears(1);
        }

        // Sichtbarkeit: Einzelbuchungen nur für die eigene Person (bzw. Personalabteilung), Salden auch für das Team
        Auswahl = definition.NurSalden || User.IstPersonal()
            ? await _mitarbeiter.SichtbareAsync(BenutzerId, BenutzerRolle, ct)
            : new List<Mitarbeiter> { (await _mitarbeiter.LadenAsync(BenutzerId, ct))! };

        List<Mitarbeiter> personen;
        if (definition.Nummer == 539)
        {
            personen = Auswahl;
        }
        else
        {
            var ziel = Auswahl.FirstOrDefault(p => p.Id == (Ma ?? BenutzerId)) ?? Auswahl.First(p => p.Id == BenutzerId || Auswahl.Count == 1);
            Ma = ziel.Id;
            personen = new List<Mitarbeiter> { ziel };
        }

        var von = definition.MitZeitraum ? Von.Value : new DateOnly(Jahr.Value, 1, 1);
        var bis = definition.MitZeitraum ? Bis.Value : new DateOnly(Jahr.Value, 12, 31);
        Daten = await _berichte.ErstellenAsync(definition, personen, von, bis, ct);

        if (string.Equals(Format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var datei = $"Bericht_{definition.Nummer}_{definition.Bezeichnung.Replace(' ', '_')}_{heute:yyyyMMdd}.csv";
            return File(BerichtService.AlsCsv(Daten), "text/csv; charset=utf-8", datei);
        }

        return Page();
    }
}
