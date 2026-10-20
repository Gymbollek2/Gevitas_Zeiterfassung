using Gevitas.Zeiterfassung.Web.Options;

namespace Gevitas.Zeiterfassung.Web.Services;

public sealed record UrlaubsUebersicht(
    int Jahr,
    decimal Anspruch,
    decimal Uebertrag,
    DateOnly Verfall,
    bool UebertragVerlaengert,
    decimal UebertragVerbraucht,
    decimal UebertragVerfallen,
    decimal GenommenBisStichtag,
    decimal GeplantNachStichtag,
    decimal Urlaubssaldo,
    decimal ResturlaubPlan);

/// <summary>
/// Reine Urlaubsberechnung:
/// <list type="bullet">
/// <item>Resturlaub des Vorjahres wird zuerst verbraucht und verfällt nach dem 31.03. (bzw. 31.05. bei genehmigter Übertragung).</item>
/// <item>Urlaubssaldo = tagesaktuell verfügbarer Urlaub (bereits genommener Urlaub ist abgezogen).</item>
/// <item>Resturlaub (Plan zum 31.12.) = Anspruch des laufenden Jahres abzüglich aller beantragten/genehmigten Tage daraus.</item>
/// </list>
/// </summary>
public static class UrlaubsRechner
{
    public static UrlaubsUebersicht Berechnen(
        DateOnly eintritt,
        int jahresurlaub,
        IEnumerable<DateOnly> urlaubstage,
        ISet<int> verlaengerteJahre,
        UrlaubOptions optionen,
        int jahr,
        DateOnly stichtag)
    {
        var tage = urlaubstage.Distinct().OrderBy(t => t).ToList();
        decimal uebertrag = 0;
        var startJahr = Math.Min(eintritt.Year, jahr);

        for (var j = startJahr; j < jahr; j++)
        {
            var (_, ausAnspruch, _, _) = Verteilen(j, Anspruch(eintritt, jahresurlaub, j), uebertrag, tage, verlaengerteJahre, optionen, DateOnly.MaxValue);
            uebertrag = Math.Max(0, Anspruch(eintritt, jahresurlaub, j) - ausAnspruch);
        }

        var anspruch = Anspruch(eintritt, jahresurlaub, jahr);
        var (verfall, ausAnspruchGesamt, uebertragGesamt, _) = Verteilen(jahr, anspruch, uebertrag, tage, verlaengerteJahre, optionen, DateOnly.MaxValue);
        var (_, ausAnspruchBis, uebertragBis, genommen) = Verteilen(jahr, anspruch, uebertrag, tage, verlaengerteJahre, optionen, stichtag);

        var restUebertrag = stichtag <= verfall ? uebertrag - uebertragBis : 0;
        var verfallen = stichtag > verfall ? uebertrag - uebertragGesamt : 0;
        var geplant = tage.Count(t => t.Year == jahr && t > stichtag);

        return new UrlaubsUebersicht(
            jahr,
            anspruch,
            uebertrag,
            verfall,
            verlaengerteJahre.Contains(jahr),
            uebertragGesamt,
            verfallen,
            genommen,
            geplant,
            anspruch - ausAnspruchBis + restUebertrag,
            anspruch - ausAnspruchGesamt);
    }

    /// <summary>Jahresanspruch; im Eintrittsjahr anteilig nach vollen Monaten (aufgerundet).</summary>
    public static decimal Anspruch(DateOnly eintritt, int jahresurlaub, int jahr)
    {
        if (jahr < eintritt.Year)
        {
            return 0;
        }

        if (jahr > eintritt.Year)
        {
            return jahresurlaub;
        }

        var monate = 13 - eintritt.Month;
        return Math.Ceiling(jahresurlaub * monate / 12m);
    }

    public static DateOnly Verfallsdatum(int jahr, ISet<int> verlaengerteJahre, UrlaubOptions optionen) =>
        UrlaubOptions.Datum(jahr, verlaengerteJahre.Contains(jahr) ? optionen.VerlaengerterVerfall : optionen.Verfall);

    /// <summary>Verteilt die Urlaubstage eines Jahres bis zum Stichtag auf Übertrag und Anspruch.</summary>
    private static (DateOnly Verfall, decimal AusAnspruch, decimal AusUebertrag, decimal Genommen) Verteilen(
        int jahr, decimal anspruch, decimal uebertrag, List<DateOnly> tage, ISet<int> verlaengerteJahre, UrlaubOptions optionen, DateOnly bis)
    {
        var verfall = Verfallsdatum(jahr, verlaengerteJahre, optionen);
        decimal ausUebertrag = 0;
        decimal ausAnspruch = 0;
        decimal genommen = 0;

        foreach (var tag in tage.Where(t => t.Year == jahr && t <= bis))
        {
            genommen++;
            if (tag <= verfall && ausUebertrag < uebertrag)
            {
                ausUebertrag++;
            }
            else
            {
                ausAnspruch++;
            }
        }

        return (verfall, ausAnspruch, ausUebertrag, genommen);
    }
}
