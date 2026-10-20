using System.Globalization;

namespace Gevitas.Zeiterfassung.Web.Services;

/// <summary>
/// Darstellung von Zeiten im Format Stunden:Minuten (z. B. 10:50 = 10 Stunden und 50 Minuten).
/// Die Zahl hinter dem Doppelpunkt ist keine Dezimalzahl, sondern die Anzahl der Minuten.
/// </summary>
public static class Zeitformat
{
    private static readonly CultureInfo Deutsch = new("de-DE");
    private static readonly string[] Wochentage = { "So", "Mo", "Di", "Mi", "Do", "Fr", "Sa" };
    private static readonly string[] Monate =
    {
        "Januar", "Februar", "März", "April", "Mai", "Juni",
        "Juli", "August", "September", "Oktober", "November", "Dezember"
    };

    public static string Dauer(int minuten)
    {
        var vorzeichen = minuten < 0 ? "-" : string.Empty;
        var betrag = Math.Abs((long)minuten);
        return $"{vorzeichen}{betrag / 60}:{betrag % 60:00}";
    }

    public static string Uhrzeit(TimeOnly? zeit) => zeit?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? string.Empty;

    public static string Datum(DateOnly datum) => datum.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    public static string DatumMitWochentag(DateOnly datum) => $"{Wochentag(datum)}. {Datum(datum)}";

    public static string Wochentag(DateOnly datum) => Wochentage[(int)datum.DayOfWeek];

    public static string Monatsname(int monat) => Monate[monat - 1];

    public static string Tage(decimal tage) => tage.ToString("0.00", Deutsch);

    public static string Zeitpunkt(DateTime zeitpunkt) => zeitpunkt.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// Liest eine Dauer ein. Erlaubt "H:mm", "-H:mm" oder ganze Stunden ("3").
    /// </summary>
    public static bool TryParseDauer(string? text, out int minuten)
    {
        minuten = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        text = text.Trim();
        var negativ = text.StartsWith('-');
        if (negativ)
        {
            text = text[1..];
        }

        int stunden;
        int min = 0;
        var teile = text.Split(':');
        if (teile.Length == 1)
        {
            if (!int.TryParse(teile[0], NumberStyles.None, CultureInfo.InvariantCulture, out stunden))
            {
                return false;
            }
        }
        else if (teile.Length == 2)
        {
            if (!int.TryParse(teile[0], NumberStyles.None, CultureInfo.InvariantCulture, out stunden)
                || !int.TryParse(teile[1], NumberStyles.None, CultureInfo.InvariantCulture, out min)
                || teile[1].Length != 2
                || min > 59)
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        minuten = stunden * 60 + min;
        if (negativ)
        {
            minuten = -minuten;
        }

        return true;
    }

    /// <summary>Minuten seit Mitternacht.</summary>
    public static int MinutenSeitMitternacht(TimeOnly zeit) => zeit.Hour * 60 + zeit.Minute;
}
