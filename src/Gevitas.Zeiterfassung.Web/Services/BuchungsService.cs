using Gevitas.Zeiterfassung.Web.Data;
using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Gevitas.Zeiterfassung.Web.Services;

public enum TerminalAktion
{
    KommenOderGehen,
    Kommen,
    Gehen,
    Aendern,
    KrankUntertaegig,
    WegezeitKommenOderGehen,
    WegezeitKommen,
    WegezeitGehen
}

public sealed class BuchungEingabe
{
    public int? Id { get; set; }
    public DateOnly Datum { get; set; }
    public TimeOnly? Beginn { get; set; }
    public TimeOnly? Ende { get; set; }
    public BuchungsArt Art { get; set; } = BuchungsArt.Arbeit;
    public string? Bemerkung { get; set; }
}

/// <summary>Erfassen, Korrigieren und Löschen von Zeitbuchungen inklusive Protokollierung.</summary>
public sealed class BuchungsService
{
    public const string Kommen = "P10";
    public const string Wechsel = "P11";
    public const string Gehen = "P20";

    private readonly ZeiterfassungDbContext _db;
    private readonly IAppUhr _uhr;
    private readonly ZeiterfassungOptions _optionen;

    public BuchungsService(ZeiterfassungDbContext db, IAppUhr uhr, IOptions<ZeiterfassungOptions> optionen)
    {
        _db = db;
        _uhr = uhr;
        _optionen = optionen.Value;
    }

    // ------------------------------------------------------------------
    // Terminal (Kommen/Gehen mit aktueller Uhrzeit)
    // ------------------------------------------------------------------

    public async Task<Zeitbuchung?> OffeneBuchungAsync(int mitarbeiterId, CancellationToken ct = default)
    {
        var heute = _uhr.Heute;
        return await _db.Zeitbuchungen
            .Where(b => b.MitarbeiterId == mitarbeiterId && b.Datum == heute && b.Ende == null && b.Art != BuchungsArt.KrankUntertaegig)
            .OrderByDescending(b => b.Beginn)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Ergebnis> TerminalAsync(int mitarbeiterId, TerminalAktion aktion, CancellationToken ct = default)
    {
        var heute = _uhr.Heute;
        var jetzt = _uhr.UhrzeitMinute;
        var offen = await OffeneBuchungAsync(mitarbeiterId, ct);
        var name = await NameAsync(mitarbeiterId, ct);

        switch (aktion)
        {
            case TerminalAktion.KommenOderGehen:
                return await TerminalAsync(mitarbeiterId, offen is null ? TerminalAktion.Kommen : TerminalAktion.Gehen, ct);

            case TerminalAktion.Kommen:
                if (offen is not null)
                {
                    return Ergebnis.Fehler($"Sie sind bereits seit {Zeitformat.Uhrzeit(offen.Beginn)} Uhr angemeldet.");
                }

                if (await KrankGemeldetAsync(mitarbeiterId, heute, ct))
                {
                    return Ergebnis.Fehler("Für heute ist bereits „Krank untertägig“ erfasst.");
                }

                Neu(mitarbeiterId, heute, jetzt, BuchungsArt.Arbeit, BuchungsQuelle.Terminal);
                Protokollieren(mitarbeiterId, heute, $"Hinzugefügt: {Zeitformat.Uhrzeit(jetzt)}, {Kommen}, Kommen", null, mitarbeiterId, name);
                await _db.SaveChangesAsync(ct);
                return Ergebnis.Ok($"Kommen um {Zeitformat.Uhrzeit(jetzt)} Uhr gebucht.");

            case TerminalAktion.Gehen:
                if (offen is null)
                {
                    return Ergebnis.Fehler("Es gibt keine offene Kommen-Buchung.");
                }

                Schliessen(offen, jetzt);
                Protokollieren(mitarbeiterId, heute, $"Hinzugefügt: {Zeitformat.Uhrzeit(jetzt)}, {Gehen}, Gehen{ArtZusatz(offen.Art)}", null, mitarbeiterId, name);
                await _db.SaveChangesAsync(ct);
                return Ergebnis.Ok($"Gehen um {Zeitformat.Uhrzeit(jetzt)} Uhr gebucht.");

            case TerminalAktion.Aendern:
                if (offen is null)
                {
                    return Ergebnis.Fehler("Eine Wechselbuchung ist nur bei offener Kommen-Buchung möglich.");
                }

                Schliessen(offen, jetzt);
                Neu(mitarbeiterId, heute, jetzt, BuchungsArt.Arbeit, BuchungsQuelle.Terminal);
                Protokollieren(mitarbeiterId, heute, $"Hinzugefügt: {Zeitformat.Uhrzeit(jetzt)}, {Wechsel}, Wechselbuchung", null, mitarbeiterId, name);
                await _db.SaveChangesAsync(ct);
                return Ergebnis.Ok($"Wechselbuchung um {Zeitformat.Uhrzeit(jetzt)} Uhr gebucht.");

            case TerminalAktion.KrankUntertaegig:
                var hatArbeit = await _db.Zeitbuchungen.AnyAsync(b => b.MitarbeiterId == mitarbeiterId && b.Datum == heute && b.Art == BuchungsArt.Arbeit, ct);
                if (!hatArbeit)
                {
                    return Ergebnis.Fehler("Bitte zuerst den Arbeitsbeginn erfassen. Für ganztägige Erkrankungen bitte eine Abwesenheit beantragen.");
                }

                if (await KrankGemeldetAsync(mitarbeiterId, heute, ct))
                {
                    return Ergebnis.Fehler("„Krank untertägig“ ist für heute bereits erfasst.");
                }

                if (offen is not null)
                {
                    Schliessen(offen, jetzt);
                }

                _db.Zeitbuchungen.Add(new Zeitbuchung
                {
                    MitarbeiterId = mitarbeiterId,
                    Datum = heute,
                    Beginn = jetzt,
                    Ende = null,
                    Art = BuchungsArt.KrankUntertaegig,
                    Quelle = BuchungsQuelle.Terminal,
                    ErstelltAm = _uhr.Jetzt,
                    GeaendertAm = _uhr.Jetzt
                });
                Protokollieren(mitarbeiterId, heute, $"Hinzugefügt: {Zeitformat.Uhrzeit(jetzt)}, {Gehen}, Krank untertägig", null, mitarbeiterId, name);
                await _db.SaveChangesAsync(ct);
                return Ergebnis.Ok("„Krank untertägig“ wurde gebucht. Die restliche Sollarbeitszeit wird automatisch aufgefüllt. Gute Besserung!");

            case TerminalAktion.WegezeitKommenOderGehen:
                return await TerminalAsync(mitarbeiterId,
                    offen?.Art == BuchungsArt.Wegezeit ? TerminalAktion.WegezeitGehen : TerminalAktion.WegezeitKommen, ct);

            case TerminalAktion.WegezeitKommen:
                if (offen?.Art == BuchungsArt.Wegezeit)
                {
                    return Ergebnis.Fehler($"Wegezeit läuft bereits seit {Zeitformat.Uhrzeit(offen.Beginn)} Uhr.");
                }

                if (offen is not null)
                {
                    Schliessen(offen, jetzt);
                }

                Neu(mitarbeiterId, heute, jetzt, BuchungsArt.Wegezeit, BuchungsQuelle.Terminal);
                Protokollieren(mitarbeiterId, heute, $"Hinzugefügt: {Zeitformat.Uhrzeit(jetzt)}, {Kommen}, Kommen, Wegezeit", null, mitarbeiterId, name);
                await _db.SaveChangesAsync(ct);
                return Ergebnis.Ok($"Wegezeit ab {Zeitformat.Uhrzeit(jetzt)} Uhr gebucht.");

            case TerminalAktion.WegezeitGehen:
                if (offen?.Art != BuchungsArt.Wegezeit)
                {
                    return Ergebnis.Fehler("Es läuft keine Wegezeit.");
                }

                Schliessen(offen, jetzt);
                Protokollieren(mitarbeiterId, heute, $"Hinzugefügt: {Zeitformat.Uhrzeit(jetzt)}, {Gehen}, Gehen, Wegezeit", null, mitarbeiterId, name);
                await _db.SaveChangesAsync(ct);
                return Ergebnis.Ok($"Wegezeit bis {Zeitformat.Uhrzeit(jetzt)} Uhr gebucht.");

            default:
                return Ergebnis.Fehler("Unbekannte Aktion.");
        }
    }

    // ------------------------------------------------------------------
    // Zeitkorrekturen (manuelle Erfassung)
    // ------------------------------------------------------------------

    public async Task<Ergebnis> SpeichernAsync(int mitarbeiterId, BuchungEingabe e, int ausfuehrenderId, bool ohneZeitgrenze, CancellationToken ct = default)
    {
        var pruefung = Pruefen(e, ohneZeitgrenze);
        if (pruefung is not null)
        {
            return Ergebnis.Fehler(pruefung);
        }

        var vorhandene = await _db.Zeitbuchungen
            .Where(b => b.MitarbeiterId == mitarbeiterId && b.Datum == e.Datum && b.Id != e.Id)
            .ToListAsync(ct);

        var ueberschneidung = Ueberschneidung(e, vorhandene);
        if (ueberschneidung is not null)
        {
            return Ergebnis.Fehler(ueberschneidung);
        }

        var ausfuehrender = await NameAsync(ausfuehrenderId, ct);
        Zeitbuchung buchung;
        string details;

        if (e.Id is null)
        {
            buchung = new Zeitbuchung
            {
                MitarbeiterId = mitarbeiterId,
                Datum = e.Datum,
                Quelle = BuchungsQuelle.Manuell,
                ErstelltAm = _uhr.Jetzt
            };
            _db.Zeitbuchungen.Add(buchung);
            details = $"Hinzugefügt: {Bereich(e.Beginn!.Value, e.Ende)}{ArtZusatz(e.Art)}";
        }
        else
        {
            buchung = await _db.Zeitbuchungen.FirstOrDefaultAsync(b => b.Id == e.Id && b.MitarbeiterId == mitarbeiterId, ct)
                      ?? throw new InvalidOperationException("Buchung nicht gefunden.");
            if (buchung.Datum != e.Datum)
            {
                return Ergebnis.Fehler("Das Datum einer Buchung kann nicht geändert werden.");
            }

            if (buchung.Beginn == e.Beginn && buchung.Ende == e.Ende && buchung.Art == e.Art)
            {
                return Ergebnis.Ok("Keine Änderung.");
            }

            details = $"Geändert: {Bereich(buchung.Beginn, buchung.Ende)}{ArtZusatz(buchung.Art)} → {Bereich(e.Beginn!.Value, e.Ende)}{ArtZusatz(e.Art)}";
        }

        buchung.Beginn = e.Beginn!.Value;
        buchung.Ende = e.Ende;
        buchung.Art = e.Art;
        buchung.GeaendertAm = _uhr.Jetzt;

        Protokollieren(mitarbeiterId, e.Datum, details, e.Bemerkung, ausfuehrenderId, ausfuehrender);
        await _db.SaveChangesAsync(ct);
        return Ergebnis.Ok($"Zeitkorrektur für {Zeitformat.Datum(e.Datum)} gespeichert.");
    }

    public async Task<Ergebnis> LoeschenAsync(int mitarbeiterId, int buchungId, string? bemerkung, int ausfuehrenderId, bool ohneZeitgrenze, CancellationToken ct = default)
    {
        var buchung = await _db.Zeitbuchungen.FirstOrDefaultAsync(b => b.Id == buchungId && b.MitarbeiterId == mitarbeiterId, ct);
        if (buchung is null)
        {
            return Ergebnis.Fehler("Die Buchung wurde nicht gefunden.");
        }

        if (!ohneZeitgrenze && buchung.Datum < _uhr.Heute.AddDays(-_optionen.MaxRueckwirkendeTage))
        {
            return Ergebnis.Fehler($"Buchungen älter als {_optionen.MaxRueckwirkendeTage} Tage können nur von der Personalabteilung geändert werden.");
        }

        _db.Zeitbuchungen.Remove(buchung);
        Protokollieren(mitarbeiterId, buchung.Datum, $"Gelöscht: {Bereich(buchung.Beginn, buchung.Ende)}{ArtZusatz(buchung.Art)}", bemerkung,
            ausfuehrenderId, await NameAsync(ausfuehrenderId, ct));
        await _db.SaveChangesAsync(ct);
        return Ergebnis.Ok($"Buchung vom {Zeitformat.Datum(buchung.Datum)} gelöscht.");
    }

    public async Task<Ergebnis> BemerkungSpeichernAsync(int mitarbeiterId, DateOnly datum, string? text, CancellationToken ct = default)
    {
        var vorhandene = await _db.Tagesbemerkungen.FirstOrDefaultAsync(t => t.MitarbeiterId == mitarbeiterId && t.Datum == datum, ct);
        if (string.IsNullOrWhiteSpace(text))
        {
            if (vorhandene is not null)
            {
                _db.Tagesbemerkungen.Remove(vorhandene);
            }
        }
        else
        {
            if (vorhandene is null)
            {
                vorhandene = new Tagesbemerkung { MitarbeiterId = mitarbeiterId, Datum = datum };
                _db.Tagesbemerkungen.Add(vorhandene);
            }

            vorhandene.Text = text.Trim();
            vorhandene.GeaendertAm = _uhr.Jetzt;
        }

        await _db.SaveChangesAsync(ct);
        return Ergebnis.Ok($"Tagesbezogene Bemerkung für {Zeitformat.Datum(datum)} gespeichert.");
    }

    public Task<List<Buchungsprotokoll>> VerlaufAsync(int mitarbeiterId, DateOnly datum, CancellationToken ct = default) =>
        _db.Buchungsprotokolle.AsNoTracking()
            .Where(p => p.MitarbeiterId == mitarbeiterId && p.Datum == datum)
            .OrderBy(p => p.Zeitpunkt).ThenBy(p => p.Id)
            .ToListAsync(ct);

    // ------------------------------------------------------------------
    // Validierung
    // ------------------------------------------------------------------

    /// <summary>Prüft eine Buchung fachlich. Liefert null, wenn alles gültig ist.</summary>
    internal string? Pruefen(BuchungEingabe e, bool ohneZeitgrenze)
    {
        var heute = _uhr.Heute;
        var jetzt = _uhr.UhrzeitMinute;

        if (e.Beginn is null)
        {
            return "Bitte einen Beginn eintragen.";
        }

        if (e.Datum > heute)
        {
            return "Buchungen in der Zukunft können nicht vorgenommen werden.";
        }

        if (!ohneZeitgrenze && e.Datum < heute.AddDays(-_optionen.MaxRueckwirkendeTage))
        {
            return $"Zeiten können maximal für {_optionen.MaxRueckwirkendeTage} Tage in die Vergangenheit erfasst werden.";
        }

        if (e.Datum == heute && (e.Beginn > jetzt || e.Ende > jetzt))
        {
            return "Buchungen in der Zukunft können nicht vorgenommen werden. Das Arbeitsende kann erst nach Arbeitsende eingetragen werden.";
        }

        if (e.Art == BuchungsArt.KrankUntertaegig)
        {
            return e.Ende is null
                ? null
                : "Bei „Krank untertägig“ darf keine Ende-Buchung eingetragen werden – sie wird automatisch ergänzt.";
        }

        if (e.Ende is null && e.Datum != heute)
        {
            return "Bitte ein Ende eintragen.";
        }

        if (e.Ende is not null && e.Ende <= e.Beginn)
        {
            return "Das Ende muss nach dem Beginn liegen.";
        }

        return null;
    }

    private static string? Ueberschneidung(BuchungEingabe e, List<Zeitbuchung> vorhandene)
    {
        if (e.Art == BuchungsArt.KrankUntertaegig)
        {
            return vorhandene.Any(b => b.Art == BuchungsArt.KrankUntertaegig)
                ? "„Krank untertägig“ ist für diesen Tag bereits erfasst."
                : null;
        }

        if (e.Ende is null && vorhandene.Any(b => b.Ende is null && b.Art != BuchungsArt.KrankUntertaegig))
        {
            return "Es kann nur eine offene Buchung (ohne Ende) geben.";
        }

        var neuBeginn = Zeitformat.MinutenSeitMitternacht(e.Beginn!.Value);
        var neuEnde = e.Ende is null ? 24 * 60 : Zeitformat.MinutenSeitMitternacht(e.Ende.Value);
        foreach (var b in vorhandene.Where(b => b.Art != BuchungsArt.KrankUntertaegig))
        {
            var beginn = Zeitformat.MinutenSeitMitternacht(b.Beginn);
            var ende = b.Ende is null ? 24 * 60 : Zeitformat.MinutenSeitMitternacht(b.Ende.Value);
            if (neuBeginn < ende && beginn < neuEnde)
            {
                return $"Die Buchung überschneidet sich mit {Bereich(b.Beginn, b.Ende)}.";
            }
        }

        return null;
    }

    // ------------------------------------------------------------------
    // Hilfsmethoden
    // ------------------------------------------------------------------

    private void Neu(int mitarbeiterId, DateOnly datum, TimeOnly beginn, BuchungsArt art, BuchungsQuelle quelle)
    {
        _db.Zeitbuchungen.Add(new Zeitbuchung
        {
            MitarbeiterId = mitarbeiterId,
            Datum = datum,
            Beginn = beginn,
            Art = art,
            Quelle = quelle,
            ErstelltAm = _uhr.Jetzt,
            GeaendertAm = _uhr.Jetzt
        });
    }

    private void Schliessen(Zeitbuchung buchung, TimeOnly ende)
    {
        if (ende <= buchung.Beginn)
        {
            // Kommen und Gehen in derselben Minute – leere Buchung entfernen
            _db.Zeitbuchungen.Remove(buchung);
            return;
        }

        buchung.Ende = ende;
        buchung.GeaendertAm = _uhr.Jetzt;
    }

    private Task<bool> KrankGemeldetAsync(int mitarbeiterId, DateOnly datum, CancellationToken ct) =>
        _db.Zeitbuchungen.AnyAsync(b => b.MitarbeiterId == mitarbeiterId && b.Datum == datum && b.Art == BuchungsArt.KrankUntertaegig, ct);

    private void Protokollieren(int mitarbeiterId, DateOnly datum, string details, string? bemerkung, int ausfuehrenderId, string ausfuehrenderName)
    {
        _db.Buchungsprotokolle.Add(new Buchungsprotokoll
        {
            MitarbeiterId = mitarbeiterId,
            Datum = datum,
            Typ = "Zeitereignis",
            Details = details,
            Bemerkung = string.IsNullOrWhiteSpace(bemerkung) ? null : bemerkung.Trim(),
            Zeitpunkt = _uhr.Jetzt,
            AusgefuehrtVonId = ausfuehrenderId,
            AusgefuehrtVonName = ausfuehrenderName
        });
    }

    private async Task<string> NameAsync(int mitarbeiterId, CancellationToken ct)
    {
        var m = await _db.Mitarbeiter.AsNoTracking().FirstOrDefaultAsync(x => x.Id == mitarbeiterId, ct);
        return m?.AnzeigeName ?? $"#{mitarbeiterId}";
    }

    private static string Bereich(TimeOnly beginn, TimeOnly? ende) =>
        ende is null ? $"{Zeitformat.Uhrzeit(beginn)} – offen" : $"{Zeitformat.Uhrzeit(beginn)} – {Zeitformat.Uhrzeit(ende)}";

    private static string ArtZusatz(BuchungsArt art) => art == BuchungsArt.Arbeit ? string.Empty : $", {art.Text()}";
}
