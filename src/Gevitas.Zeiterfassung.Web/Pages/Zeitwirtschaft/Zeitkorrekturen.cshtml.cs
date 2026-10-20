using System.Globalization;
using Gevitas.Zeiterfassung.Web.Domain;
using Gevitas.Zeiterfassung.Web.Infrastructure;
using Gevitas.Zeiterfassung.Web.Options;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using static Gevitas.Zeiterfassung.Web.Services.Texte;

namespace Gevitas.Zeiterfassung.Web.Pages.Zeitwirtschaft;

public class ZeitkorrekturenModel : AppSeite
{
    private readonly KontenService _konten;
    private readonly BuchungsService _buchungen;
    private readonly MitarbeiterService _mitarbeiter;
    private readonly AntragsService _antraege;
    private readonly KontobuchungService _kontobuchungen;
    private readonly IAppUhr _uhr;

    public ZeitkorrekturenModel(
        KontenService konten,
        BuchungsService buchungen,
        MitarbeiterService mitarbeiter,
        AntragsService antraege,
        KontobuchungService kontobuchungen,
        IAppUhr uhr,
        IOptions<ZeiterfassungOptions> optionen)
    {
        _konten = konten;
        _buchungen = buchungen;
        _mitarbeiter = mitarbeiter;
        _antraege = antraege;
        _kontobuchungen = kontobuchungen;
        _uhr = uhr;
        Optionen = optionen.Value;
    }

    [BindProperty(SupportsGet = true)]
    public string? Monat { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? Tag { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Ma { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool Neu { get; set; }

    public ZeiterfassungOptions Optionen { get; }
    public DateOnly MonatsBeginn { get; private set; }
    public DateOnly Heute => _uhr.Heute;
    public List<TagesAuswertung> Tage { get; private set; } = new();
    public TagesAuswertung? Auswahl { get; private set; }
    public List<Buchungsprotokoll> Verlauf { get; private set; } = new();
    public Mitarbeiter Person { get; private set; } = null!;
    public List<Mitarbeiter> Personen { get; private set; } = new();
    public bool OhneZeitgrenze => User.IstPersonal();
    public string MonatText => MonatsBeginn.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    public int ZielId => Ma is not null && User.IstPersonal() ? Ma.Value : BenutzerId;

    /// <summary>Anträge und Umbuchungen stellt jede Person nur für sich selbst.</summary>
    public bool IstEigeneAnsicht => ZielId == BenutzerId;

    public bool UebertragZeitraumAktiv
    {
        get
        {
            var von = UrlaubOptions.Datum(Heute.Year, Optionen.Urlaub.UebertragAntragVon);
            var bis = UrlaubOptions.Datum(Heute.Year, Optionen.Urlaub.UebertragAntragBis);
            return Heute >= von && Heute <= bis;
        }
    }

    public bool UebertragBeantragt { get; private set; }

    public bool IstBearbeitbar(DateOnly datum) =>
        datum <= Heute && (OhneZeitgrenze || datum >= Heute.AddDays(-Optionen.MaxRueckwirkendeTage));

    public async Task<IActionResult> OnGetAsync()
    {
        var person = await _mitarbeiter.LadenAsync(ZielId, HttpContext.RequestAborted);
        if (person is null)
        {
            return NotFound();
        }

        Person = person;
        if (User.IstPersonal())
        {
            Personen = await _mitarbeiter.AlleAsync(nurAktive: true, HttpContext.RequestAborted);
        }

        MonatsBeginn = MonatErmitteln();
        var monatsEnde = MonatsBeginn.AddMonths(1).AddDays(-1);
        Tage = await _konten.AuswertenAsync(ZielId, MonatsBeginn, monatsEnde, HttpContext.RequestAborted);

        var gewaehlt = Tag is { } t && t >= MonatsBeginn && t <= monatsEnde
            ? t
            : Heute >= MonatsBeginn && Heute <= monatsEnde ? Heute : MonatsBeginn;
        Tag = gewaehlt;
        Auswahl = Tage.FirstOrDefault(d => d.Datum == gewaehlt);
        Verlauf = await _buchungen.VerlaufAsync(ZielId, gewaehlt, HttpContext.RequestAborted);
        UebertragBeantragt = await _antraege.UrlaubsuebertragBeantragtAsync(ZielId, Heute.Year, HttpContext.RequestAborted);
        return Page();
    }

    public async Task<IActionResult> OnPostSpeichernAsync([FromForm] BuchungEingabe eingabe)
    {
        Melden(await _buchungen.SpeichernAsync(ZielId, eingabe, BenutzerId, OhneZeitgrenze, HttpContext.RequestAborted));
        return Zurueck(eingabe.Datum);
    }

    public async Task<IActionResult> OnPostLoeschenAsync(int buchungId, DateOnly datum, string? bemerkung)
    {
        Melden(await _buchungen.LoeschenAsync(ZielId, buchungId, bemerkung, BenutzerId, OhneZeitgrenze, HttpContext.RequestAborted));
        return Zurueck(datum);
    }

    public async Task<IActionResult> OnPostBemerkungAsync(DateOnly datum, string? text)
    {
        if (ZielId != BenutzerId && !User.IstPersonal())
        {
            return Forbid();
        }

        Melden(await _buchungen.BemerkungSpeichernAsync(ZielId, datum, text, HttpContext.RequestAborted));
        return Zurueck(datum);
    }

    public async Task<IActionResult> OnPostFreigabeAsync(DateOnly datum, bool ueber10, bool vorRahmen, bool nachRahmen, string? bemerkung)
    {
        if (!IstEigeneAnsicht)
        {
            return Forbid();
        }

        Melden(await _antraege.TagesfreigabeBeantragenAsync(BenutzerId, datum, ueber10, vorRahmen, nachRahmen, bemerkung, HttpContext.RequestAborted));
        return Zurueck(datum);
    }

    public async Task<IActionResult> OnPostUrlaubsuebertragAsync(DateOnly datum, bool kappung, string? bemerkung)
    {
        if (!IstEigeneAnsicht)
        {
            return Forbid();
        }

        if (kappung)
        {
            Fehler(T("Bitte das Häkchen bei „Kappung Urlaub Vorjahr“ entfernen, um die Übertragung zu beantragen."));
        }
        else
        {
            Melden(await _antraege.UrlaubsuebertragBeantragenAsync(BenutzerId, bemerkung, HttpContext.RequestAborted));
        }

        return Zurueck(datum);
    }

    public async Task<IActionResult> OnPostUmbuchungAsync(DateOnly datum, Konto quelle, string? stunden, DateOnly buchungsdatum, string? bemerkung)
    {
        if (!IstEigeneAnsicht)
        {
            return Forbid();
        }

        Melden(await _kontobuchungen.UmbuchenAsync(BenutzerId, quelle, stunden, buchungsdatum, bemerkung, BenutzerId, HttpContext.RequestAborted));
        return Zurueck(datum);
    }

    private IActionResult Zurueck(DateOnly datum) => RedirectToPage(new
    {
        monat = datum.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        tag = datum.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ma = Ma
    });

    private DateOnly MonatErmitteln()
    {
        if (!string.IsNullOrEmpty(Monat)
            && DateOnly.TryParseExact(Monat + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var beginn))
        {
            return beginn;
        }

        if (Tag is { } tag)
        {
            return new DateOnly(tag.Year, tag.Month, 1);
        }

        return new DateOnly(Heute.Year, Heute.Month, 1);
    }

    public object RouteFuer(DateOnly monat, DateOnly? tag = null, bool neu = false) => new
    {
        monat = monat.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        tag = tag?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ma = Ma,
        neu = neu ? true : (bool?)null
    };
}
