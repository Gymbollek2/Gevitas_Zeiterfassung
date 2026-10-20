using Gevitas.Zeiterfassung.Web.Options;
using Gevitas.Zeiterfassung.Web.Services;

namespace Gevitas.Zeiterfassung.Tests;

public class UrlaubsRechnerTests
{
    private static readonly UrlaubOptions Optionen = new();
    private static readonly DateOnly Eintritt = new(2025, 1, 1);

    private static IEnumerable<DateOnly> Werktage(DateOnly von, DateOnly bis)
    {
        for (var d = von; d <= bis; d = d.AddDays(1))
        {
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                yield return d;
            }
        }
    }

    [Fact]
    public void Nicht_genommener_Urlaub_wird_ins_Folgejahr_uebertragen()
    {
        var u = UrlaubsRechner.Berechnen(Eintritt, 30, Array.Empty<DateOnly>(), new HashSet<int>(), Optionen, 2026, new DateOnly(2026, 2, 1));

        Assert.Equal(30, u.Anspruch);
        Assert.Equal(30, u.Uebertrag);
        Assert.Equal(new DateOnly(2026, 3, 31), u.Verfall);
        Assert.Equal(60, u.Urlaubssaldo);
        Assert.Equal(30, u.ResturlaubPlan);
    }

    [Fact]
    public void Uebertrag_verfaellt_nach_dem_31_Maerz()
    {
        var u = UrlaubsRechner.Berechnen(Eintritt, 30, Array.Empty<DateOnly>(), new HashSet<int>(), Optionen, 2026, new DateOnly(2026, 4, 15));

        Assert.Equal(30, u.Urlaubssaldo);
        Assert.Equal(30, u.UebertragVerfallen);
    }

    [Fact]
    public void Urlaub_im_Januar_verbraucht_zuerst_den_Uebertrag()
    {
        var tage = Werktage(new DateOnly(2026, 1, 12), new DateOnly(2026, 1, 16)).ToList();
        var u = UrlaubsRechner.Berechnen(Eintritt, 30, tage, new HashSet<int>(), Optionen, 2026, new DateOnly(2026, 2, 1));

        Assert.Equal(5, u.UebertragVerbraucht);
        Assert.Equal(55, u.Urlaubssaldo);
        Assert.Equal(30, u.ResturlaubPlan);
    }

    [Fact]
    public void Genehmigte_Uebertragung_verlaengert_bis_31_Mai()
    {
        var tage = Werktage(new DateOnly(2026, 5, 4), new DateOnly(2026, 5, 8)).ToList();

        var ohne = UrlaubsRechner.Berechnen(Eintritt, 30, tage, new HashSet<int>(), Optionen, 2026, new DateOnly(2026, 5, 20));
        var mit = UrlaubsRechner.Berechnen(Eintritt, 30, tage, new HashSet<int> { 2026 }, Optionen, 2026, new DateOnly(2026, 5, 20));

        Assert.Equal(25, ohne.ResturlaubPlan);
        Assert.Equal(30, mit.ResturlaubPlan);
        Assert.Equal(new DateOnly(2026, 5, 31), mit.Verfall);
        Assert.True(mit.UebertragVerlaengert);
    }

    [Fact]
    public void Geplanter_Urlaub_reduziert_den_Plan_aber_nicht_das_Tagessaldo()
    {
        var tage = Werktage(new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 14)).ToList();
        var u = UrlaubsRechner.Berechnen(Eintritt, 30, tage, new HashSet<int>(), Optionen, 2026, new DateOnly(2026, 7, 1));

        Assert.Equal(10, u.GeplantNachStichtag);
        Assert.Equal(20, u.ResturlaubPlan);
        Assert.Equal(30, u.Urlaubssaldo);
    }

    [Theory]
    [InlineData(1, 30)]
    [InlineData(7, 15)]
    [InlineData(10, 8)]
    [InlineData(12, 3)]
    public void Anspruch_im_Eintrittsjahr_ist_anteilig(int monat, int erwartet)
    {
        Assert.Equal(erwartet, UrlaubsRechner.Anspruch(new DateOnly(2026, monat, 1), 30, 2026));
    }
}
