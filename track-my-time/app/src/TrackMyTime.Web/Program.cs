using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using MudBlazor.Services;
using TrackMyTime.Web.Components;
using TrackMyTime.Web.Data;
using TrackMyTime.Web.Repositories;
using TrackMyTime.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// HA apps get their console output surfaced as the app's log tab - plain, undecorated lines
// read best there.
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss ";
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

// Backlog #17/#19: English is the default; Danish is the only other supported UI language for
// now. Switching culture also fixes decimal-comma number entry/display for free, since neither
// this app's ToString("0.##") call sites nor MudBlazor's MudNumericField<decimal> pass an
// explicit culture - both already follow CultureInfo.CurrentCulture.
//
// No ResourcesPath here on purpose: despite SharedResource.resx living under Resources/, this
// SDK embeds it (verified via `strings` on the built dll) as "TrackMyTime.Web.SharedResource" -
// no "Resources." segment - so IStringLocalizer<SharedResource> must resolve against that same
// bare name (SharedResource.cs's namespace is plain TrackMyTime.Web, not .Resources, for the
// same reason). Setting ResourcesPath = "Resources" here would make the localizer look for
// "TrackMyTime.Web.Resources.SharedResource" instead, find nothing, and silently fall back to
// echoing the raw resource key on every page.
builder.Services.AddLocalization();
var supportedCultures = new[] { new CultureInfo("en"), new CultureInfo("da") };
var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures,
};

// Without this, keys default to the container's writable layer and are regenerated on every
// restart (i.e. every app upgrade), invalidating antiforgery tokens for any open session.
// Persisting under /data keeps them stable across upgrades like everything else here.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(AppPaths.DataDirectory, "dataprotection-keys")));

builder.Services.AddSingleton<SqliteConnectionFactory>();
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<ITimeEntryRepository, TimeEntryRepository>();
builder.Services.AddScoped<IDayOffRepository, DayOffRepository>();
builder.Services.AddScoped<INominalHoursRepository, NominalHoursRepository>();
builder.Services.AddScoped<TimeSummaryService>();
builder.Services.AddScoped<ExportImportService>();

builder.Services.AddHttpClient<HomeAssistantSupervisorClient>();
builder.Services.AddSingleton<MqttPublisherService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MqttPublisherService>());
builder.Services.AddHostedService<DailyBackupService>();

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Logger);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// Home Assistant's ingress proxy fronts every app under a path that can change across
// restarts, passed in X-Ingress-Path (not the standard X-Forwarded-Prefix). Map it onto
// PathBase, before routing/static files/component mapping, so generated links, static assets,
// and the Blazor Server SignalR circuit all resolve correctly through the ingress iframe.
app.Use((context, next) =>
{
    var ingressPath = context.Request.Headers["X-Ingress-Path"].ToString();
    if (!string.IsNullOrEmpty(ingressPath))
    {
        var pathBase = new PathString(ingressPath);
        if (context.Request.Path.StartsWithSegments(pathBase, out var remaining))
        {
            context.Request.Path = remaining.HasValue ? remaining : "/";
        }
        context.Request.PathBase = pathBase;
    }
    return next(context);
});

// Must come after the PathBase middleware above so the culture cookie/redirect logic below
// operates on the already-corrected (ingress-stripped) request, consistent with every other
// route in the app.
app.UseRequestLocalization(localizationOptions);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Blazor Server can't hot-swap culture mid-circuit, so switching language is a plain GET
// endpoint (standard ASP.NET Core localization pattern) that sets the culture cookie and does a
// full page redirect, rather than a SignalR/component interaction. redirectUri is a same-app
// relative path (built by the language switcher from NavigationManager, no leading "/") - it's
// combined with the current request's PathBase (already ingress-corrected above) rather than
// trusted as an absolute URL, so the redirect keeps working under Home Assistant's ingress proxy.
app.MapGet("/culture/set", (HttpContext httpContext, string culture, string? redirectUri) =>
{
    httpContext.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
        new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });

    var relative = (redirectUri ?? string.Empty).TrimStart('/');
    return Results.LocalRedirect($"{httpContext.Request.PathBase}/{relative}");
});

// Blazor Server can't trigger a browser file download from C# alone, so exporting data is a
// plain GET endpoint the "Data" page links to rather than a server-side interaction.
app.MapGet("/api/export", async (ExportImportService exportImportService) =>
{
    var document = await exportImportService.ExportAsync();
    var json = JsonSerializer.SerializeToUtf8Bytes(document, new JsonSerializerOptions { WriteIndented = true });
    return Results.File(json, "application/json", $"trackmytime-export-{DateTime.UtcNow:yyyyMMdd}.json");
});

app.Run();
