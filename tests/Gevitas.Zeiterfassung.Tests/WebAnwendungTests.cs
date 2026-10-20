using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Gevitas.Zeiterfassung.Tests;

/// <summary>Startet die komplette Anwendung mit einer eigenen SQLite-Datei und Demodaten.</summary>
public sealed class ZeiterfassungFactory : WebApplicationFactory<Program>
{
    private readonly string _verzeichnis = Path.Combine(Path.GetTempPath(), "gevitas-tests-" + Guid.NewGuid().ToString("N"));

    public ZeiterfassungFactory()
    {
        Directory.CreateDirectory(_verzeichnis);
        // Umgebungsvariablen werden von WebApplication.CreateBuilder sicher vor dem Lesen der Konfiguration ausgewertet
        Environment.SetEnvironmentVariable("ConnectionStrings__Zeiterfassung", $"Data Source={Path.Combine(_verzeichnis, "test.db")}");
        Environment.SetEnvironmentVariable("DataProtection__SchluesselPfad", Path.Combine(_verzeichnis, "keys"));
        Environment.SetEnvironmentVariable("Seed__Demodaten", "true");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            Directory.Delete(_verzeichnis, recursive: true);
        }
        catch (IOException)
        {
            // SQLite kann die Datei unter Windows noch kurz sperren – temporäre Dateien sind unkritisch
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

public class WebAnwendungTests : IClassFixture<ZeiterfassungFactory>
{
    private static readonly Regex Token = new("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"", RegexOptions.Compiled);
    private readonly ZeiterfassungFactory _factory;

    public WebAnwendungTests(ZeiterfassungFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Client() => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private static async Task<HttpResponseMessage> AnmeldenAsync(HttpClient client, string benutzer, string passwort)
    {
        var seite = await client.GetStringAsync("/Account/Anmelden");
        var token = Token.Match(seite).Groups[1].Value;
        return await client.PostAsync("/Account/Anmelden", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Benutzername"] = benutzer,
            ["Passwort"] = passwort,
            ["__RequestVerificationToken"] = token
        }));
    }

    [Fact]
    public async Task Health_Check_ist_ohne_Anmeldung_erreichbar()
    {
        var antwort = await Client().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
    }

    [Fact]
    public async Task Anonyme_Benutzer_werden_zur_Anmeldung_umgeleitet()
    {
        var antwort = await Client().GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        Assert.Contains("/Account/Anmelden", antwort.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Antworten_enthalten_Sicherheits_Header()
    {
        var antwort = await Client().GetAsync("/Account/Anmelden");

        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        Assert.True(antwort.Headers.Contains("Content-Security-Policy"));
        Assert.Equal("DENY", antwort.Headers.GetValues("X-Frame-Options").Single());
    }

    [Fact]
    public async Task Falsches_Passwort_wird_abgelehnt()
    {
        var antwort = await AnmeldenAsync(Client(), "mitarbeiter", "falsch");

        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        Assert.Contains("nicht korrekt", await antwort.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("mitarbeiter")]
    [InlineData("1003")]
    [InlineData(" MITARBEITER ")]
    public async Task Anmeldung_mit_Benutzername_oder_Personalnummer(string kennung)
    {
        var client = Client();
        var antwort = await AnmeldenAsync(client, kennung, "Demo1234!");

        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        Assert.Equal("/", antwort.Headers.Location!.ToString());
        Assert.Contains("Mischa Mitarbeiter", await client.GetStringAsync("/MeineDaten"));
    }

    [Fact]
    public async Task Administrator_muss_das_Startpasswort_aendern()
    {
        var client = Client();
        var antwort = await AnmeldenAsync(client, "admin", "Gevitas#Start2026");

        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        Assert.Contains("/Account/PasswortAendern", antwort.Headers.Location!.ToString());

        var dashboard = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, dashboard.StatusCode);
        Assert.Contains("/Account/PasswortAendern", dashboard.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Mitarbeiter_hat_keinen_Zugriff_auf_die_Verwaltung()
    {
        var client = Client();
        await AnmeldenAsync(client, "mitarbeiter", "Demo1234!");

        var antwort = await client.GetAsync("/Verwaltung/Mitarbeiter");

        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        Assert.Contains("ZugriffVerweigert", antwort.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Terminal_bucht_Kommen_und_Dashboard_zeigt_Anwesenheit()
    {
        var client = Client();
        await AnmeldenAsync(client, "kollegin", "Demo1234!");
        var terminal = await client.GetStringAsync("/Terminal");
        var token = Token.Match(terminal).Groups[1].Value;

        var antwort = await client.PostAsync("/Terminal", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["aktion"] = "Kommen",
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        var dashboard = await client.GetStringAsync("/");
        Assert.Contains("Anwesend", dashboard);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Terminal")]
    [InlineData("/Zeitwirtschaft/Zeitkorrekturen")]
    [InlineData("/Zeitwirtschaft/Zeitkorrekturen?neu=true")]
    [InlineData("/Zeitwirtschaft/Monatskalender")]
    [InlineData("/Zeitwirtschaft/Jahreskalender")]
    [InlineData("/Zeitwirtschaft/Regelverletzungen")]
    [InlineData("/Zeitwirtschaft/Gruppenkalender")]
    [InlineData("/Antraege")]
    [InlineData("/Antraege?sicht=Genehmiger&filter=Alle")]
    [InlineData("/Antraege/Abwesenheit")]
    [InlineData("/Antraege/AbwesenheitLoeschen")]
    [InlineData("/Berichte")]
    [InlineData("/Berichte/Bericht?nr=500")]
    [InlineData("/Berichte/Bericht?nr=539")]
    [InlineData("/Berichte/Bericht?nr=540")]
    [InlineData("/Berichte/Bericht?nr=542")]
    [InlineData("/Berichte/Bericht?nr=544")]
    [InlineData("/Berichte/Bericht?nr=546")]
    [InlineData("/MeineDaten")]
    [InlineData("/Information")]
    [InlineData("/Einstellungen")]
    [InlineData("/Account/PasswortAendern")]
    [InlineData("/Verwaltung/Mitarbeiter")]
    [InlineData("/Verwaltung/MitarbeiterBearbeiten")]
    [InlineData("/Verwaltung/Kontokorrekturen")]
    public async Task Alle_Seiten_werden_fuer_die_Personalabteilung_fehlerfrei_angezeigt(string url)
    {
        var client = Client();
        await AnmeldenAsync(client, "personal", "Demo1234!");

        var antwort = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
    }

    [Fact]
    public async Task Bericht_kann_als_Csv_exportiert_werden()
    {
        var client = Client();
        await AnmeldenAsync(client, "fuehrung", "Demo1234!");

        var antwort = await client.GetAsync("/Berichte/Bericht?nr=539&format=csv");

        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        Assert.Equal("text/csv", antwort.Content.Headers.ContentType!.MediaType);
        Assert.Contains("Mitarbeiter, Mischa", await antwort.Content.ReadAsStringAsync());
    }
}
