using Gevitas.Zeiterfassung.Web.Services;

namespace Gevitas.Zeiterfassung.Tests;

public class ZeitformatTests
{
    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(650, "10:50")]
    [InlineData(630, "10:30")]
    [InlineData(5, "0:05")]
    [InlineData(-18179, "-302:59")]
    public void Dauer_formatiert_Stunden_und_Minuten(int minuten, string erwartet)
    {
        Assert.Equal(erwartet, Zeitformat.Dauer(minuten));
    }

    [Theory]
    [InlineData("1:30", 90)]
    [InlineData("3", 180)]
    [InlineData("-0:45", -45)]
    [InlineData(" 12:05 ", 725)]
    public void TryParseDauer_liest_gueltige_Werte(string text, int erwartet)
    {
        Assert.True(Zeitformat.TryParseDauer(text, out var minuten));
        Assert.Equal(erwartet, minuten);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1:5")]
    [InlineData("1:75")]
    [InlineData("1:2:3")]
    public void TryParseDauer_lehnt_ungueltige_Werte_ab(string text)
    {
        Assert.False(Zeitformat.TryParseDauer(text, out _));
    }

    [Fact]
    public void Wochentag_ist_deutsch_abgekuerzt()
    {
        Assert.Equal("Di", Zeitformat.Wochentag(new DateOnly(2026, 10, 20)));
        Assert.Equal("Di. 20.10.2026", Zeitformat.DatumMitWochentag(new DateOnly(2026, 10, 20)));
    }
}
