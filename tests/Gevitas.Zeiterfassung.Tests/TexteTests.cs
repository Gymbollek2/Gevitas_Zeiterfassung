using System.Text.Json;
using System.Text.RegularExpressions;
using Gevitas.Zeiterfassung.Web.Services;

namespace Gevitas.Zeiterfassung.Tests;

public class TexteTests
{
    private static readonly Regex Platzhalter = new(@"\{\d+[^}]*\}", RegexOptions.Compiled);
    private static readonly Regex Aufruf = new(@"(?<![\w.])[TF]\(""((?:[^""\\]|\\.)*)""", RegexOptions.Compiled);

    private static string WebProjekt()
    {
        var verzeichnis = new DirectoryInfo(AppContext.BaseDirectory);
        while (verzeichnis is not null && !File.Exists(Path.Combine(verzeichnis.FullName, "Gevitas_Zeiterfassung.sln")))
        {
            verzeichnis = verzeichnis.Parent;
        }

        Assert.NotNull(verzeichnis);
        return Path.Combine(verzeichnis!.FullName, "src", "Gevitas.Zeiterfassung.Web");
    }

    [Fact]
    public async Task Uebersetzung_richtet_sich_nach_der_Sprache_der_Anfrage()
    {
        await Task.Yield();
        try
        {
            Assert.Equal("Speichern", Texte.T("Speichern"));

            Texte.Sprache = "en";
            Assert.Equal("Save", Texte.T("Speichern"));
            Assert.Equal("Clocked in at 08:15.", Texte.F("Kommen um {0} Uhr gebucht.", "08:15"));
            Assert.Equal("Unbekannter Text", Texte.T("Unbekannter Text"));
            Assert.Equal("Tu 20/10/2026", Zeitformat.DatumMitWochentag(new DateOnly(2026, 10, 20)));
            Assert.Equal("October", Zeitformat.Monatsname(10));
            Assert.Equal("2.50", Zeitformat.Tage(2.5m));
        }
        finally
        {
            Texte.Sprache = "de";
        }
    }

    [Fact]
    public void Alle_Texte_haben_eine_englische_Uebersetzung()
    {
        var projekt = WebProjekt();
        var dateien = Directory.EnumerateFiles(projekt, "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(projekt, "*.cshtml", SearchOption.AllDirectories))
            .Where(d => !d.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !d.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !d.EndsWith("Uebersetzungen.cs", StringComparison.Ordinal));

        var fehlend = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var datei in dateien)
        {
            foreach (Match m in Aufruf.Matches(File.ReadAllText(datei)))
            {
                var schluessel = m.Groups[1].Value.Replace("\\\"", "\"");
                var nurPlatzhalter = !Platzhalter.Replace(schluessel, string.Empty).Any(char.IsLetter);
                if (!nurPlatzhalter && !Texte.HatUebersetzung(schluessel))
                {
                    fehlend.Add($"{Path.GetFileName(datei)}: {schluessel}");
                }
            }
        }

        Assert.True(fehlend.Count == 0, "Fehlende Übersetzungen:\n" + string.Join("\n", fehlend));
    }

    [Fact]
    public void Konfigurierte_Abwesenheitsarten_und_Feiertage_sind_uebersetzt()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(WebProjekt(), "appsettings.json")));
        var arten = json.RootElement.GetProperty("Zeiterfassung").GetProperty("Abwesenheitsarten")
            .EnumerateArray().Select(a => a.GetProperty("Name").GetString()!);
        var feiertage = new FeiertagsService(TestDaten.Als(new Web.Options.ZeiterfassungOptions { Feiertage = FeiertagsService.BekannteSchluessel.ToList() }))
            .FuerJahr(2026).Values;

        var fehlend = arten.Concat(feiertage).Where(t => !Texte.HatUebersetzung(t)).ToList();

        Assert.Empty(fehlend);
    }

    [Fact]
    public void Platzhalter_in_Uebersetzungen_stimmen_ueberein()
    {
        var fehler = Uebersetzungen.Englisch
            .Where(e => !Platzhalter.Matches(e.Key).Select(m => m.Value).OrderBy(x => x)
                .SequenceEqual(Platzhalter.Matches(e.Value).Select(m => m.Value).OrderBy(x => x)))
            .Select(e => e.Key)
            .ToList();

        Assert.Empty(fehler);
    }
}
