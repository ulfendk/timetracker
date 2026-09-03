using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
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

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Blazor Server can't trigger a browser file download from C# alone, so exporting data is a
// plain GET endpoint the "Data" page links to rather than a server-side interaction.
app.MapGet("/api/export", async (ExportImportService exportImportService) =>
{
    var document = await exportImportService.ExportAsync();
    var json = JsonSerializer.SerializeToUtf8Bytes(document, new JsonSerializerOptions { WriteIndented = true });
    return Results.File(json, "application/json", $"trackmytime-export-{DateTime.UtcNow:yyyyMMdd}.json");
});

app.Run();
