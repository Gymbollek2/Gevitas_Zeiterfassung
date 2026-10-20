using System.Globalization;
using Gevitas.Zeiterfassung.Web.Options;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ZeiterfassungOptions>(builder.Configuration.GetSection(ZeiterfassungOptions.Abschnitt));

builder.Services.AddRazorPages();
builder.Services.AddHealthChecks();

var app = builder.Build();

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
app.UseStaticFiles();
app.UseRouting();

app.MapHealthChecks("/health");
app.MapRazorPages();

app.Run();

/// <summary>Für Integrationstests (WebApplicationFactory) öffentlich gemacht.</summary>
public partial class Program
{
}
