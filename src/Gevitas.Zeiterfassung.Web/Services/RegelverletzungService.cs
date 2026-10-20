using Gevitas.Zeiterfassung.Web.Data;
using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using static Gevitas.Zeiterfassung.Web.Services.Texte;

namespace Gevitas.Zeiterfassung.Web.Services;

public sealed record Regelverletzung(DateOnly Datum, string Code, string Beschreibung, string? Info, bool Quittiert, bool Quittierbar);

/// <summary>
/// Ermittelt Buchungen mit Besonderheiten (fehlende Buchungen, Arbeitszeit &gt; 10 Std., Rahmenverletzungen,
/// Saldo-Grenzen, Ruhezeit) und verwaltet deren Quittierung durch die Mitarbeitenden.
/// </summary>
public sealed class RegelverletzungService
{
    /// <summary>Diese Hinweise erscheinen nicht als Regelverletzung (rein informativ).</summary>
    private static readonly HashSet<string> NurInformativ = new() { Regelcodes.PauseErgaenzt };

    /// <summary>Diese Verletzungen müssen korrigiert werden und können nicht quittiert werden.</summary>
    private static readonly HashSet<string> NichtQuittierbar = new() { Regelcodes.FehlendeGehenBuchung };

    private readonly ZeiterfassungDbContext _db;
    private readonly KontenService _konten;
    private readonly IAppUhr _uhr;
    private readonly ZeiterfassungOptions _optionen;

    public RegelverletzungService(ZeiterfassungDbContext db, KontenService konten, IAppUhr uhr, IOptions<ZeiterfassungOptions> optionen)
    {
        _db = db;
        _konten = konten;
        _uhr = uhr;
        _optionen = optionen.Value;
    }

    public async Task<List<Regelverletzung>> ErmittelnAsync(int mitarbeiterId, DateOnly von, DateOnly bis, bool mitQuittierten, CancellationToken ct = default)
    {
        // Nur abgeschlossene Tage prüfen
        var letzterTag = _uhr.Heute.AddDays(-1);
        if (bis > letzterTag)
        {
            bis = letzterTag;
        }

        if (bis < von)
        {
            return new List<Regelverletzung>();
        }

        var tage = await _konten.AuswertenAsync(mitarbeiterId, von.AddDays(-1), bis, ct);
        var quittiert = await _db.Regelquittierungen.AsNoTracking()
            .Where(q => q.MitarbeiterId == mitarbeiterId && q.Datum >= von && q.Datum <= bis)
            .Select(q => new { q.Datum, q.Regelcode })
            .ToListAsync(ct);
        var quittiertSet = quittiert.Select(q => (q.Datum, q.Regelcode)).ToHashSet();

        var ergebnis = new List<Regelverletzung>();
        TagesAuswertung? vortag = null;
        foreach (var tag in tage)
        {
            if (tag.Datum >= von)
            {
                var hinweise = tag.Ergebnis.Hinweise.Where(h => !NurInformativ.Contains(h.Code)).ToList();
                var ruhe = RuhezeitMinuten(vortag?.Ergebnis.LetztesEnde, tag.Ergebnis.ErsterBeginn);
                if (ruhe is int r && r < _optionen.MinRuhezeitMinuten)
                {
                    hinweise.Add(new Regelhinweis(Regelcodes.Ruhezeit,
                        F("Ruhezeit unterschritten ({0} statt {1} Std.)", Zeitformat.Dauer(r), Zeitformat.Dauer(_optionen.MinRuhezeitMinuten))));
                }

                foreach (var h in hinweise)
                {
                    var istQuittiert = quittiertSet.Contains((tag.Datum, h.Code));
                    if (istQuittiert && !mitQuittierten)
                    {
                        continue;
                    }

                    ergebnis.Add(new Regelverletzung(tag.Datum, h.Code, h.Text, Info(tag, h.Code), istQuittiert, !NichtQuittierbar.Contains(h.Code)));
                }
            }

            vortag = tag;
        }

        return ergebnis;
    }

    public async Task<Ergebnis> QuittierenAsync(int mitarbeiterId, DateOnly datum, string code, bool quittieren, CancellationToken ct = default)
    {
        if (NichtQuittierbar.Contains(code))
        {
            return Ergebnis.Fehler(T("Diese Regelverletzung muss über die Zeitkorrekturen behoben werden."));
        }

        var vorhanden = await _db.Regelquittierungen.FirstOrDefaultAsync(q => q.MitarbeiterId == mitarbeiterId && q.Datum == datum && q.Regelcode == code, ct);
        if (quittieren && vorhanden is null)
        {
            _db.Regelquittierungen.Add(new Regelquittierung { MitarbeiterId = mitarbeiterId, Datum = datum, Regelcode = code, QuittiertAm = _uhr.Jetzt });
        }
        else if (!quittieren && vorhanden is not null)
        {
            _db.Regelquittierungen.Remove(vorhanden);
        }

        await _db.SaveChangesAsync(ct);
        return Ergebnis.Ok(quittieren ? F("Hinweis vom {0} quittiert.", Zeitformat.Datum(datum)) : F("Quittierung vom {0} aufgehoben.", Zeitformat.Datum(datum)));
    }

    /// <summary>Ruhezeit zwischen dem letzten Ende des Vortags und dem ersten Beginn des Tages.</summary>
    public static int? RuhezeitMinuten(int? letztesEndeVortag, int? ersterBeginn) =>
        letztesEndeVortag is int ende && ersterBeginn is int beginn ? 24 * 60 - ende + beginn : null;

    private string? Info(TagesAuswertung tag, string code) => code switch
    {
        Regelcodes.FehltUnentschuldigt => $"{_optionen.Rahmenarbeitszeit.Beginn} - {_optionen.Rahmenarbeitszeit.Ende}",
        Regelcodes.ArbeitUeber10Stunden => $"Netto {Zeitformat.Dauer(tag.Ergebnis.NettoArbeitMinuten + tag.Ergebnis.GekappteMinuten)}",
        Regelcodes.SaldoUnterUntergrenze or Regelcodes.SaldoUeberObergrenze => $"Saldo {Zeitformat.Dauer(tag.SaldoKontostand)}",
        _ => null
    };
}
