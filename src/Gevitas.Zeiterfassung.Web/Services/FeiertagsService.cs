using System.Collections.Concurrent;
using Gevitas.Zeiterfassung.Web.Options;
using Microsoft.Extensions.Options;

namespace Gevitas.Zeiterfassung.Web.Services;

public interface IFeiertagsService
{
    IReadOnlyDictionary<DateOnly, string> FuerJahr(int jahr);
    bool IstFeiertag(DateOnly datum, out string name);
}

/// <summary>
/// Berechnet gesetzliche Feiertage. Welche Feiertage gelten, wird in der appsettings.json
/// (Zeiterfassung:Feiertage) festgelegt – Standard ist Baden-Württemberg.
/// </summary>
public sealed class FeiertagsService : IFeiertagsService
{
    private static readonly Dictionary<string, (string Name, Func<int, DateOnly, DateOnly> Datum)> Definitionen =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Neujahr"] = ("Neujahr", (j, _) => new DateOnly(j, 1, 1)),
            ["HeiligeDreiKoenige"] = ("Heilige Drei Könige", (j, _) => new DateOnly(j, 1, 6)),
            ["Frauentag"] = ("Internationaler Frauentag", (j, _) => new DateOnly(j, 3, 8)),
            ["Gruendonnerstag"] = ("Gründonnerstag", (_, o) => o.AddDays(-3)),
            ["Karfreitag"] = ("Karfreitag", (_, o) => o.AddDays(-2)),
            ["Ostersonntag"] = ("Ostersonntag", (_, o) => o),
            ["Ostermontag"] = ("Ostermontag", (_, o) => o.AddDays(1)),
            ["TagDerArbeit"] = ("Tag der Arbeit", (j, _) => new DateOnly(j, 5, 1)),
            ["ChristiHimmelfahrt"] = ("Christi Himmelfahrt", (_, o) => o.AddDays(39)),
            ["Pfingstsonntag"] = ("Pfingstsonntag", (_, o) => o.AddDays(49)),
            ["Pfingstmontag"] = ("Pfingstmontag", (_, o) => o.AddDays(50)),
            ["Fronleichnam"] = ("Fronleichnam", (_, o) => o.AddDays(60)),
            ["MariaeHimmelfahrt"] = ("Mariä Himmelfahrt", (j, _) => new DateOnly(j, 8, 15)),
            ["Weltkindertag"] = ("Weltkindertag", (j, _) => new DateOnly(j, 9, 20)),
            ["TagDerDeutschenEinheit"] = ("Tag der Deutschen Einheit", (j, _) => new DateOnly(j, 10, 3)),
            ["Reformationstag"] = ("Reformationstag", (j, _) => new DateOnly(j, 10, 31)),
            ["Allerheiligen"] = ("Allerheiligen", (j, _) => new DateOnly(j, 11, 1)),
            ["BussUndBettag"] = ("Buß- und Bettag", (j, _) => BussUndBettag(j)),
            ["Heiligabend"] = ("Heiligabend", (j, _) => new DateOnly(j, 12, 24)),
            ["ErsterWeihnachtstag"] = ("1. Weihnachtstag", (j, _) => new DateOnly(j, 12, 25)),
            ["ZweiterWeihnachtstag"] = ("2. Weihnachtstag", (j, _) => new DateOnly(j, 12, 26)),
            ["Silvester"] = ("Silvester", (j, _) => new DateOnly(j, 12, 31))
        };

    private readonly IReadOnlyList<string> _aktive;
    private readonly ConcurrentDictionary<int, IReadOnlyDictionary<DateOnly, string>> _cache = new();

    public FeiertagsService(IOptions<ZeiterfassungOptions> optionen)
    {
        _aktive = optionen.Value.Feiertage;
    }

    public static IReadOnlyCollection<string> BekannteSchluessel => Definitionen.Keys;

    public IReadOnlyDictionary<DateOnly, string> FuerJahr(int jahr) => _cache.GetOrAdd(jahr, Berechnen);

    public bool IstFeiertag(DateOnly datum, out string name)
    {
        if (FuerJahr(datum.Year).TryGetValue(datum, out var n))
        {
            name = n;
            return true;
        }

        name = string.Empty;
        return false;
    }

    private IReadOnlyDictionary<DateOnly, string> Berechnen(int jahr)
    {
        var ostern = Ostersonntag(jahr);
        var ergebnis = new Dictionary<DateOnly, string>();
        foreach (var schluessel in _aktive)
        {
            if (Definitionen.TryGetValue(schluessel, out var def))
            {
                ergebnis[def.Datum(jahr, ostern)] = def.Name;
            }
        }

        return ergebnis;
    }

    /// <summary>Ostersonntag nach der Gaußschen Osterformel (gregorianischer Kalender).</summary>
    public static DateOnly Ostersonntag(int jahr)
    {
        var a = jahr % 19;
        var b = jahr / 100;
        var c = jahr % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var monat = (h + l - 7 * m + 114) / 31;
        var tag = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateOnly(jahr, monat, tag);
    }

    /// <summary>Buß- und Bettag: Mittwoch vor dem 23. November.</summary>
    private static DateOnly BussUndBettag(int jahr)
    {
        var datum = new DateOnly(jahr, 11, 22);
        while (datum.DayOfWeek != DayOfWeek.Wednesday)
        {
            datum = datum.AddDays(-1);
        }

        return datum;
    }
}
