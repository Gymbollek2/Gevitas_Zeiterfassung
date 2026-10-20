using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Options;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Gevitas.Zeiterfassung.Web.Data;

/// <summary>
/// Legt beim Start die SQLite-Datenbank an und befüllt Stammdaten
/// (Abwesenheitsarten aus der appsettings.json, Administrator, optional Demodaten).
/// </summary>
public sealed class DatenbankInitialisierung
{
    private readonly ZeiterfassungDbContext _db;
    private readonly ZeiterfassungOptions _optionen;
    private readonly SeedOptions _seed;
    private readonly PasswortService _passwoerter;
    private readonly IAppUhr _uhr;
    private readonly ILogger<DatenbankInitialisierung> _log;

    public DatenbankInitialisierung(
        ZeiterfassungDbContext db,
        IOptions<ZeiterfassungOptions> optionen,
        IOptions<SeedOptions> seed,
        PasswortService passwoerter,
        IAppUhr uhr,
        ILogger<DatenbankInitialisierung> log)
    {
        _db = db;
        _optionen = optionen.Value;
        _seed = seed.Value;
        _passwoerter = passwoerter;
        _uhr = uhr;
        _log = log;
    }

    public static void VerzeichnisSicherstellen(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (string.IsNullOrWhiteSpace(dataSource) || dataSource == ":memory:")
        {
            return;
        }

        var verzeichnis = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrEmpty(verzeichnis))
        {
            Directory.CreateDirectory(verzeichnis);
        }
    }

    public async Task AusfuehrenAsync(CancellationToken ct = default)
    {
        await _db.Database.EnsureCreatedAsync(ct);
        await AbwesenheitsartenAbgleichenAsync(ct);

        if (!_seed.Aktiv)
        {
            return;
        }

        await AdministratorAnlegenAsync(ct);
        if (_seed.Demodaten)
        {
            await DemodatenAnlegenAsync(ct);
        }
    }

    private async Task AbwesenheitsartenAbgleichenAsync(CancellationToken ct)
    {
        var vorhandene = await _db.Abwesenheitsarten.ToDictionaryAsync(a => a.Code, ct);
        var konfigurierte = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sortierung = 0;

        foreach (var eintrag in _optionen.Abwesenheitsarten)
        {
            if (string.IsNullOrWhiteSpace(eintrag.Code))
            {
                continue;
            }

            var code = eintrag.Code.Trim().ToUpperInvariant();
            konfigurierte.Add(code);
            if (!Enum.TryParse<AbwesenheitsWirkung>(eintrag.Wirkung, ignoreCase: true, out var wirkung))
            {
                _log.LogWarning("Unbekannte Wirkung '{Wirkung}' für Abwesenheitsart {Code} – verwende SollGutschrift.", eintrag.Wirkung, code);
                wirkung = AbwesenheitsWirkung.SollGutschrift;
            }

            if (!vorhandene.TryGetValue(code, out var art))
            {
                art = new Abwesenheitsart { Code = code };
                _db.Abwesenheitsarten.Add(art);
            }

            art.Name = eintrag.Name;
            art.Farbe = eintrag.Farbe;
            art.Wirkung = wirkung;
            art.ZaehltAlsUrlaub = eintrag.ZaehltAlsUrlaub;
            art.MaxKalendertage = eintrag.MaxKalendertage;
            art.NachweisErforderlich = eintrag.NachweisErforderlich;
            art.Sortierung = sortierung++;
            art.Aktiv = true;
        }

        // Nicht mehr konfigurierte Arten deaktivieren (nicht löschen – sie können noch verwendet werden)
        foreach (var art in vorhandene.Values.Where(a => !konfigurierte.Contains(a.Code)))
        {
            art.Aktiv = false;
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task AdministratorAnlegenAsync(CancellationToken ct)
    {
        if (await _db.Mitarbeiter.AnyAsync(ct))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_seed.AdminPasswort))
        {
            _log.LogWarning("Kein Seed:AdminPasswort konfiguriert – es wurde kein Administrator angelegt.");
            return;
        }

        var admin = new Mitarbeiter
        {
            Personalnummer = "0001",
            Benutzername = _seed.AdminBenutzername.Trim().ToLowerInvariant(),
            Vorname = "System",
            Nachname = "Administrator",
            Email = _optionen.Support.PersonalabteilungEmail,
            Rolle = Rolle.Administrator,
            SollMinutenProTag = _optionen.Sollarbeitszeit.StandardMinutenProTag,
            Jahresurlaub = _optionen.Urlaub.StandardJahresurlaub,
            Eintrittsdatum = new DateOnly(_uhr.Heute.Year, 1, 1),
            PasswortAendernErforderlich = true
        };
        admin.PasswortHash = _passwoerter.Hashen(admin, _seed.AdminPasswort);
        _db.Mitarbeiter.Add(admin);
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Administrator '{Benutzer}' wurde angelegt. Das Passwort muss bei der ersten Anmeldung geändert werden.", admin.Benutzername);
    }

    private async Task DemodatenAnlegenAsync(CancellationToken ct)
    {
        if (await _db.Mitarbeiter.AnyAsync(m => m.Benutzername == "fuehrung", ct))
        {
            return;
        }

        var admin = await _db.Mitarbeiter.OrderBy(m => m.Id).FirstAsync(ct);
        var eintritt = new DateOnly(_uhr.Heute.Year - 1, 1, 1);

        Mitarbeiter Neu(string nr, string benutzer, string vorname, string nachname, Rolle rolle, Mitarbeiter? chef)
        {
            var m = new Mitarbeiter
            {
                Personalnummer = nr,
                Benutzername = benutzer,
                Vorname = vorname,
                Nachname = nachname,
                Email = $"{benutzer}@gevitas.example",
                Abteilung = "Entwicklung",
                Rolle = rolle,
                Vorgesetzter = chef,
                SollMinutenProTag = _optionen.Sollarbeitszeit.StandardMinutenProTag,
                Jahresurlaub = _optionen.Urlaub.StandardJahresurlaub,
                Eintrittsdatum = eintritt
            };
            m.PasswortHash = _passwoerter.Hashen(m, "Demo1234!");
            return m;
        }

        var fuehrung = Neu("1001", "fuehrung", "Frieda", "Führung", Rolle.Fuehrungskraft, admin);
        var personal = Neu("1002", "personal", "Petra", "Personal", Rolle.Personal, admin);
        var mitarbeiter = Neu("1003", "mitarbeiter", "Mischa", "Mitarbeiter", Rolle.Mitarbeiter, fuehrung);
        var kollegin = Neu("1004", "kollegin", "Klara", "Kollegin", Rolle.Mitarbeiter, fuehrung);

        _db.Mitarbeiter.AddRange(fuehrung, personal, mitarbeiter, kollegin);
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Demodaten angelegt (Benutzer: fuehrung, personal, mitarbeiter, kollegin – Passwort: Demo1234!).");
    }
}
