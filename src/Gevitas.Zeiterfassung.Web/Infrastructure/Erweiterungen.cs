using Gevitas.Zeiterfassung.Web.Data;
using Gevitas.Zeiterfassung.Web.Options;
using Gevitas.Zeiterfassung.Web.Services;
using Microsoft.Extensions.Options;

namespace Gevitas.Zeiterfassung.Web.Infrastructure;

public static class Richtlinien
{
    public const string Fuehrungskraft = "Fuehrungskraft";
    public const string Personal = "Personal";
    public const string Administrator = "Administrator";
}

public static class DiensteErweiterungen
{
    public static IServiceCollection AddZeiterfassungDienste(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAppUhr, AppUhr>();
        services.AddSingleton<PasswortService>();
        services.AddScoped<AnmeldeService>();
        services.AddScoped<DatenbankInitialisierung>();
        services.AddScoped<MitarbeiterService>();

        services.AddSingleton<IFeiertagsService, FeiertagsService>();
        services.AddSingleton<Arbeitstagkalender>();
        services.AddSingleton(sp => new Tagesberechnung(
            ArbeitszeitRegeln.Aus(sp.GetRequiredService<IOptions<ZeiterfassungOptions>>().Value)));
        services.AddScoped<KontenService>();
        services.AddScoped<BuchungsService>();

        services.AddSingleton<IEmailVersand, SmtpEmailVersand>();
        services.AddScoped<BenachrichtigungsService>();
        services.AddScoped<UrlaubService>();
        services.AddScoped<AntragsService>();
        services.AddScoped<KontobuchungService>();
        services.AddScoped<KalenderService>();
        services.AddScoped<RegelverletzungService>();
        services.AddScoped<BerichtService>();
        return services;
    }
}

public static class PipelineErweiterungen
{
    /// <summary>Setzt grundlegende Sicherheits-Header für alle Antworten.</summary>
    public static IApplicationBuilder UseSicherheitsHeader(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var header = context.Response.Headers;
            header["X-Content-Type-Options"] = "nosniff";
            header["X-Frame-Options"] = "DENY";
            header["Referrer-Policy"] = "strict-origin-when-cross-origin";
            header["Content-Security-Policy"] =
                "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; frame-ancestors 'none'; form-action 'self'";
            await next();
        });

    /// <summary>
    /// Leitet Benutzer, die ihr Startpasswort noch ändern müssen, auf die Passwortseite um.
    /// </summary>
    public static IApplicationBuilder UsePasswortAendernPflicht(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var benutzer = context.User;
            if (benutzer.Identity?.IsAuthenticated == true
                && benutzer.HasClaim(Claimtypen.PasswortAendern, "1"))
            {
                var pfad = context.Request.Path;
                var erlaubt = pfad.StartsWithSegments("/Account/PasswortAendern", StringComparison.OrdinalIgnoreCase)
                              || pfad.StartsWithSegments("/Account/Abmelden", StringComparison.OrdinalIgnoreCase)
                              || pfad.StartsWithSegments("/css")
                              || pfad.StartsWithSegments("/js")
                              || pfad.StartsWithSegments("/health")
                              || pfad.Value?.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) == true;
                if (!erlaubt)
                {
                    context.Response.Redirect("/Account/PasswortAendern");
                    return;
                }
            }

            await next();
        });
}
