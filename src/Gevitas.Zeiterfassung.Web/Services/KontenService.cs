using Gevitas.Zeiterfassung.Web.Data;
using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using static Gevitas.Zeiterfassung.Web.Services.Texte;

namespace Gevitas.Zeiterfassung.Web.Services;

public sealed class TagesAuswertung
{
    public DateOnly Datum { get; init; }
    public bool IstArbeitstag { get; init; }
    public string? Feiertag { get; init; }
    public bool IstZukunft { get; init; }
    public bool IstHeute { get; init; }
    public List<Zeitbuchung> Buchungen { get; init; } = new();
    public TagesErgebnis Ergebnis { get; init; } = null!;
    public Tagesberechtigung? Berechtigung { get; init; }
    public string? Bemerkung { get; init; }
    public List<Kontobuchung> Kontobuchungen { get; init; } = new();

    /// <summary>Kontostände am Ende des Tages (inkl. manueller Kontobuchungen).</summary>
    public int SaldoKontostand { get; set; }
    public int WegezeitKontostand { get; set; }
    public int SonderarbeitszeitKontostand { get; set; }
}

public sealed record Kontostand(
    int Saldo,
    int Wegezeit,
    int Sonderarbeitszeit,
    int HeuteArbeitszeit,
    int HeuteSoll,
    bool Anwesend,
    BuchungsArt? OffeneArt);

/// <summary>
/// Wertet die Zeitbuchungen eines Mitarbeiters tageweise aus und führt die Konten
/// Gleitzeitsaldo, Wegezeit und Sonderarbeitszeit fort.
/// </summary>
public sealed class KontenService
{
    private readonly ZeiterfassungDbContext _db;
    private readonly Arbeitstagkalender _kalender;
    private readonly Tagesberechnung _berechnung;
    private readonly IAppUhr _uhr;
    private readonly SaldoOptions _saldo;

    public KontenService(
        ZeiterfassungDbContext db,
        Arbeitstagkalender kalender,
        Tagesberechnung berechnung,
        IAppUhr uhr,
        IOptions<ZeiterfassungOptions> optionen)
    {
        _db = db;
        _kalender = kalender;
        _berechnung = berechnung;
        _uhr = uhr;
        _saldo = optionen.Value.Saldo;
    }

    /// <summary>
    /// Liefert die Auswertung für jeden Tag im Zeitraum. Die Kontostände werden ab dem Erfassungsbeginn
    /// fortgeschrieben; Tage in der Zukunft verändern die Konten nicht.
    /// </summary>
    public async Task<List<TagesAuswertung>> AuswertenAsync(int mitarbeiterId, DateOnly von, DateOnly bis, CancellationToken ct = default)
    {
        var mitarbeiter = await _db.Mitarbeiter.AsNoTracking().FirstAsync(m => m.Id == mitarbeiterId, ct);
        var heute = _uhr.Heute;
        var start = mitarbeiter.Erfassungsbeginn < von ? mitarbeiter.Erfassungsbeginn : von;

        var buchungen = await _db.Zeitbuchungen.AsNoTracking()
            .Where(b => b.MitarbeiterId == mitarbeiterId && b.Datum >= start && b.Datum <= bis)
            .ToListAsync(ct);
        var abwesenheiten = await _db.Abwesenheiten.AsNoTracking().Include(a => a.Art)
            .Where(a => a.MitarbeiterId == mitarbeiterId && a.Bis >= start && a.Von <= bis
                        && (a.Status == AbwesenheitsStatus.Genehmigt || a.Status == AbwesenheitsStatus.Beantragt))
            .ToListAsync(ct);
        var berechtigungen = await _db.Tagesberechtigungen.AsNoTracking()
            .Where(t => t.MitarbeiterId == mitarbeiterId && t.Datum >= start && t.Datum <= bis)
            .ToDictionaryAsync(t => t.Datum, ct);
        var bemerkungen = await _db.Tagesbemerkungen.AsNoTracking()
            .Where(t => t.MitarbeiterId == mitarbeiterId && t.Datum >= von && t.Datum <= bis)
            .ToDictionaryAsync(t => t.Datum, t => t.Text, ct);
        var kontobuchungen = await _db.Kontobuchungen.AsNoTracking()
            .Where(k => k.MitarbeiterId == mitarbeiterId && !k.Storniert && k.Datum <= bis)
            .ToListAsync(ct);

        var buchungenNachTag = buchungen.GroupBy(b => b.Datum).ToDictionary(g => g.Key, g => g.OrderBy(b => b.Beginn).ToList());
        var kontobuchungenNachTag = kontobuchungen.GroupBy(k => k.Datum).ToDictionary(g => g.Key, g => g.ToList());

        // Kontobuchungen vor dem Startdatum fließen als Anfangsbestand ein
        var saldo = kontobuchungen.Where(k => k.Datum < start && k.Konto == Konto.Saldo).Sum(k => k.Minuten);
        var wegezeit = kontobuchungen.Where(k => k.Datum < start && k.Konto == Konto.Wegezeit).Sum(k => k.Minuten);
        var sonder = kontobuchungen.Where(k => k.Datum < start && k.Konto == Konto.Sonderarbeitszeit).Sum(k => k.Minuten);

        var ergebnis = new List<TagesAuswertung>();
        for (var datum = start; datum <= bis; datum = datum.AddDays(1))
        {
            var istZukunft = datum > heute;
            var tagesBuchungen = buchungenNachTag.GetValueOrDefault(datum) ?? new List<Zeitbuchung>();
            berechtigungen.TryGetValue(datum, out var berechtigung);
            var soll = _kalender.SollMinuten(mitarbeiter, datum);
            var abwesenheit = AbwesenheitFuer(abwesenheiten, datum);

            var eingabe = new TagesEingabe
            {
                Datum = datum,
                SollMinuten = soll,
                Buchungen = tagesBuchungen.Select(b => new BuchungsIntervall(b.Beginn, b.Ende, b.Art)).ToList(),
                Abwesenheit = abwesenheit,
                ErlaubtUeber10Stunden = berechtigung?.ArbeitUeber10Stunden == true,
                ErlaubtVorRahmen = berechtigung?.ArbeitVorRahmen == true,
                ErlaubtNachRahmen = berechtigung?.ArbeitNachRahmen == true,
                OffeneBuchungenBis = datum == heute ? _uhr.UhrzeitMinute : null,
                IstAbgeschlossen = datum < heute && datum >= mitarbeiter.Erfassungsbeginn
            };
            var tag = _berechnung.Berechnen(eingabe);
            var tagesKonto = kontobuchungenNachTag.GetValueOrDefault(datum) ?? new List<Kontobuchung>();

            if (!istZukunft && datum >= mitarbeiter.Erfassungsbeginn)
            {
                saldo += tag.SaldoDelta;
                wegezeit += tag.WegezeitKontoDelta;
                sonder += tag.SonderarbeitszeitDelta;
            }

            saldo += tagesKonto.Where(k => k.Konto == Konto.Saldo).Sum(k => k.Minuten);
            wegezeit += tagesKonto.Where(k => k.Konto == Konto.Wegezeit).Sum(k => k.Minuten);
            sonder += tagesKonto.Where(k => k.Konto == Konto.Sonderarbeitszeit).Sum(k => k.Minuten);

            if (eingabe.IstAbgeschlossen)
            {
                if (saldo < _saldo.UntergrenzeMinuten)
                {
                    tag.Hinweise.Add(new Regelhinweis(Regelcodes.SaldoUnterUntergrenze, T("Saldo unterschreitet zulässige Untergrenze")));
                }
                else if (saldo > _saldo.ObergrenzeMinuten)
                {
                    tag.Hinweise.Add(new Regelhinweis(Regelcodes.SaldoUeberObergrenze, T("Saldo überschreitet zulässige Obergrenze")));
                }
            }

            if (datum < von)
            {
                continue;
            }

            _kalender.IstFeiertag(datum, out var feiertag);
            ergebnis.Add(new TagesAuswertung
            {
                Datum = datum,
                IstArbeitstag = _kalender.IstArbeitstag(datum),
                Feiertag = string.IsNullOrEmpty(feiertag) ? null : feiertag,
                IstZukunft = istZukunft,
                IstHeute = datum == heute,
                Buchungen = tagesBuchungen,
                Ergebnis = tag,
                Berechtigung = berechtigung,
                Bemerkung = bemerkungen.GetValueOrDefault(datum),
                Kontobuchungen = tagesKonto,
                SaldoKontostand = saldo,
                WegezeitKontostand = wegezeit,
                SonderarbeitszeitKontostand = sonder
            });
        }

        return ergebnis;
    }

    public async Task<Kontostand> KontostandAsync(int mitarbeiterId, CancellationToken ct = default)
    {
        var heute = _uhr.Heute;
        var tag = (await AuswertenAsync(mitarbeiterId, heute, heute, ct)).Single();
        var offen = tag.Buchungen.FirstOrDefault(b => b.Ende is null && b.Art != BuchungsArt.KrankUntertaegig);
        return new Kontostand(
            tag.SaldoKontostand,
            tag.WegezeitKontostand,
            tag.SonderarbeitszeitKontostand,
            tag.Ergebnis.ArbeitszeitMinuten,
            tag.Ergebnis.SollMinuten,
            offen is not null,
            offen?.Art);
    }

    /// <summary>Gleitzeitsaldo am Ende des angegebenen Tages.</summary>
    public async Task<int> SaldoAmAsync(int mitarbeiterId, DateOnly datum, CancellationToken ct = default) =>
        (await AuswertenAsync(mitarbeiterId, datum, datum, ct)).Single().SaldoKontostand;

    private static TagesAbwesenheit? AbwesenheitFuer(List<Abwesenheit> abwesenheiten, DateOnly datum)
    {
        var treffer = abwesenheiten
            .Where(a => a.Umfasst(datum))
            .OrderBy(a => a.Status == AbwesenheitsStatus.Genehmigt ? 0 : 1)
            .FirstOrDefault();
        if (treffer?.Art is null)
        {
            return null;
        }

        return new TagesAbwesenheit(
            treffer.ArtCode,
            treffer.Art.Name,
            treffer.Art.Farbe,
            treffer.Art.Wirkung,
            treffer.Status == AbwesenheitsStatus.Genehmigt);
    }
}
