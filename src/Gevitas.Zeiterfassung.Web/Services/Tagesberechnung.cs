using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Options;

namespace Gevitas.Zeiterfassung.Web.Services;

/// <summary>Arbeitszeitregeln in Minuten seit Mitternacht bzw. Minuten Dauer.</summary>
public sealed record ArbeitszeitRegeln(
    int RahmenBeginn,
    int RahmenEnde,
    int MaxArbeitszeit,
    int MinPause,
    int Schwelle1,
    int Pause1,
    int Schwelle2,
    int Pause2,
    int MaxBlockOhnePause,
    int BlockPause)
{
    public static ArbeitszeitRegeln Aus(ZeiterfassungOptions o) => new(
        Zeitformat.MinutenSeitMitternacht(o.Rahmenarbeitszeit.BeginnZeit),
        Zeitformat.MinutenSeitMitternacht(o.Rahmenarbeitszeit.EndeZeit),
        o.MaxArbeitszeitMinuten,
        o.Pausen.MinPauseMinuten,
        o.Pausen.Schwelle1Minuten,
        o.Pausen.Pause1Minuten,
        o.Pausen.Schwelle2Minuten,
        o.Pausen.Pause2Minuten,
        o.Pausen.MaxBlockOhnePauseMinuten,
        o.Pausen.BlockPauseMinuten);

    public static ArbeitszeitRegeln Standard { get; } = Aus(new ZeiterfassungOptions());
}

public sealed record BuchungsIntervall(TimeOnly Beginn, TimeOnly? Ende, BuchungsArt Art);

public sealed record TagesAbwesenheit(string Code, string Name, string Farbe, AbwesenheitsWirkung Wirkung, bool Genehmigt);

public sealed record Regelhinweis(string Code, string Text);

public static class Regelcodes
{
    public const string FehlendeGehenBuchung = "FEHLT_GEHEN";
    public const string ArbeitUeber10Stunden = "UEBER_10H";
    public const string ArbeitVorRahmen = "VOR_RAHMEN";
    public const string ArbeitNachRahmen = "NACH_RAHMEN";
    public const string PauseErgaenzt = "PAUSE";
    public const string FehltUnentschuldigt = "FEHLT";
    public const string ArbeitAnAbwesenheitstag = "ARBEIT_ABWESEND";
    public const string SaldoUnterUntergrenze = "SALDO_UNTEN";
    public const string SaldoUeberObergrenze = "SALDO_OBEN";
    public const string Ruhezeit = "RUHEZEIT";
}

public sealed class TagesEingabe
{
    public DateOnly Datum { get; init; }
    public int SollMinuten { get; init; }
    public IReadOnlyList<BuchungsIntervall> Buchungen { get; init; } = Array.Empty<BuchungsIntervall>();
    public TagesAbwesenheit? Abwesenheit { get; init; }
    public bool ErlaubtUeber10Stunden { get; init; }
    public bool ErlaubtVorRahmen { get; init; }
    public bool ErlaubtNachRahmen { get; init; }

    /// <summary>Für den laufenden Tag: offene Buchungen werden bis zu dieser Uhrzeit gewertet.</summary>
    public TimeOnly? OffeneBuchungenBis { get; init; }

    /// <summary>true für vergangene Tage (Fehlzeiten-Prüfung).</summary>
    public bool IstAbgeschlossen { get; init; }
}

public sealed class TagesErgebnis
{
    public DateOnly Datum { get; init; }
    public int SollMinuten { get; init; }

    /// <summary>Gebuchte Arbeitszeit vor Pausenabzug (innerhalb des gewerteten Rahmens).</summary>
    public int AnwesenheitMinuten { get; set; }

    /// <summary>Anerkannte Pausen (Unterbrechungen ab der Mindestdauer).</summary>
    public int PausenMinuten { get; set; }

    public int PausenabzugMinuten { get; set; }
    public int GekappteMinuten { get; set; }
    public int VorRahmenMinuten { get; set; }
    public int NachRahmenMinuten { get; set; }

    /// <summary>Arbeitszeit nach Pausenabzug und Kappung (ohne Wegezeit und Gutschriften).</summary>
    public int NettoArbeitMinuten { get; set; }

    public int WegezeitMinuten { get; set; }
    public int WegezeitAufArbeitszeit { get; set; }
    public int KrankGutschrift { get; set; }
    public int AbwesenheitsGutschrift { get; set; }

    /// <summary>Gewertete Ist-Zeit für das Gleitzeitkonto.</summary>
    public int ArbeitszeitMinuten { get; set; }

    public int SaldoDelta => ArbeitszeitMinuten - SollMinuten;
    public int WegezeitKontoDelta { get; set; }
    public int SonderarbeitszeitDelta { get; set; }

    public TagesAbwesenheit? Abwesenheit { get; set; }
    public bool HatOffeneBuchung { get; set; }
    public int? ErsterBeginn { get; set; }
    public int? LetztesEnde { get; set; }
    public List<Regelhinweis> Hinweise { get; } = new();
}

/// <summary>
/// Reine Berechnungslogik für einen Arbeitstag (ohne Datenbankzugriff, vollständig testbar):
/// Rahmenarbeitszeit, gesetzlicher Pausenabzug, Kappung nach 10 Stunden, Wegezeit, Krank untertägig und Abwesenheiten.
/// </summary>
public sealed class Tagesberechnung
{
    private readonly ArbeitszeitRegeln _regeln;

    public Tagesberechnung(ArbeitszeitRegeln regeln)
    {
        _regeln = regeln;
    }

    public TagesErgebnis Berechnen(TagesEingabe e)
    {
        var r = new TagesErgebnis { Datum = e.Datum, SollMinuten = e.SollMinuten };
        var arbeit = new List<(int Beginn, int Ende)>();
        var wege = new List<(int Beginn, int Ende)>();
        int? krankAb = null;
        var fehlendesEnde = false;

        foreach (var b in e.Buchungen)
        {
            var beginn = Zeitformat.MinutenSeitMitternacht(b.Beginn);
            if (b.Art == BuchungsArt.KrankUntertaegig)
            {
                krankAb = krankAb is null ? beginn : Math.Min(krankAb.Value, beginn);
                continue;
            }

            int ende;
            if (b.Ende is { } bekanntesEnde)
            {
                ende = Zeitformat.MinutenSeitMitternacht(bekanntesEnde);
            }
            else if (e.OffeneBuchungenBis is { } bis)
            {
                ende = Zeitformat.MinutenSeitMitternacht(bis);
                r.HatOffeneBuchung = true;
            }
            else
            {
                fehlendesEnde = true;
                continue;
            }

            if (ende <= beginn)
            {
                continue;
            }

            (b.Art == BuchungsArt.Wegezeit ? wege : arbeit).Add((beginn, ende));
        }

        if (fehlendesEnde)
        {
            r.Hinweise.Add(new Regelhinweis(Regelcodes.FehlendeGehenBuchung, "Fehlende Gehen-Buchung"));
        }

        var alle = arbeit.Concat(wege).ToList();
        if (alle.Count > 0)
        {
            r.ErsterBeginn = alle.Min(i => i.Beginn);
            r.LetztesEnde = alle.Max(i => i.Ende);
        }

        // Rahmenarbeitszeit: Zeiten außerhalb werden ohne Freigabe nicht gewertet
        arbeit = Vereinigen(Zuschneiden(arbeit, e, r));
        wege = Vereinigen(Zuschneiden(wege, e, r));
        if (r.VorRahmenMinuten > 0)
        {
            r.Hinweise.Add(new Regelhinweis(Regelcodes.ArbeitVorRahmen,
                $"Arbeit vor Rahmen ({Zeitformat.Dauer(r.VorRahmenMinuten)} nicht gewertet)"));
        }

        if (r.NachRahmenMinuten > 0)
        {
            r.Hinweise.Add(new Regelhinweis(Regelcodes.ArbeitNachRahmen,
                $"Arbeit nach Rahmen ({Zeitformat.Dauer(r.NachRahmenMinuten)} nicht gewertet)"));
        }

        r.AnwesenheitMinuten = arbeit.Sum(i => i.Ende - i.Beginn);
        r.WegezeitMinuten = wege.Sum(i => i.Ende - i.Beginn);

        // Ganztägige, genehmigte Abwesenheit: gebuchte Zeiten werden nicht gutgeschrieben
        if (e.Abwesenheit is { Genehmigt: true } abwesenheit)
        {
            r.Abwesenheit = abwesenheit;
            if (r.AnwesenheitMinuten > 0 || r.WegezeitMinuten > 0)
            {
                r.Hinweise.Add(new Regelhinweis(Regelcodes.ArbeitAnAbwesenheitstag,
                    $"Arbeitszeit an Abwesenheitstag ({abwesenheit.Name}) – bitte Abwesenheit stornieren, sonst wird die Zeit nicht gutgeschrieben"));
            }

            switch (abwesenheit.Wirkung)
            {
                case AbwesenheitsWirkung.SollGutschrift:
                    r.AbwesenheitsGutschrift = e.SollMinuten;
                    break;
                case AbwesenheitsWirkung.Gleitzeitabbau:
                    r.AbwesenheitsGutschrift = 0;
                    break;
                case AbwesenheitsWirkung.AbbauSonderarbeitszeit:
                    r.AbwesenheitsGutschrift = e.SollMinuten;
                    r.SonderarbeitszeitDelta = -e.SollMinuten;
                    break;
                case AbwesenheitsWirkung.AbbauWegezeit:
                    r.AbwesenheitsGutschrift = e.SollMinuten;
                    r.WegezeitKontoDelta = -e.SollMinuten;
                    break;
            }

            r.ArbeitszeitMinuten = r.AbwesenheitsGutschrift;
            return r;
        }

        if (e.Abwesenheit is not null)
        {
            r.Abwesenheit = e.Abwesenheit;
        }

        // Gesetzlicher Pausenabzug
        r.PausenMinuten = AnerkanntePausen(Vereinigen(arbeit.Concat(wege).ToList()));
        var netto = NettoNachPausen(arbeit, r.PausenMinuten);
        r.PausenabzugMinuten = r.AnwesenheitMinuten - netto;
        if (r.PausenabzugMinuten > 0)
        {
            r.Hinweise.Add(new Regelhinweis(Regelcodes.PauseErgaenzt,
                $"Pause ergänzt ({Zeitformat.Dauer(r.PausenabzugMinuten)})"));
        }

        // Kappung nach 10 Stunden
        if (netto > _regeln.MaxArbeitszeit && !e.ErlaubtUeber10Stunden)
        {
            r.GekappteMinuten = netto - _regeln.MaxArbeitszeit;
            netto = _regeln.MaxArbeitszeit;
            r.Hinweise.Add(new Regelhinweis(Regelcodes.ArbeitUeber10Stunden,
                $"Arbeitszeit > {Zeitformat.Dauer(_regeln.MaxArbeitszeit)} Std. ({Zeitformat.Dauer(r.GekappteMinuten)} gekappt)"));
        }

        r.NettoArbeitMinuten = netto;

        // Wegezeit füllt zuerst die Sollarbeitszeit auf, der Rest geht auf das Wegezeitkonto
        r.WegezeitAufArbeitszeit = Math.Min(r.WegezeitMinuten, Math.Max(0, e.SollMinuten - netto));
        r.WegezeitKontoDelta = r.WegezeitMinuten - r.WegezeitAufArbeitszeit;

        // Krank untertägig füllt den Rest der Sollarbeitszeit auf
        if (krankAb is not null)
        {
            r.KrankGutschrift = Math.Max(0, e.SollMinuten - netto - r.WegezeitAufArbeitszeit);
        }

        r.ArbeitszeitMinuten = netto + r.WegezeitAufArbeitszeit + r.KrankGutschrift;

        if (e.IstAbgeschlossen && e.SollMinuten > 0 && e.Buchungen.Count == 0 && e.Abwesenheit is null)
        {
            r.Hinweise.Add(new Regelhinweis(Regelcodes.FehltUnentschuldigt, "Fehlt unentschuldigt"));
        }

        return r;
    }

    private List<(int Beginn, int Ende)> Zuschneiden(List<(int Beginn, int Ende)> intervalle, TagesEingabe e, TagesErgebnis r)
    {
        var ergebnis = new List<(int Beginn, int Ende)>();
        foreach (var (b, en) in intervalle)
        {
            var beginn = b;
            var ende = en;
            if (!e.ErlaubtVorRahmen && beginn < _regeln.RahmenBeginn)
            {
                r.VorRahmenMinuten += Math.Min(ende, _regeln.RahmenBeginn) - beginn;
                beginn = Math.Max(beginn, _regeln.RahmenBeginn);
            }

            if (!e.ErlaubtNachRahmen && ende > _regeln.RahmenEnde)
            {
                r.NachRahmenMinuten += ende - Math.Max(beginn, _regeln.RahmenEnde);
                ende = Math.Min(ende, _regeln.RahmenEnde);
            }

            if (ende > beginn)
            {
                ergebnis.Add((beginn, ende));
            }
        }

        return ergebnis;
    }

    /// <summary>Sortiert und verschmilzt überlappende Intervalle.</summary>
    internal static List<(int Beginn, int Ende)> Vereinigen(List<(int Beginn, int Ende)> intervalle)
    {
        var ergebnis = new List<(int Beginn, int Ende)>();
        foreach (var i in intervalle.OrderBy(x => x.Beginn))
        {
            if (ergebnis.Count > 0 && i.Beginn <= ergebnis[^1].Ende)
            {
                var letztes = ergebnis[^1];
                ergebnis[^1] = (letztes.Beginn, Math.Max(letztes.Ende, i.Ende));
            }
            else
            {
                ergebnis.Add(i);
            }
        }

        return ergebnis;
    }

    private int AnerkanntePausen(List<(int Beginn, int Ende)> sortiert)
    {
        var summe = 0;
        for (var i = 1; i < sortiert.Count; i++)
        {
            var luecke = sortiert[i].Beginn - sortiert[i - 1].Ende;
            if (luecke >= _regeln.MinPause)
            {
                summe += luecke;
            }
        }

        return summe;
    }

    /// <summary>
    /// Wendet die Pausenregeln an: Arbeitszeit jenseits der Schwellen wird erst gezählt, wenn die
    /// erforderliche Pause erreicht ist. Zusätzlich darf kein Block länger als die Höchstdauer ohne Pause sein.
    /// </summary>
    private int NettoNachPausen(List<(int Beginn, int Ende)> arbeit, int pausen)
    {
        var brutto = arbeit.Sum(i => i.Ende - i.Beginn);
        var fehlend1 = Math.Max(0, _regeln.Pause1 - pausen);
        var netto = brutto;
        if (brutto > _regeln.Schwelle1)
        {
            netto = _regeln.Schwelle1 + Math.Max(0, brutto - _regeln.Schwelle1 - fehlend1);
        }

        var fehlend2 = Math.Max(0, _regeln.Pause2 - pausen - fehlend1);
        if (netto > _regeln.Schwelle2)
        {
            netto = _regeln.Schwelle2 + Math.Max(0, netto - _regeln.Schwelle2 - fehlend2);
        }

        // Blöcke ohne anerkannte Pause (Unterbrechungen unter der Mindestdauer zählen nicht)
        var blockAbzug = 0;
        var blockBeginn = -1;
        var blockEnde = -1;
        foreach (var (b, en) in arbeit)
        {
            if (blockBeginn < 0)
            {
                blockBeginn = b;
                blockEnde = en;
            }
            else if (b - blockEnde < _regeln.MinPause)
            {
                blockEnde = en;
            }
            else
            {
                blockAbzug += BlockAbzug(blockEnde - blockBeginn);
                blockBeginn = b;
                blockEnde = en;
            }
        }

        if (blockBeginn >= 0)
        {
            blockAbzug += BlockAbzug(blockEnde - blockBeginn);
        }

        return Math.Max(0, Math.Min(netto, brutto - blockAbzug));
    }

    private int BlockAbzug(int laenge) =>
        laenge > _regeln.MaxBlockOhnePause ? Math.Min(_regeln.BlockPause, laenge - _regeln.MaxBlockOhnePause) : 0;
}
