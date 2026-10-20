using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Options;
using Microsoft.Extensions.Options;

namespace Gevitas.Zeiterfassung.Web.Services;

/// <summary>Bestimmt Arbeitstage und Sollarbeitszeit unter Berücksichtigung von Wochenenden und Feiertagen.</summary>
public sealed class Arbeitstagkalender
{
    private readonly IFeiertagsService _feiertage;
    private readonly HashSet<DayOfWeek> _arbeitstage;

    public Arbeitstagkalender(IFeiertagsService feiertage, IOptions<ZeiterfassungOptions> optionen)
    {
        _feiertage = feiertage;
        _arbeitstage = new HashSet<DayOfWeek>(optionen.Value.Sollarbeitszeit.ArbeitstageOderStandard);
    }

    public bool IstArbeitstag(DateOnly datum) => _arbeitstage.Contains(datum.DayOfWeek) && !_feiertage.IstFeiertag(datum, out _);

    public bool IstFeiertag(DateOnly datum, out string name) => _feiertage.IstFeiertag(datum, out name);

    public int SollMinuten(Mitarbeiter mitarbeiter, DateOnly datum) =>
        datum >= mitarbeiter.Erfassungsbeginn && IstArbeitstag(datum) ? mitarbeiter.SollMinutenProTag : 0;

    public int ArbeitstageZwischen(DateOnly von, DateOnly bis)
    {
        var anzahl = 0;
        for (var d = von; d <= bis; d = d.AddDays(1))
        {
            if (IstArbeitstag(d))
            {
                anzahl++;
            }
        }

        return anzahl;
    }
}
