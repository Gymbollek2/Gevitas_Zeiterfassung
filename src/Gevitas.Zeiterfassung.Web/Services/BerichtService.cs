using System.Globalization;
using System.Text;
using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Options;
using Microsoft.Extensions.Options;
using static Gevitas.Zeiterfassung.Web.Services.Texte;

namespace Gevitas.Zeiterfassung.Web.Services;

public sealed record BerichtDefinition(int Nummer, string Bezeichnung, string Beschreibung, bool NurSalden, bool MitZeitraum);

public sealed record BerichtZeile(IReadOnlyList<string> Zellen, string? Ampel = null);

public sealed class BerichtDaten
{
    public required BerichtDefinition Definition { get; init; }
    public required string Untertitel { get; init; }
    public required IReadOnlyList<string> Spalten { get; init; }
    public List<BerichtZeile> Zeilen { get; } = new();
    public List<string> Hinweise { get; } = new();
}

/// <summary>
/// Stellt die Berichte bereit (Monatsjournal, Saldo-Ampel, Pausenabzug, Urlaubsübersicht,
/// Arbeitszeit größer 10, Ruhezeitverletzungen) inkl. CSV-Export.
/// </summary>
public sealed class BerichtService
{
    public static readonly IReadOnlyList<BerichtDefinition> Definitionen = new[]
    {
        new BerichtDefinition(500, "Monatsjournal", "Alle Buchungen und Tageswerte eines Zeitraums", NurSalden: false, MitZeitraum: true),
        new BerichtDefinition(539, "Saldo-Ampel", "Kontostände mit Ampel (eigene bzw. Team)", NurSalden: true, MitZeitraum: false),
        new BerichtDefinition(540, "Pausenabzug", "Tage mit automatisch ergänzten Pausen", NurSalden: false, MitZeitraum: true),
        new BerichtDefinition(542, "Urlaubsübersicht", "Anspruch, Übertrag, genommener und geplanter Urlaub", NurSalden: true, MitZeitraum: false),
        new BerichtDefinition(544, "Arbeitszeit größer 10", "Tage mit Arbeitszeit über der Kappungsgrenze", NurSalden: false, MitZeitraum: true),
        new BerichtDefinition(546, "Ruhezeitverletzungen", "Unterschreitungen der Mindestruhezeit", NurSalden: false, MitZeitraum: true)
    };

    private readonly KontenService _konten;
    private readonly UrlaubService _urlaub;
    private readonly KalenderService _kalender;
    private readonly IAppUhr _uhr;
    private readonly ZeiterfassungOptions _optionen;

    public BerichtService(KontenService konten, UrlaubService urlaub, KalenderService kalender, IAppUhr uhr, IOptions<ZeiterfassungOptions> optionen)
    {
        _konten = konten;
        _uhr = uhr;
        _urlaub = urlaub;
        _kalender = kalender;
        _optionen = optionen.Value;
    }

    public static BerichtDefinition? Definition(int nummer) => Definitionen.FirstOrDefault(d => d.Nummer == nummer);

    public async Task<BerichtDaten> ErstellenAsync(BerichtDefinition definition, IReadOnlyList<Mitarbeiter> personen, DateOnly von, DateOnly bis, CancellationToken ct = default) =>
        definition.Nummer switch
        {
            500 => await MonatsjournalAsync(definition, personen[0], von, bis, ct),
            539 => await SaldoAmpelAsync(definition, personen, ct),
            540 => await PausenabzugAsync(definition, personen[0], von, bis, ct),
            542 => await UrlaubsuebersichtAsync(definition, personen[0], von.Year, ct),
            544 => await UeberZehnAsync(definition, personen[0], von, bis, ct),
            546 => await RuhezeitAsync(definition, personen[0], von, bis, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(definition))
        };

    private async Task<BerichtDaten> MonatsjournalAsync(BerichtDefinition d, Mitarbeiter p, DateOnly von, DateOnly bis, CancellationToken ct)
    {
        var bericht = Neu(d, p, von, bis, "Datum", "Buchungen", "Abwesenheit", "Soll", "Ist", "Pausenabzug", "Tagessaldo", "Saldo", "Hinweise");
        var tage = await _konten.AuswertenAsync(p.Id, von, bis, ct);
        int soll = 0, ist = 0;
        foreach (var t in tage)
        {
            var e = t.Ergebnis;
            var buchungen = string.Join(", ", t.Buchungen.Select(b =>
                $"{Zeitformat.Uhrzeit(b.Beginn)}–{(b.Ende is null ? "…" : Zeitformat.Uhrzeit(b.Ende))}{(b.Art == BuchungsArt.Arbeit ? "" : " " + b.Art.Text())}"));
            var abwesenheit = e.Abwesenheit is null ? (t.Feiertag is null ? "" : T(t.Feiertag)) : $"{e.Abwesenheit.Code}{(e.Abwesenheit.Genehmigt ? "" : " " + T("(beantragt)"))}";
            bericht.Zeilen.Add(new BerichtZeile(new[]
            {
                Zeitformat.DatumMitWochentag(t.Datum),
                buchungen,
                abwesenheit,
                Zeitformat.Dauer(e.SollMinuten),
                t.IstZukunft ? "" : Zeitformat.Dauer(e.ArbeitszeitMinuten),
                e.PausenabzugMinuten > 0 ? Zeitformat.Dauer(e.PausenabzugMinuten) : "",
                t.IstZukunft ? "" : Zeitformat.Dauer(e.SaldoDelta),
                t.IstZukunft ? "" : Zeitformat.Dauer(t.SaldoKontostand),
                string.Join("; ", e.Hinweise.Select(h => h.Text))
            }));
            if (!t.IstZukunft)
            {
                soll += e.SollMinuten;
                ist += e.ArbeitszeitMinuten;
            }
        }

        bericht.Hinweise.Add(F("Summe Soll {0} · Summe Ist {1} · Differenz {2}", Zeitformat.Dauer(soll), Zeitformat.Dauer(ist), Zeitformat.Dauer(ist - soll)));
        return bericht;
    }

    private async Task<BerichtDaten> SaldoAmpelAsync(BerichtDefinition d, IReadOnlyList<Mitarbeiter> personen, CancellationToken ct)
    {
        var bericht = new BerichtDaten
        {
            Definition = d,
            Untertitel = F("Stand {0} · {1} Person(en)", Zeitformat.Zeitpunkt(_uhr.Jetzt), personen.Count),
            Spalten = new[] { T("Ampel"), T("Name"), T("Pers.-Nr."), T("Gleitzeitsaldo"), T("Wegezeit"), T("Sonderarbeitszeit"), T("Urlaubssaldo") }
        };

        foreach (var p in personen)
        {
            var stand = await _konten.KontostandAsync(p.Id, ct);
            var urlaub = await _urlaub.UebersichtAsync(p.Id, ct: ct);
            var betrag = Math.Abs(stand.Saldo);
            var ampel = betrag >= _optionen.Saldo.AmpelRotAbMinuten ? "rot" : betrag >= _optionen.Saldo.AmpelGelbAbMinuten ? "gelb" : "gruen";
            bericht.Zeilen.Add(new BerichtZeile(new[]
            {
                ampel switch { "rot" => T("Rot"), "gelb" => T("Gelb"), _ => T("Grün") },
                p.AnzeigeName,
                p.Personalnummer,
                Zeitformat.Dauer(stand.Saldo),
                Zeitformat.Dauer(stand.Wegezeit),
                Zeitformat.Dauer(stand.Sonderarbeitszeit),
                Zeitformat.Tage(urlaub.Urlaubssaldo)
            }, ampel));
        }

        bericht.Hinweise.Add(F("Grün bis ±{0} Std., Gelb bis ±{1} Std., darüber Rot.", Zeitformat.Dauer(_optionen.Saldo.AmpelGelbAbMinuten), Zeitformat.Dauer(_optionen.Saldo.AmpelRotAbMinuten)));
        return bericht;
    }

    private async Task<BerichtDaten> PausenabzugAsync(BerichtDefinition d, Mitarbeiter p, DateOnly von, DateOnly bis, CancellationToken ct)
    {
        var bericht = Neu(d, p, von, bis, "Datum", "Anwesenheit", "Anerkannte Pausen", "Pausenabzug", "Gewertete Arbeitszeit");
        foreach (var t in (await _konten.AuswertenAsync(p.Id, von, bis, ct)).Where(t => t.Ergebnis.PausenabzugMinuten > 0))
        {
            bericht.Zeilen.Add(new BerichtZeile(new[]
            {
                Zeitformat.DatumMitWochentag(t.Datum),
                Zeitformat.Dauer(t.Ergebnis.AnwesenheitMinuten),
                Zeitformat.Dauer(t.Ergebnis.PausenMinuten),
                Zeitformat.Dauer(t.Ergebnis.PausenabzugMinuten),
                Zeitformat.Dauer(t.Ergebnis.NettoArbeitMinuten)
            }));
        }

        bericht.Hinweise.Add(F("Nach {0} Std. mind. {1} Min., nach {2} Std. mind. {3} Min. Pause; Unterbrechungen ab {4} Min. zählen.", Zeitformat.Dauer(_optionen.Pausen.Schwelle1Minuten), _optionen.Pausen.Pause1Minuten, Zeitformat.Dauer(_optionen.Pausen.Schwelle2Minuten), _optionen.Pausen.Pause2Minuten, _optionen.Pausen.MinPauseMinuten));
        return bericht;
    }

    private async Task<BerichtDaten> UrlaubsuebersichtAsync(BerichtDefinition d, Mitarbeiter p, int jahr, CancellationToken ct)
    {
        var u = await _urlaub.UebersichtAsync(p.Id, jahr, ct);
        var bericht = new BerichtDaten
        {
            Definition = d,
            Untertitel = F("{0} ({1}) · Jahr {2}", p.AnzeigeName, p.Personalnummer, jahr),
            Spalten = new[] { T("Zeitraum"), T("Art"), T("Status"), T("Urlaubstage") }
        };

        var abwesenheiten = await _kalender.AbwesenheitenAsync(new[] { p.Id }, new DateOnly(jahr, 1, 1), new DateOnly(jahr, 12, 31), ct);
        foreach (var a in abwesenheiten.Where(a => a.Art?.ZaehltAlsUrlaub == true))
        {
            var tage = _urlaub.UrlaubstageImZeitraum(p, a.Von, a.Bis).Count(t => t.Year == jahr);
            bericht.Zeilen.Add(new BerichtZeile(new[]
            {
                $"{Zeitformat.Datum(a.Von)} – {Zeitformat.Datum(a.Bis)}",
                T(a.Art?.Name ?? a.ArtCode),
                a.Status.Text(),
                tage.ToString(CultureInfo.InvariantCulture)
            }));
        }

        bericht.Hinweise.Add(F("Anspruch {0} · Übertrag {1} (Verfall nach {2}, verfallen {3})", Zeitformat.Tage(u.Anspruch), Zeitformat.Tage(u.Uebertrag), Zeitformat.Datum(u.Verfall), Zeitformat.Tage(u.UebertragVerfallen)));
        bericht.Hinweise.Add(F("Genommen {0} · Geplant {1} · Urlaubssaldo {2} · Resturlaub (Plan 31.12.) {3}", Zeitformat.Tage(u.GenommenBisStichtag), Zeitformat.Tage(u.GeplantNachStichtag), Zeitformat.Tage(u.Urlaubssaldo), Zeitformat.Tage(u.ResturlaubPlan)));
        return bericht;
    }

    private async Task<BerichtDaten> UeberZehnAsync(BerichtDefinition d, Mitarbeiter p, DateOnly von, DateOnly bis, CancellationToken ct)
    {
        var bericht = Neu(d, p, von, bis, "Datum", "Arbeitszeit netto", "Gekappt", "Freigabe > 10 Std.");
        foreach (var t in await _konten.AuswertenAsync(p.Id, von, bis, ct))
        {
            var netto = t.Ergebnis.NettoArbeitMinuten + t.Ergebnis.GekappteMinuten;
            if (netto <= _optionen.MaxArbeitszeitMinuten)
            {
                continue;
            }

            bericht.Zeilen.Add(new BerichtZeile(new[]
            {
                Zeitformat.DatumMitWochentag(t.Datum),
                Zeitformat.Dauer(netto),
                Zeitformat.Dauer(t.Ergebnis.GekappteMinuten),
                t.Berechtigung?.ArbeitUeber10Stunden == true ? T("Ja") : T("Nein")
            }));
        }

        return bericht;
    }

    private async Task<BerichtDaten> RuhezeitAsync(BerichtDefinition d, Mitarbeiter p, DateOnly von, DateOnly bis, CancellationToken ct)
    {
        var bericht = Neu(d, p, von, bis, "Datum", "Ende Vortag", "Beginn", "Ruhezeit");
        TagesAuswertung? vortag = null;
        foreach (var t in await _konten.AuswertenAsync(p.Id, von.AddDays(-1), bis, ct))
        {
            var ruhe = RegelverletzungService.RuhezeitMinuten(vortag?.Ergebnis.LetztesEnde, t.Ergebnis.ErsterBeginn);
            if (t.Datum >= von && ruhe is int r && r < _optionen.MinRuhezeitMinuten)
            {
                bericht.Zeilen.Add(new BerichtZeile(new[]
                {
                    Zeitformat.DatumMitWochentag(t.Datum),
                    Uhr(vortag!.Ergebnis.LetztesEnde!.Value),
                    Uhr(t.Ergebnis.ErsterBeginn!.Value),
                    Zeitformat.Dauer(r)
                }));
            }

            vortag = t;
        }

        bericht.Hinweise.Add(F("Mindestruhezeit: {0} Std.", Zeitformat.Dauer(_optionen.MinRuhezeitMinuten)));
        return bericht;
    }

    public static byte[] AlsCsv(BerichtDaten bericht)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', bericht.Spalten.Select(Csv)));
        foreach (var zeile in bericht.Zeilen)
        {
            sb.AppendLine(string.Join(';', zeile.Zellen.Select(Csv)));
        }

        // UTF-8 mit BOM, damit Excel Umlaute korrekt anzeigt
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string Csv(string wert) =>
        wert.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0 ? $"\"{wert.Replace("\"", "\"\"")}\"" : wert;

    private static string Uhr(int minuten) => $"{minuten / 60:00}:{minuten % 60:00}";

    private static BerichtDaten Neu(BerichtDefinition d, Mitarbeiter p, DateOnly von, DateOnly bis, params string[] spalten) => new()
    {
        Definition = d,
        Untertitel = F("{0} ({1}) · {2} – {3}", p.AnzeigeName, p.Personalnummer, Zeitformat.Datum(von), Zeitformat.Datum(bis)),
        Spalten = spalten.Select(T).ToArray()
    };
}
