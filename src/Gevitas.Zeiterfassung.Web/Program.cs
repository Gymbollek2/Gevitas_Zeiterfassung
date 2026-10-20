using System.Globalization;
using Gevitas.Zeiterfassung.Web.Data;
using Gevitas.Zeiterfassung.Web.Infrastructure;
using Gevitas.Zeiterfassung.Web.Options;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- Konfiguration ----------
builder.Services.Configure<ZeiterfassungOptions>(builder.Configuration.GetSection(ZeiterfassungOptions.Abschnitt));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.Abschnitt));
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.Abschnitt));
builder.Services.Configure<AuthentifizierungOptions>(builder.Configuration.GetSection(AuthentifizierungOptions.Abschnitt));

// ---------- Datenbank (SQLite) ----------
var connectionString = builder.Configuration.GetConnectionString("Zeiterfassung")
                       ?? throw new InvalidOperationException("ConnectionStrings:Zeiterfassung fehlt in der appsettings.json.");
DatenbankInitialisierung.VerzeichnisSicherstellen(connectionString);
builder.Services.AddDbContext<ZeiterfassungDbContext>(o => o.UseSqlite(connectionString));

// ---------- Data Protection (Cookies bleiben nach Container-Neustart gültig) ----------
var schluesselPfad = builder.Configuration["DataProtection:SchluesselPfad"];
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("Gevitas.Zeiterfassung");
if (!string.IsNullOrWhiteSpace(schluesselPfad))
{
    dataProtection.PersistKeysToFileSystem(Directory.CreateDirectory(schluesselPfad));
}

// ---------- Authentifizierung & Autorisierung ----------
var auth = builder.Configuration.GetSection(AuthentifizierungOptions.Abschnitt).Get<AuthentifizierungOptions>() ?? new AuthentifizierungOptions();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = auth.CookieName;
        o.Cookie.HttpOnly = true;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.LoginPath = "/Account/Anmelden";
        o.LogoutPath = "/Account/Abmelden";
        o.AccessDeniedPath = "/Account/ZugriffVerweigert";
        o.ExpireTimeSpan = TimeSpan.FromMinutes(auth.SitzungsdauerMinuten);
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/");
    o.Conventions.AllowAnonymousToPage("/Account/Anmelden");
    o.Conventions.AllowAnonymousToPage("/Error");
    o.Conventions.AuthorizeFolder("/Verwaltung", Richtlinien.Personal);
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Richtlinien.Fuehrungskraft, p => p.RequireRole(nameof(Gevitas.Zeiterfassung.Web.Domain.Rolle.Fuehrungskraft)))
    .AddPolicy(Richtlinien.Personal, p => p.RequireRole(nameof(Gevitas.Zeiterfassung.Web.Domain.Rolle.Personal)))
    .AddPolicy(Richtlinien.Administrator, p => p.RequireRole(nameof(Gevitas.Zeiterfassung.Web.Domain.Rolle.Administrator)));

builder.Services.AddHealthChecks();

// ---------- Fachliche Dienste ----------
builder.Services.AddZeiterfassungDienste();

var app = builder.Build();

// ---------- Datenbank anlegen und Stammdaten befüllen ----------
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DatenbankInitialisierung>().AusfuehrenAsync();
}

// ---------- HTTP-Pipeline ----------
var deutsch = new CultureInfo("de-DE");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(deutsch),
    SupportedCultures = new[] { deutsch },
    SupportedUICultures = new[] { deutsch }
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseSicherheitsHeader();
app.UseStaticFiles();
app.UseSprache();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UsePasswortAendernPflicht();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapRazorPages();

app.Run();

/// <summary>Für Integrationstests (WebApplicationFactory) öffentlich gemacht.</summary>
public partial class Program
{
}
