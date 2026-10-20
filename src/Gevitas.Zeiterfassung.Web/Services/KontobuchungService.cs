using Gevitas.Zeiterfassung.Web.Data;
using Gevitas.Zeiterfassung.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gevitas.Zeiterfassung.Web.Services;

/// <summary>
/// Umbuchungen (Sonderarbeitszeit/Wegezeit → Gleitzeitsaldo) durch Mitarbeitende sowie
/// Kontokorrekturen und Stornos durch die Personalabteilung.
/// </summary>
public sealed class KontobuchungService
{
    private readonly ZeiterfassungDbContext _db;
    private readonly IAppUhr _uhr;

    public KontobuchungService(ZeiterfassungDbContext db, IAppUhr uhr)
    {
        _db = db;
        _uhr = uhr;
    }

    public async Task<Ergebnis> UmbuchenAsync(int mitarbeiterId, Konto quelle, string? stunden, DateOnly datum, string? bemerkung, int ausfuehrenderId, CancellationToken ct = default)
    {
        if (quelle is not (Konto.Sonderarbeitszeit or Konto.Wegezeit))
        {
            return Ergebnis.Fehler("Umbuchungen sind nur vom Sonderarbeitszeit- oder Wegezeitkonto möglich.");
        }

        if (!Zeitformat.TryParseDauer(stunden, out var minuten) || minuten <= 0)
        {
            return Ergebnis.Fehler("Bitte den Kontobuchungswert im Format Std:Min angeben (z. B. 2:30).");
        }

        if (datum > _uhr.Heute)
        {
            return Ergebnis.Fehler("Umbuchungen in der Zukunft sind nicht möglich.");
        }

        var vorgang = Guid.NewGuid();
        var text = quelle == Konto.Sonderarbeitszeit ? "Umbuchung Sonderarbeitszeitkonto auf Saldo" : "Umbuchung Wegezeit auf Saldo";
        _db.Kontobuchungen.AddRange(
            Buchung(mitarbeiterId, datum, quelle, -minuten, text, bemerkung, vorgang, ausfuehrenderId),
            Buchung(mitarbeiterId, datum, Konto.Saldo, minuten, text, bemerkung, vorgang, ausfuehrenderId));
        await _db.SaveChangesAsync(ct);
        return Ergebnis.Ok($"{Zeitformat.Dauer(minuten)} Std. wurden vom Konto „{quelle.Text()}“ auf den Gleitzeitsaldo umgebucht.");
    }

    public async Task<Ergebnis> KorrigierenAsync(int mitarbeiterId, Konto konto, string? stunden, DateOnly datum, string? bemerkung, int ausfuehrenderId, CancellationToken ct = default)
    {
        if (!Zeitformat.TryParseDauer(stunden, out var minuten) || minuten == 0)
        {
            return Ergebnis.Fehler("Bitte einen Wert im Format Std:Min angeben, z. B. 2:30 oder -1:15.");
        }

        if (string.IsNullOrWhiteSpace(bemerkung))
        {
            return Ergebnis.Fehler("Bitte eine Begründung für die Korrektur angeben.");
        }

        _db.Kontobuchungen.Add(Buchung(mitarbeiterId, datum, konto, minuten, "Korrektur Personalabteilung", bemerkung, Guid.NewGuid(), ausfuehrenderId));
        await _db.SaveChangesAsync(ct);
        return Ergebnis.Ok($"Korrektur von {Zeitformat.Dauer(minuten)} Std. auf „{konto.Text()}“ gebucht.");
    }

    public async Task<Ergebnis> StornierenAsync(Guid vorgang, CancellationToken ct = default)
    {
        var buchungen = await _db.Kontobuchungen.Where(k => k.Vorgang == vorgang && !k.Storniert).ToListAsync(ct);
        if (buchungen.Count == 0)
        {
            return Ergebnis.Fehler("Die Buchung wurde nicht gefunden oder ist bereits storniert.");
        }

        foreach (var b in buchungen)
        {
            b.Storniert = true;
        }

        await _db.SaveChangesAsync(ct);
        return Ergebnis.Ok("Die Buchung wurde storniert.");
    }

    public Task<List<Kontobuchung>> ListeAsync(int mitarbeiterId, CancellationToken ct = default) =>
        _db.Kontobuchungen.AsNoTracking()
            .Where(k => k.MitarbeiterId == mitarbeiterId)
            .OrderByDescending(k => k.Datum).ThenByDescending(k => k.Id)
            .ToListAsync(ct);

    private Kontobuchung Buchung(int mitarbeiterId, DateOnly datum, Konto konto, int minuten, string text, string? bemerkung, Guid vorgang, int ausfuehrenderId) => new()
    {
        MitarbeiterId = mitarbeiterId,
        Datum = datum,
        Konto = konto,
        Minuten = minuten,
        Text = text,
        Bemerkung = string.IsNullOrWhiteSpace(bemerkung) ? null : bemerkung.Trim(),
        Vorgang = vorgang,
        ErstelltVonId = ausfuehrenderId,
        ErstelltAm = _uhr.Jetzt
    };
}
