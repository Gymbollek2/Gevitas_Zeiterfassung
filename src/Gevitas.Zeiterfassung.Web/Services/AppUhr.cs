using Gevitas.Zeiterfassung.Web.Options;
using Microsoft.Extensions.Options;

namespace Gevitas.Zeiterfassung.Web.Services;

/// <summary>
/// Liefert die aktuelle Zeit in der konfigurierten Zeitzone (z. B. Europe/Berlin),
/// unabhängig von der Zeitzone des Servers oder Containers.
/// </summary>
public interface IAppUhr
{
    DateTime Jetzt { get; }
    DateOnly Heute { get; }

    /// <summary>Aktuelle Uhrzeit auf volle Minuten abgeschnitten (Buchungsgenauigkeit).</summary>
    TimeOnly UhrzeitMinute { get; }
}

public sealed class AppUhr : IAppUhr
{
    private readonly TimeProvider _zeit;
    private readonly TimeZoneInfo _zeitzone;

    public AppUhr(TimeProvider zeit, IOptions<ZeiterfassungOptions> optionen)
    {
        _zeit = zeit;
        _zeitzone = ZeitzoneErmitteln(optionen.Value.Zeitzone);
    }

    public DateTime Jetzt => TimeZoneInfo.ConvertTimeFromUtc(_zeit.GetUtcNow().UtcDateTime, _zeitzone);

    public DateOnly Heute => DateOnly.FromDateTime(Jetzt);

    public TimeOnly UhrzeitMinute
    {
        get
        {
            var jetzt = Jetzt;
            return new TimeOnly(jetzt.Hour, jetzt.Minute);
        }
    }

    private static TimeZoneInfo ZeitzoneErmitteln(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Local;
        }
    }
}
