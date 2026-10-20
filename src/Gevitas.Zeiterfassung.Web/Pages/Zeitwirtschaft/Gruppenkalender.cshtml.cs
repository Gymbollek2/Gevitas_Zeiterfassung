using System.Globalization;
using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Infrastructure;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gevitas.Zeiterfassung.Web.Pages.Zeitwirtschaft;

/// <summary>Übersicht der Abwesenheiten des Teams – ohne Abwesenheitsgründe.</summary>
[Authorize(Policy = Richtlinien.Fuehrungskraft)]
public class GruppenkalenderModel : AppSeite
{
    private readonly KalenderService _kalender;
    private readonly MitarbeiterService _mitarbeiter;
    private readonly Arbeitstagkalender _arbeitstage;
    private readonly IAppUhr _uhr;

    public GruppenkalenderModel(KalenderService kalender, MitarbeiterService mitarbeiter, Arbeitstagkalender arbeitstage, IAppUhr uhr)
    {
        _kalender = kalender;
        _mitarbeiter = mitarbeiter;
        _arbeitstage = arbeitstage;
        _uhr = uhr;
    }

    [BindProperty(SupportsGet = true)]
    public string? Monat { get; set; }

    public DateOnly MonatsBeginn { get; private set; }
    public DateOnly Heute => _uhr.Heute;
    public List<DateOnly> Tage { get; private set; } = new();
    public List<Mitarbeiter> Personen { get; private set; } = new();
    public List<Abwesenheit> Abwesenheiten { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var ct = HttpContext.RequestAborted;
        MonatsBeginn = !string.IsNullOrEmpty(Monat)
                       && DateOnly.TryParseExact(Monat + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var m)
            ? m
            : new DateOnly(Heute.Year, Heute.Month, 1);
        var ende = MonatsBeginn.AddMonths(1).AddDays(-1);
        for (var d = MonatsBeginn; d <= ende; d = d.AddDays(1))
        {
            Tage.Add(d);
        }

        Personen = await _mitarbeiter.SichtbareAsync(BenutzerId, BenutzerRolle, ct);
        Abwesenheiten = await _kalender.AbwesenheitenAsync(Personen.Select(p => p.Id).ToList(), MonatsBeginn, ende, ct);
    }

    public bool IstArbeitstag(DateOnly d) => _arbeitstage.IstArbeitstag(d);

    public Abwesenheit? AbwesenheitAm(int mitarbeiterId, DateOnly d) =>
        Abwesenheiten.FirstOrDefault(a => a.MitarbeiterId == mitarbeiterId && a.Umfasst(d));

    public static string MonatIso(DateOnly d) => d.ToString("yyyy-MM", CultureInfo.InvariantCulture);
}
