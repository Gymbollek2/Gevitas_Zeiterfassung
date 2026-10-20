using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Services;

namespace Gevitas.Zeiterfassung.Tests;

public class TagesberechnungTests
{
    private static readonly DateOnly Tag = new(2026, 10, 14);
    private readonly Tagesberechnung _berechnung = new(ArbeitszeitRegeln.Standard);

    private static BuchungsIntervall A(string beginn, string? ende, BuchungsArt art = BuchungsArt.Arbeit) =>
        new(TimeOnly.Parse(beginn), ende is null ? null : TimeOnly.Parse(ende), art);

    private TagesErgebnis Rechne(int soll = 480, params BuchungsIntervall[] buchungen) =>
        _berechnung.Berechnen(new TagesEingabe { Datum = Tag, SollMinuten = soll, Buchungen = buchungen, IstAbgeschlossen = true });

    [Fact]
    public void Normaler_Tag_mit_ausreichender_Pause_ohne_Abzug()
    {
        var e = Rechne(480, A("08:00", "12:00"), A("12:30", "16:30"));

        Assert.Equal(480, e.ArbeitszeitMinuten);
        Assert.Equal(0, e.PausenabzugMinuten);
        Assert.Equal(30, e.PausenMinuten);
        Assert.Equal(0, e.SaldoDelta);
        Assert.Empty(e.Hinweise);
    }

    [Fact]
    public void Ueber_sechs_Stunden_ohne_Pause_werden_30_Minuten_ergaenzt()
    {
        var e = Rechne(480, A("08:00", "15:00"));

        Assert.Equal(420, e.AnwesenheitMinuten);
        Assert.Equal(30, e.PausenabzugMinuten);
        Assert.Equal(390, e.ArbeitszeitMinuten);
        Assert.Contains(e.Hinweise, h => h.Code == Regelcodes.PauseErgaenzt);
    }

    [Fact]
    public void Abzug_unterschreitet_nie_die_Sechs_Stunden_Schwelle()
    {
        // 6:10 ohne Pause: es werden nur 10 Minuten abgezogen
        var e = Rechne(480, A("08:00", "14:10"));

        Assert.Equal(360, e.ArbeitszeitMinuten);
        Assert.Equal(10, e.PausenabzugMinuten);
    }

    [Fact]
    public void Erste_Pause_16_Minuten_wird_bis_neun_Stunden_um_14_Minuten_ergaenzt()
    {
        // Beispiel aus dem Handbuch: erste Pause 16 Minuten → bis 9 Stunden wird um 14 Minuten ergänzt
        var e = Rechne(480, A("07:00", "11:00"), A("11:16", "16:30"));

        Assert.Equal(16, e.PausenMinuten);
        Assert.Equal(14, e.PausenabzugMinuten);
        Assert.Equal(540, e.ArbeitszeitMinuten);
    }

    [Fact]
    public void Pause_unter_15_Minuten_zaehlt_nicht()
    {
        var e = Rechne(480, A("08:00", "11:00"), A("11:14", "15:00"));

        Assert.Equal(0, e.PausenMinuten);
        Assert.Equal(30, e.PausenabzugMinuten);
    }

    [Fact]
    public void Block_ueber_sechs_Stunden_bekommt_15_Minuten_Pause()
    {
        // Beispiel aus dem Handbuch: 07–09 und 10–17 Uhr → um 16 Uhr werden 15 Minuten Pause ergänzt
        var e = Rechne(480, A("07:00", "09:00"), A("10:00", "17:00"));

        Assert.Equal(60, e.PausenMinuten);
        Assert.Equal(15, e.PausenabzugMinuten);
        Assert.Equal(525, e.ArbeitszeitMinuten);
    }

    [Fact]
    public void Ueber_neun_Stunden_werden_insgesamt_45_Minuten_Pause_verlangt()
    {
        var e = Rechne(480, A("07:00", "12:00"), A("12:30", "17:30"));

        Assert.Equal(30, e.PausenMinuten);
        Assert.Equal(15, e.PausenabzugMinuten);
        Assert.Equal(585, e.ArbeitszeitMinuten);
    }

    [Fact]
    public void Arbeitszeit_wird_nach_zehn_Stunden_gekappt()
    {
        var e = Rechne(480, A("07:00", "12:00"), A("12:45", "19:00"));

        // 6:15 Std. am Stück → 15 Min. Blockpause, danach 11:00 Std. netto → 1:00 Std. gekappt
        Assert.Equal(15, e.PausenabzugMinuten);
        Assert.Equal(600, e.ArbeitszeitMinuten);
        Assert.Equal(60, e.GekappteMinuten);
        Assert.Contains(e.Hinweise, h => h.Code == Regelcodes.ArbeitUeber10Stunden);
    }

    [Fact]
    public void Mit_Freigabe_wird_nicht_gekappt()
    {
        var e = _berechnung.Berechnen(new TagesEingabe
        {
            Datum = Tag,
            SollMinuten = 480,
            Buchungen = new[] { A("07:00", "12:00"), A("12:45", "19:00") },
            ErlaubtUeber10Stunden = true
        });

        Assert.Equal(660, e.ArbeitszeitMinuten);
        Assert.Equal(0, e.GekappteMinuten);
    }

    [Fact]
    public void Zeiten_ausserhalb_des_Rahmens_werden_ohne_Freigabe_nicht_gewertet()
    {
        var e = Rechne(480, A("05:30", "11:00"), A("11:30", "14:00"));

        Assert.Equal(30, e.VorRahmenMinuten);
        Assert.Equal(450, e.ArbeitszeitMinuten);
        Assert.Contains(e.Hinweise, h => h.Code == Regelcodes.ArbeitVorRahmen);
    }

    [Fact]
    public void Freigabe_nach_Rahmen_wertet_spaete_Arbeit()
    {
        var ohne = Rechne(0, A("18:00", "21:00"));
        var mit = _berechnung.Berechnen(new TagesEingabe
        {
            Datum = Tag,
            Buchungen = new[] { A("18:00", "21:00") },
            ErlaubtNachRahmen = true
        });

        Assert.Equal(120, ohne.ArbeitszeitMinuten);
        Assert.Equal(60, ohne.NachRahmenMinuten);
        Assert.Equal(180, mit.ArbeitszeitMinuten);
    }

    [Fact]
    public void Wegezeit_fuellt_zuerst_die_Sollzeit_auf_und_Rest_geht_aufs_Wegezeitkonto()
    {
        // Beispiel aus dem Handbuch (07.08.2024)
        var e = Rechne(480,
            A("07:30", "10:15"),
            A("11:00", "14:00", BuchungsArt.Wegezeit),
            A("14:00", "16:30"),
            A("17:00", "21:00", BuchungsArt.Wegezeit));

        Assert.Equal(315, e.NettoArbeitMinuten);
        Assert.Equal(360, e.WegezeitMinuten);
        Assert.Equal(60, e.NachRahmenMinuten); // 20–21 Uhr ohne Antrag „Arbeit nach Rahmen“ nicht gewertet
        Assert.Equal(165, e.WegezeitAufArbeitszeit);
        Assert.Equal(480, e.ArbeitszeitMinuten);
        Assert.Equal(195, e.WegezeitKontoDelta);
    }

    [Fact]
    public void Krank_untertaegig_fuellt_Sollzeit_auf()
    {
        var e = Rechne(480, A("08:00", "09:00"), A("09:00", null, BuchungsArt.KrankUntertaegig));

        Assert.Equal(60, e.NettoArbeitMinuten);
        Assert.Equal(420, e.KrankGutschrift);
        Assert.Equal(480, e.ArbeitszeitMinuten);
        Assert.DoesNotContain(e.Hinweise, h => h.Code == Regelcodes.FehlendeGehenBuchung);
    }

    [Fact]
    public void Fehlende_Gehen_Buchung_an_vergangenem_Tag_wird_gemeldet()
    {
        var e = Rechne(480, A("08:00", null));

        Assert.Equal(0, e.ArbeitszeitMinuten);
        Assert.Contains(e.Hinweise, h => h.Code == Regelcodes.FehlendeGehenBuchung);
    }

    [Fact]
    public void Offene_Buchung_am_heutigen_Tag_wird_bis_jetzt_gewertet()
    {
        var e = _berechnung.Berechnen(new TagesEingabe
        {
            Datum = Tag,
            SollMinuten = 480,
            Buchungen = new[] { A("08:00", null) },
            OffeneBuchungenBis = new TimeOnly(10, 30)
        });

        Assert.True(e.HatOffeneBuchung);
        Assert.Equal(150, e.ArbeitszeitMinuten);
    }

    [Fact]
    public void Arbeitstag_ohne_Buchung_ist_unentschuldigtes_Fehlen()
    {
        var e = Rechne(480);

        Assert.Equal(-480, e.SaldoDelta);
        Assert.Contains(e.Hinweise, h => h.Code == Regelcodes.FehltUnentschuldigt);
    }

    [Theory]
    [InlineData(AbwesenheitsWirkung.SollGutschrift, 480, 0, 0)]
    [InlineData(AbwesenheitsWirkung.Gleitzeitabbau, 0, 0, 0)]
    [InlineData(AbwesenheitsWirkung.AbbauSonderarbeitszeit, 480, -480, 0)]
    [InlineData(AbwesenheitsWirkung.AbbauWegezeit, 480, 0, -480)]
    public void Genehmigte_Abwesenheit_wirkt_auf_die_Konten(AbwesenheitsWirkung wirkung, int ist, int sonder, int wege)
    {
        var e = _berechnung.Berechnen(new TagesEingabe
        {
            Datum = Tag,
            SollMinuten = 480,
            Abwesenheit = new TagesAbwesenheit("X", "Test", "#fff", wirkung, Genehmigt: true),
            IstAbgeschlossen = true
        });

        Assert.Equal(ist, e.ArbeitszeitMinuten);
        Assert.Equal(sonder, e.SonderarbeitszeitDelta);
        Assert.Equal(wege, e.WegezeitKontoDelta);
        Assert.DoesNotContain(e.Hinweise, h => h.Code == Regelcodes.FehltUnentschuldigt);
    }

    [Fact]
    public void Arbeit_an_Urlaubstag_wird_nicht_gutgeschrieben()
    {
        var e = _berechnung.Berechnen(new TagesEingabe
        {
            Datum = Tag,
            SollMinuten = 480,
            Buchungen = new[] { A("08:00", "12:00") },
            Abwesenheit = new TagesAbwesenheit("U", "Urlaub", "#fff", AbwesenheitsWirkung.SollGutschrift, Genehmigt: true)
        });

        Assert.Equal(480, e.ArbeitszeitMinuten);
        Assert.Contains(e.Hinweise, h => h.Code == Regelcodes.ArbeitAnAbwesenheitstag);
    }

    [Fact]
    public void Beantragte_Abwesenheit_wird_noch_nicht_gutgeschrieben_ist_aber_kein_Fehlen()
    {
        var e = _berechnung.Berechnen(new TagesEingabe
        {
            Datum = Tag,
            SollMinuten = 480,
            Abwesenheit = new TagesAbwesenheit("U", "Urlaub", "#fff", AbwesenheitsWirkung.SollGutschrift, Genehmigt: false),
            IstAbgeschlossen = true
        });

        Assert.Equal(0, e.ArbeitszeitMinuten);
        Assert.DoesNotContain(e.Hinweise, h => h.Code == Regelcodes.FehltUnentschuldigt);
    }
}
