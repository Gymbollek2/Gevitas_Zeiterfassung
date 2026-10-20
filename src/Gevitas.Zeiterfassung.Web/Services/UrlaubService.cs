using Gevitas.Zeiterfassung.Web.Data;
using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Gevitas.Zeiterfassung.Web.Services;

/// <summary>Lädt die Urlaubsdaten aus der Datenbank und rechnet mit dem <see cref="UrlaubsRechner"/>.</summary>
public sealed class UrlaubService
{
    private readonly ZeiterfassungDbContext _db;
    private readonly Arbeitstagkalender _kalender;
    private readonly IAppUhr _uhr;
    private readonly UrlaubOptions _optionen;

    public UrlaubService(ZeiterfassungDbContext db, Arbeitstagkalender kalender, IAppUhr uhr, IOptions<ZeiterfassungOptions> optionen)
    {
        _db = db;
        _kalender = kalender;
        _uhr = uhr;
        _optionen = optionen.Value.Urlaub;
    }

    public async Task<UrlaubsUebersicht> UebersichtAsync(int mitarbeiterId, int? jahr = null, CancellationToken ct = default)
    {
        var (mitarbeiter, tage, verlaengert) = await LadenAsync(mitarbeiterId, null, ct);
        return UrlaubsRechner.Berechnen(mitarbeiter.Eintrittsdatum, mitarbeiter.Jahresurlaub, tage, verlaengert, _optionen,
            jahr ?? _uhr.Heute.Year, _uhr.Heute);
    }

    /// <summary>Prüft, ob für den Zeitraum genügend Urlaub vorhanden ist. Liefert null, wenn ja, sonst die Fehlermeldung.</summary>
    public async Task<string?> PruefenAsync(int mitarbeiterId, DateOnly von, DateOnly bis, CancellationToken ct = default)
    {
        var (mitarbeiter, tage, verlaengert) = await LadenAsync(mitarbeiterId, null, ct);
        var neu = UrlaubstageImZeitraum(mitarbeiter, von, bis);
        var alle = tage.Concat(neu).ToList();

        foreach (var jahr in neu.Select(t => t.Year).Distinct())
        {
            var uebersicht = UrlaubsRechner.Berechnen(mitarbeiter.Eintrittsdatum, mitarbeiter.Jahresurlaub, alle, verlaengert, _optionen,
                jahr, new DateOnly(jahr, 1, 1).AddDays(-1));

            // Verfügbar sind der Jahresanspruch und – bis zum Verfall – der Übertrag aus dem Vorjahr
            var verfuegbar = uebersicht.Anspruch + uebersicht.Uebertrag;
            var benoetigt = alle.Count(t => t.Year == jahr);
            if (uebersicht.ResturlaubPlan < 0 || benoetigt > verfuegbar)
            {
                return $"Nicht genügend Urlaubstage für {jahr}: benötigt {benoetigt}, verfügbar {verfuegbar:0.#} " +
                       $"(Resturlaub aus dem Vorjahr verfällt nach dem {Zeitformat.Datum(uebersicht.Verfall)}).";
            }
        }

        return null;
    }

    public List<DateOnly> UrlaubstageImZeitraum(Mitarbeiter mitarbeiter, DateOnly von, DateOnly bis)
    {
        var tage = new List<DateOnly>();
        for (var d = von; d <= bis; d = d.AddDays(1))
        {
            if (_kalender.SollMinuten(mitarbeiter, d) > 0)
            {
                tage.Add(d);
            }
        }

        return tage;
    }

    private async Task<(Mitarbeiter Mitarbeiter, List<DateOnly> Tage, HashSet<int> Verlaengert)> LadenAsync(int mitarbeiterId, int? ohneAbwesenheitId, CancellationToken ct)
    {
        var mitarbeiter = await _db.Mitarbeiter.AsNoTracking().FirstAsync(m => m.Id == mitarbeiterId, ct);
        var abwesenheiten = await _db.Abwesenheiten.AsNoTracking()
            .Where(a => a.MitarbeiterId == mitarbeiterId
                        && a.Art!.ZaehltAlsUrlaub
                        && (a.Status == AbwesenheitsStatus.Beantragt || a.Status == AbwesenheitsStatus.Genehmigt)
                        && a.Id != ohneAbwesenheitId)
            .ToListAsync(ct);
        var tage = abwesenheiten.SelectMany(a => UrlaubstageImZeitraum(mitarbeiter, a.Von, a.Bis)).ToList();

        var verlaengert = (await _db.Antraege.AsNoTracking()
                .Where(a => a.AntragstellerId == mitarbeiterId && a.Art == AntragsArt.UrlaubsUebertrag && a.Status == AntragsStatus.Genehmigt)
                .Select(a => a.Von)
                .ToListAsync(ct))
            .Select(d => d.Year)
            .ToHashSet();

        return (mitarbeiter, tage, verlaengert);
    }
}
