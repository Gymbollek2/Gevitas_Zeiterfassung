using Gevitas.Zeiterfassung.Web.Data;
using Gevitas.Zeiterfassung.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gevitas.Zeiterfassung.Web.Services;

public sealed record KalenderTag(DateOnly Datum, bool IstArbeitstag, string? Feiertag, List<Abwesenheit> Abwesenheiten);

/// <summary>Liefert Abwesenheiten und Feiertage für Monats-, Jahres- und Gruppenkalender.</summary>
public sealed class KalenderService
{
    private readonly ZeiterfassungDbContext _db;
    private readonly Arbeitstagkalender _kalender;

    public KalenderService(ZeiterfassungDbContext db, Arbeitstagkalender kalender)
    {
        _db = db;
        _kalender = kalender;
    }

    public async Task<List<Abwesenheit>> AbwesenheitenAsync(IReadOnlyCollection<int> mitarbeiterIds, DateOnly von, DateOnly bis, CancellationToken ct = default) =>
        await _db.Abwesenheiten.AsNoTracking()
            .Include(a => a.Art)
            .Where(a => mitarbeiterIds.Contains(a.MitarbeiterId)
                        && a.Bis >= von && a.Von <= bis
                        && (a.Status == AbwesenheitsStatus.Beantragt || a.Status == AbwesenheitsStatus.Genehmigt))
            .OrderBy(a => a.Von)
            .ToListAsync(ct);

    public async Task<List<KalenderTag>> TageAsync(int mitarbeiterId, DateOnly von, DateOnly bis, CancellationToken ct = default)
    {
        var abwesenheiten = await AbwesenheitenAsync(new[] { mitarbeiterId }, von, bis, ct);
        var tage = new List<KalenderTag>();
        for (var d = von; d <= bis; d = d.AddDays(1))
        {
            _kalender.IstFeiertag(d, out var feiertag);
            tage.Add(new KalenderTag(d, _kalender.IstArbeitstag(d), string.IsNullOrEmpty(feiertag) ? null : feiertag,
                abwesenheiten.Where(a => a.Umfasst(d)).ToList()));
        }

        return tage;
    }

    public Task<List<Abwesenheitsart>> ArtenAsync(CancellationToken ct = default) =>
        _db.Abwesenheitsarten.AsNoTracking().Where(a => a.Aktiv).OrderBy(a => a.Sortierung).ToListAsync(ct);
}
