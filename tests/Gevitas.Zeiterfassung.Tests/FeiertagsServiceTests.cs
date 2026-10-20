using Gevitas.Zeiterfassung.Web.Services;

namespace Gevitas.Zeiterfassung.Tests;

public class FeiertagsServiceTests
{
    private readonly FeiertagsService _feiertage = new(TestDaten.Als(TestDaten.Optionen()));

    [Theory]
    [InlineData(2024, 3, 31)]
    [InlineData(2025, 4, 20)]
    [InlineData(2026, 4, 5)]
    [InlineData(2027, 3, 28)]
    public void Ostersonntag_wird_korrekt_berechnet(int jahr, int monat, int tag)
    {
        Assert.Equal(new DateOnly(jahr, monat, tag), FeiertagsService.Ostersonntag(jahr));
    }

    [Theory]
    [InlineData("2026-01-06", "Heilige Drei Könige")]
    [InlineData("2026-04-03", "Karfreitag")]
    [InlineData("2026-05-14", "Christi Himmelfahrt")]
    [InlineData("2026-06-04", "Fronleichnam")]
    [InlineData("2026-10-03", "Tag der Deutschen Einheit")]
    [InlineData("2026-11-01", "Allerheiligen")]
    public void Baden_Wuerttemberg_Feiertage_werden_erkannt(string datum, string name)
    {
        Assert.True(_feiertage.IstFeiertag(DateOnly.Parse(datum), out var gefunden));
        Assert.Equal(name, gefunden);
    }

    [Fact]
    public void Nicht_konfigurierte_Feiertage_gelten_nicht()
    {
        // Reformationstag ist in BW kein Feiertag
        Assert.False(_feiertage.IstFeiertag(new DateOnly(2026, 10, 31), out _));
    }
}
