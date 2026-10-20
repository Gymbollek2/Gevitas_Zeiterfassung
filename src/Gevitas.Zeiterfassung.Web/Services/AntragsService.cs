using Gevitas.Zeiterfassung.Web.Data;
using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Gevitas.Zeiterfassung.Web.Services;

public enum AntragsSicht
{
    Antragsteller,
    Genehmiger
}

public enum AntragsFilter
{
    Offen,
    Alle,
    Erledigt
}

/// <summary>
/// Antragsworkflow: Abwesenheiten, Löschung genehmigter Abwesenheiten, tagesbezogene Freigaben
/// (Arbeit &gt; 10 Std., vor/nach Rahmen) und Übertragung des Resturlaubs (zweistufig).
/// Genehmiger ist die disziplinarische Führungskraft; ist sie abwesend, die nächsthöhere Führungskraft.
/// </summary>
public sealed class AntragsService
{
    public const string LinkGenehmiger = "/Antraege?sicht=Genehmiger";
    public const string LinkEigene = "/Antraege?sicht=Antragsteller&filter=Alle";

    private readonly ZeiterfassungDbContext _db;
    private readonly IAppUhr _uhr;
    private readonly BenachrichtigungsService _benachrichtigungen;
    private readonly UrlaubService _urlaub;
    private readonly ZeiterfassungOptions _optionen;

    public AntragsService(
        ZeiterfassungDbContext db,
        IAppUhr uhr,
        BenachrichtigungsService benachrichtigungen,
        UrlaubService urlaub,
        IOptions<ZeiterfassungOptions> optionen)
    {
        _db = db;
        _uhr = uhr;
        _benachrichtigungen = benachrichtigungen;
        _urlaub = urlaub;
        _optionen = optionen.Value;
    }

    // ------------------------------------------------------------------
    // Genehmiger
    // ------------------------------------------------------------------

    /// <summary>
    /// Ermittelt den zuständigen Genehmiger. Null bedeutet: Personalabteilung.
    /// </summary>
    public async Task<int?> GenehmigerErmittelnAsync(Mitarbeiter antragsteller, CancellationToken ct = default)
    {
        if (antragsteller.AbweichenderGenehmigerId is int abweichend
            && await _db.Mitarbeiter.AnyAsync(m => m.Id == abweichend && m.Aktiv, ct))
        {
            return abweichend;
        }

        return await NaechsteAnwesendeFuehrungskraftAsync(antragsteller.VorgesetzterId, antragsteller.Id, ct);
    }

    private async Task<int?> NaechsteAnwesendeFuehrungskraftAsync(int? startId, int antragstellerId, CancellationToken ct)
    {
        var heute = _uhr.Heute;
        var besucht = new HashSet<int> { antragstellerId };
        var kandidat = startId;

        while (kandidat is int id && besucht.Add(id))
        {
            var fk = await _db.Mitarbeiter.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
            if (fk is null)
            {
                break;
            }

            var abwesend = await _db.Abwesenheiten.AnyAsync(a =>
                a.MitarbeiterId == fk.Id && a.Status == AbwesenheitsStatus.Genehmigt && a.Von <= heute && a.Bis >= heute, ct);
            if (fk.Aktiv && !abwesend)
            {
                return fk.Id;
            }

            kandidat = fk.VorgesetzterId;
        }

        return null;
    }

    // ------------------------------------------------------------------
    // Anträge stellen
    // ------------------------------------------------------------------

    public async Task<Ergebnis> AbwesenheitBeantragenAsync(int mitarbeiterId, DateOnly von, DateOnly bis, string artCode, string? bemerkung, CancellationToken ct = default)
    {
        if (bis < von)
        {
            return Ergebnis.Fehler("Das Ende darf nicht vor dem Beginn liegen.");
        }

        if (bis.DayNumber - von.DayNumber > 366)
        {
            return Ergebnis.Fehler("Eine Abwesenheit darf höchstens ein Jahr umfassen.");
        }

        var art = await _db.Abwesenheitsarten.FirstOrDefaultAsync(a => a.Code == artCode && a.Aktiv, ct);
        if (art is null)
        {
            return Ergebnis.Fehler("Bitte eine gültige Art der Abwesenheit wählen.");
        }

        var mitarbeiter = await _db.Mitarbeiter.FirstAsync(m => m.Id == mitarbeiterId, ct);
        var eigene = await _db.Abwesenheiten
            .Where(a => a.MitarbeiterId == mitarbeiterId
                        && (a.Status == AbwesenheitsStatus.Beantragt || a.Status == AbwesenheitsStatus.Genehmigt))
            .ToListAsync(ct);

        var ueberschneidung = eigene.FirstOrDefault(a => a.Von <= bis && a.Bis >= von);
        if (ueberschneidung is not null)
        {
            return Ergebnis.Fehler($"Im Zeitraum besteht bereits eine Abwesenheit ({Zeitformat.Datum(ueberschneidung.Von)} – {Zeitformat.Datum(ueberschneidung.Bis)}).");
        }

        if (art.MaxKalendertage is int max)
        {
            var (start, ende) = ZusammenhaengenderZeitraum(eigene.Where(a => a.ArtCode == art.Code).ToList(), von, bis);
            if (ende.DayNumber - start.DayNumber + 1 > max)
            {
                return Ergebnis.Fehler($"„{art.Name}“ ist für maximal {max} zusammenhängende Kalendertage möglich. " +
                                       "Längere Arbeitsunfähigkeiten müssen durch eine ärztliche Bescheinigung (eAU) nachgewiesen werden – bitte „Krank mit eAU“ wählen.");
            }
        }

        if (art.ZaehltAlsUrlaub)
        {
            if (_urlaub.UrlaubstageImZeitraum(mitarbeiter, von, bis).Count == 0)
            {
                return Ergebnis.Fehler("Der Zeitraum enthält keine Arbeitstage.");
            }

            var urlaubsFehler = await _urlaub.PruefenAsync(mitarbeiterId, von, bis, ct);
            if (urlaubsFehler is not null)
            {
                return Ergebnis.Fehler(urlaubsFehler);
            }
        }

        var abwesenheit = new Abwesenheit
        {
            MitarbeiterId = mitarbeiterId,
            ArtCode = art.Code,
            Von = von,
            Bis = bis,
            Status = AbwesenheitsStatus.Beantragt,
            Bemerkung = Bereinigen(bemerkung),
            ErstelltAm = _uhr.Jetzt
        };
        _db.Abwesenheiten.Add(abwesenheit);

        var antrag = await AntragAnlegenAsync(mitarbeiter, AntragsArt.Abwesenheit, von, bis, bemerkung, abwesenheit, stufen: 1, ct);
        await _db.SaveChangesAsync(ct);

        var hinweis = art.NachweisErforderlich ? " Bitte reichen Sie den Nachweis (Urkunde) bei der Personalabteilung ein." : string.Empty;
        return Ergebnis.Ok($"Antrag „{art.Name}“ ({Zeitformat.Datum(von)} – {Zeitformat.Datum(bis)}) wurde an {await GenehmigerNameAsync(antrag.GenehmigerId, ct)} gesendet.{hinweis}");
    }

    public async Task<Ergebnis> AbwesenheitLoeschenAsync(int mitarbeiterId, int abwesenheitId, string? bemerkung, CancellationToken ct = default)
    {
        var abwesenheit = await _db.Abwesenheiten.Include(a => a.Art)
            .FirstOrDefaultAsync(a => a.Id == abwesenheitId && a.MitarbeiterId == mitarbeiterId, ct);
        if (abwesenheit is null)
        {
            return Ergebnis.Fehler("Die Abwesenheit wurde nicht gefunden.");
        }

        if (abwesenheit.Status == AbwesenheitsStatus.Beantragt)
        {
            abwesenheit.Status = AbwesenheitsStatus.Zurueckgezogen;
            var offene = await _db.Antraege.Where(a => a.AbwesenheitId == abwesenheit.Id && a.Status == AntragsStatus.Offen).ToListAsync(ct);
            foreach (var antrag in offene)
            {
                antrag.Status = AntragsStatus.Zurueckgezogen;
                antrag.EntschiedenAm = _uhr.Jetzt;
            }

            await _db.SaveChangesAsync(ct);
            return Ergebnis.Ok("Der noch nicht genehmigte Antrag wurde gelöscht und wird der Führungskraft nicht mehr angezeigt.");
        }

        if (abwesenheit.Status != AbwesenheitsStatus.Genehmigt)
        {
            return Ergebnis.Fehler("Diese Abwesenheit kann nicht mehr gelöscht werden.");
        }

        if (await _db.Antraege.AnyAsync(a => a.AbwesenheitId == abwesenheit.Id && a.Art == AntragsArt.AbwesenheitLoeschen && a.Status == AntragsStatus.Offen, ct))
        {
            return Ergebnis.Fehler("Für diese Abwesenheit wurde bereits eine Löschung beantragt.");
        }

        var mitarbeiter = await _db.Mitarbeiter.FirstAsync(m => m.Id == mitarbeiterId, ct);
        var loeschantrag = await AntragAnlegenAsync(mitarbeiter, AntragsArt.AbwesenheitLoeschen, abwesenheit.Von, abwesenheit.Bis, bemerkung, abwesenheit, stufen: 1, ct);
        await _db.SaveChangesAsync(ct);
        return Ergebnis.Ok($"Die Löschung der genehmigten Abwesenheit wurde bei {await GenehmigerNameAsync(loeschantrag.GenehmigerId, ct)} beantragt.");
    }

    public async Task<Ergebnis> TagesfreigabeBeantragenAsync(int mitarbeiterId, DateOnly datum, bool ueber10, bool vorRahmen, bool nachRahmen, string? bemerkung, CancellationToken ct = default)
    {
        if (datum > _uhr.Heute)
        {
            return Ergebnis.Fehler("Freigaben können nur für bereits geleistete Arbeitstage beantragt werden.");
        }

        var berechtigung = await _db.Tagesberechtigungen.FirstOrDefaultAsync(t => t.MitarbeiterId == mitarbeiterId && t.Datum == datum, ct);
        if (berechtigung is null)
        {
            berechtigung = new Tagesberechtigung { MitarbeiterId = mitarbeiterId, Datum = datum };
            _db.Tagesberechtigungen.Add(berechtigung);
        }

        var mitarbeiter = await _db.Mitarbeiter.FirstAsync(m => m.Id == mitarbeiterId, ct);
        var arten = new List<AntragsArt>();
        if (ueber10 && !berechtigung.ArbeitUeber10Stunden)
        {
            berechtigung.ArbeitUeber10Stunden = true;
            arten.Add(AntragsArt.ArbeitUeber10Stunden);
        }

        if (vorRahmen && !berechtigung.ArbeitVorRahmen)
        {
            berechtigung.ArbeitVorRahmen = true;
            arten.Add(AntragsArt.ArbeitVorRahmen);
        }

        if (nachRahmen && !berechtigung.ArbeitNachRahmen)
        {
            berechtigung.ArbeitNachRahmen = true;
            arten.Add(AntragsArt.ArbeitNachRahmen);
        }

        if (arten.Count == 0)
        {
            return Ergebnis.Fehler("Bitte mindestens eine neue Freigabe auswählen.");
        }

        foreach (var art in arten)
        {
            await AntragAnlegenAsync(mitarbeiter, art, datum, datum, bemerkung, null, stufen: 1, ct);
        }

        await _db.SaveChangesAsync(ct);
        return Ergebnis.Ok("Antrag gestellt. Die Zeiten werden sofort gutgeschrieben und bei Ablehnung wieder abgezogen.");
    }

    public async Task<Ergebnis> UrlaubsuebertragBeantragenAsync(int mitarbeiterId, string? bemerkung, CancellationToken ct = default)
    {
        var heute = _uhr.Heute;
        var von = UrlaubOptions.Datum(heute.Year, _optionen.Urlaub.UebertragAntragVon);
        var bis = UrlaubOptions.Datum(heute.Year, _optionen.Urlaub.UebertragAntragBis);
        if (heute < von || heute > bis)
        {
            return Ergebnis.Fehler($"Die Übertragung des Resturlaubs kann nur vom {Zeitformat.Datum(von)} bis {Zeitformat.Datum(bis)} beantragt werden. " +
                                   $"Danach bitte eine E-Mail an {_optionen.Support.PersonalabteilungEmail} schreiben.");
        }

        var jahresbeginn = new DateOnly(heute.Year, 1, 1);
        if (await _db.Antraege.AnyAsync(a => a.AntragstellerId == mitarbeiterId && a.Art == AntragsArt.UrlaubsUebertrag && a.Von == jahresbeginn
                                              && (a.Status == AntragsStatus.Offen || a.Status == AntragsStatus.Genehmigt), ct))
        {
            return Ergebnis.Fehler("Für dieses Jahr wurde die Übertragung bereits beantragt.");
        }

        var mitarbeiter = await _db.Mitarbeiter.FirstAsync(m => m.Id == mitarbeiterId, ct);
        var verfall = UrlaubOptions.Datum(heute.Year, _optionen.Urlaub.VerlaengerterVerfall);
        await AntragAnlegenAsync(mitarbeiter, AntragsArt.UrlaubsUebertrag, jahresbeginn, verfall, bemerkung, null, stufen: 2, ct);
        await _db.SaveChangesAsync(ct);
        return Ergebnis.Ok("Der zweistufige Genehmigungsprozess (Führungskraft und Bereichsleitung) wurde gestartet. Sie werden per Benachrichtigung informiert.");
    }

    public async Task<Ergebnis> ZurueckziehenAsync(int mitarbeiterId, int antragId, CancellationToken ct = default)
    {
        var antrag = await _db.Antraege.Include(a => a.Abwesenheit)
            .FirstOrDefaultAsync(a => a.Id == antragId && a.AntragstellerId == mitarbeiterId, ct);
        if (antrag is null || antrag.Status != AntragsStatus.Offen)
        {
            return Ergebnis.Fehler("Nur eigene, noch offene Anträge können gelöscht werden.");
        }

        switch (antrag.Art)
        {
            case AntragsArt.Abwesenheit when antrag.Abwesenheit is not null:
                antrag.Abwesenheit.Status = AbwesenheitsStatus.Zurueckgezogen;
                break;
            case AntragsArt.ArbeitUeber10Stunden:
            case AntragsArt.ArbeitVorRahmen:
            case AntragsArt.ArbeitNachRahmen:
                await FreigabeEntziehenAsync(antrag, ct);
                break;
        }

        antrag.Status = AntragsStatus.Zurueckgezogen;
        antrag.EntschiedenAm = _uhr.Jetzt;
        await _db.SaveChangesAsync(ct);
        return Ergebnis.Ok("Der Antrag wurde gelöscht. Er wird der Führungskraft nicht mehr angezeigt.");
    }

    // ------------------------------------------------------------------
    // Genehmigen / Ablehnen
    // ------------------------------------------------------------------

    public async Task<Ergebnis> EntscheidenAsync(int benutzerId, Rolle rolle, int antragId, bool genehmigen, string? bemerkung, CancellationToken ct = default)
    {
        var antrag = await _db.Antraege
            .Include(a => a.Abwesenheit).ThenInclude(a => a!.Art)
            .Include(a => a.Antragsteller)
            .FirstOrDefaultAsync(a => a.Id == antragId, ct);
        if (antrag is null)
        {
            return Ergebnis.Fehler("Der Antrag wurde nicht gefunden.");
        }

        if (antrag.Status != AntragsStatus.Offen)
        {
            return Ergebnis.Fehler("Der Antrag wurde bereits bearbeitet.");
        }

        if (!DarfEntscheiden(antrag, benutzerId, rolle))
        {
            return Ergebnis.Fehler("Sie sind für diesen Antrag nicht als Genehmiger hinterlegt.");
        }

        if (!genehmigen && string.IsNullOrWhiteSpace(bemerkung))
        {
            return Ergebnis.Fehler("Bei einer Ablehnung muss eine Begründung angegeben werden.");
        }

        var beschreibung = Beschreibung(antrag);

        if (genehmigen && antrag.Stufe < antrag.Stufen)
        {
            antrag.Stufe++;
            antrag.GenehmigerId = await NaechsteAnwesendeFuehrungskraftAsync(
                await _db.Mitarbeiter.Where(m => m.Id == benutzerId).Select(m => m.VorgesetzterId).FirstOrDefaultAsync(ct),
                antrag.AntragstellerId, ct);
            antrag.Entscheidungsbemerkung = Bereinigen(bemerkung);
            await BenachrichtigenAsync(antrag.GenehmigerId, "Neuer Antrag zur Genehmigung",
                $"{antrag.Antragsteller!.VollerName}: {beschreibung} (Stufe {antrag.Stufe} von {antrag.Stufen})", LinkGenehmiger, ct);
            await _db.SaveChangesAsync(ct);
            return Ergebnis.Ok($"Stufe {antrag.Stufe - 1} genehmigt – der Antrag wurde an {await GenehmigerNameAsync(antrag.GenehmigerId, ct)} weitergeleitet.");
        }

        switch (antrag.Art)
        {
            case AntragsArt.Abwesenheit when antrag.Abwesenheit is not null:
                antrag.Abwesenheit.Status = genehmigen ? AbwesenheitsStatus.Genehmigt : AbwesenheitsStatus.Abgelehnt;
                break;
            case AntragsArt.AbwesenheitLoeschen when antrag.Abwesenheit is not null && genehmigen:
                antrag.Abwesenheit.Status = AbwesenheitsStatus.Storniert;
                break;
            case AntragsArt.ArbeitUeber10Stunden:
            case AntragsArt.ArbeitVorRahmen:
            case AntragsArt.ArbeitNachRahmen:
                if (!genehmigen)
                {
                    await FreigabeEntziehenAsync(antrag, ct);
                }

                break;
        }

        antrag.Status = genehmigen ? AntragsStatus.Genehmigt : AntragsStatus.Abgelehnt;
        antrag.Entscheidungsbemerkung = Bereinigen(bemerkung);
        antrag.EntschiedenVonId = benutzerId;
        antrag.EntschiedenAm = _uhr.Jetzt;

        var ergebnisText = genehmigen ? "genehmigt" : "abgelehnt";
        await BenachrichtigenAsync(antrag.AntragstellerId, $"Antrag {ergebnisText}",
            $"Ihr Antrag „{beschreibung}“ wurde {ergebnisText}." + (string.IsNullOrWhiteSpace(bemerkung) ? string.Empty : $" Bemerkung: {bemerkung.Trim()}"),
            LinkEigene, ct);
        await _db.SaveChangesAsync(ct);
        return Ergebnis.Ok($"Antrag von {antrag.Antragsteller!.VollerName} {ergebnisText}.");
    }

    public static bool DarfEntscheiden(Antrag antrag, int benutzerId, Rolle rolle) =>
        antrag.AntragstellerId != benutzerId
        && (antrag.GenehmigerId == benutzerId
            || (antrag.GenehmigerId is null && rolle >= Rolle.Personal)
            || rolle == Rolle.Administrator);

    // ------------------------------------------------------------------
    // Abfragen
    // ------------------------------------------------------------------

    public async Task<List<Antrag>> ListeAsync(int benutzerId, Rolle rolle, AntragsSicht sicht, AntragsFilter filter, DateOnly ab, CancellationToken ct = default)
    {
        var abZeitpunkt = ab.ToDateTime(TimeOnly.MinValue);
        var abfrage = _db.Antraege.AsNoTracking()
            .Include(a => a.Antragsteller)
            .Include(a => a.Genehmiger)
            .Include(a => a.EntschiedenVon)
            .Include(a => a.Abwesenheit).ThenInclude(a => a!.Art)
            .Where(a => a.ErstelltAm >= abZeitpunkt);

        abfrage = sicht == AntragsSicht.Antragsteller
            ? abfrage.Where(a => a.AntragstellerId == benutzerId)
            : abfrage.Where(a => a.AntragstellerId != benutzerId
                                 && (a.GenehmigerId == benutzerId
                                     || a.EntschiedenVonId == benutzerId
                                     || (a.GenehmigerId == null && rolle >= Rolle.Personal)));

        abfrage = filter switch
        {
            AntragsFilter.Offen => abfrage.Where(a => a.Status == AntragsStatus.Offen),
            AntragsFilter.Erledigt => abfrage.Where(a => a.Status != AntragsStatus.Offen),
            _ => abfrage
        };

        return await abfrage.OrderByDescending(a => a.ErstelltAm).ThenByDescending(a => a.Id).ToListAsync(ct);
    }

    public Task<bool> UrlaubsuebertragBeantragtAsync(int mitarbeiterId, int jahr, CancellationToken ct = default)
    {
        var jahresbeginn = new DateOnly(jahr, 1, 1);
        return _db.Antraege.AnyAsync(a => a.AntragstellerId == mitarbeiterId && a.Art == AntragsArt.UrlaubsUebertrag && a.Von == jahresbeginn
                                          && (a.Status == AntragsStatus.Offen || a.Status == AntragsStatus.Genehmigt), ct);
    }

    public Task<int> OffeneZurGenehmigungAsync(int benutzerId, Rolle rolle, CancellationToken ct = default) =>
        _db.Antraege.CountAsync(a => a.Status == AntragsStatus.Offen
                                     && a.AntragstellerId != benutzerId
                                     && (a.GenehmigerId == benutzerId || (a.GenehmigerId == null && rolle >= Rolle.Personal)), ct);

    public static string Beschreibung(Antrag antrag) => antrag.Art switch
    {
        AntragsArt.Abwesenheit or AntragsArt.AbwesenheitLoeschen =>
            $"{antrag.Art.Text()}: {antrag.Abwesenheit?.Art?.Name ?? antrag.Abwesenheit?.ArtCode} {Zeitformat.Datum(antrag.Von)} – {Zeitformat.Datum(antrag.Bis)}",
        AntragsArt.UrlaubsUebertrag => $"{antrag.Art.Text()} {antrag.Von.Year} bis {Zeitformat.Datum(antrag.Bis)}",
        _ => $"{antrag.Art.Text()} am {Zeitformat.Datum(antrag.Von)}"
    };

    // ------------------------------------------------------------------
    // Hilfsmethoden
    // ------------------------------------------------------------------

    private async Task<Antrag> AntragAnlegenAsync(Mitarbeiter antragsteller, AntragsArt art, DateOnly von, DateOnly bis, string? bemerkung,
        Abwesenheit? abwesenheit, int stufen, CancellationToken ct)
    {
        var antrag = new Antrag
        {
            Antragsteller = antragsteller,
            AntragstellerId = antragsteller.Id,
            Art = art,
            Von = von,
            Bis = bis,
            Abwesenheit = abwesenheit,
            Bemerkung = Bereinigen(bemerkung),
            GenehmigerId = await GenehmigerErmittelnAsync(antragsteller, ct),
            Stufe = 1,
            Stufen = stufen,
            ErstelltAm = _uhr.Jetzt
        };
        _db.Antraege.Add(antrag);

        await BenachrichtigenAsync(antrag.GenehmigerId, "Neuer Antrag zur Genehmigung",
            $"{antragsteller.VollerName}: {Beschreibung(antrag)}" + (antrag.Bemerkung is null ? string.Empty : $" – {antrag.Bemerkung}"),
            LinkGenehmiger, ct);
        return antrag;
    }

    /// <summary>Benachrichtigt den Genehmiger bzw. – wenn keiner hinterlegt ist – alle Mitarbeitenden der Personalabteilung.</summary>
    private async Task BenachrichtigenAsync(int? empfaengerId, string titel, string text, string link, CancellationToken ct)
    {
        if (empfaengerId is int id)
        {
            await _benachrichtigungen.BenachrichtigenAsync(id, titel, text, link, ct);
            return;
        }

        var personal = await _db.Mitarbeiter.Where(m => m.Aktiv && m.Rolle >= Rolle.Personal).Select(m => m.Id).ToListAsync(ct);
        foreach (var p in personal)
        {
            await _benachrichtigungen.BenachrichtigenAsync(p, titel, text, link, ct);
        }
    }

    private async Task FreigabeEntziehenAsync(Antrag antrag, CancellationToken ct)
    {
        var berechtigung = await _db.Tagesberechtigungen.FirstOrDefaultAsync(t => t.MitarbeiterId == antrag.AntragstellerId && t.Datum == antrag.Von, ct);
        if (berechtigung is null)
        {
            return;
        }

        switch (antrag.Art)
        {
            case AntragsArt.ArbeitUeber10Stunden:
                berechtigung.ArbeitUeber10Stunden = false;
                break;
            case AntragsArt.ArbeitVorRahmen:
                berechtigung.ArbeitVorRahmen = false;
                break;
            case AntragsArt.ArbeitNachRahmen:
                berechtigung.ArbeitNachRahmen = false;
                break;
        }
    }

    private async Task<string> GenehmigerNameAsync(int? genehmigerId, CancellationToken ct)
    {
        if (genehmigerId is null)
        {
            return "die Personalabteilung";
        }

        var m = await _db.Mitarbeiter.AsNoTracking().FirstOrDefaultAsync(x => x.Id == genehmigerId, ct);
        return m?.VollerName ?? "die Personalabteilung";
    }

    /// <summary>Erweitert den Zeitraum um direkt angrenzende Abwesenheiten derselben Art.</summary>
    internal static (DateOnly Start, DateOnly Ende) ZusammenhaengenderZeitraum(List<Abwesenheit> gleicheArt, DateOnly von, DateOnly bis)
    {
        var start = von;
        var ende = bis;
        bool geaendert;
        do
        {
            geaendert = false;
            foreach (var a in gleicheArt)
            {
                if (a.Bis == start.AddDays(-1) || (a.Von < start && a.Bis >= start.AddDays(-1)))
                {
                    if (a.Von < start)
                    {
                        start = a.Von;
                        geaendert = true;
                    }
                }

                if (a.Von == ende.AddDays(1) || (a.Bis > ende && a.Von <= ende.AddDays(1)))
                {
                    if (a.Bis > ende)
                    {
                        ende = a.Bis;
                        geaendert = true;
                    }
                }
            }
        }
        while (geaendert);

        return (start, ende);
    }

    private static string? Bereinigen(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
