using System.Text.Json;
using TrackMyTime.Web.Models;
using TrackMyTime.Web.Models.Export;
using TrackMyTime.Web.Repositories;
using TrackMyTime.Web.Services;

namespace TrackMyTime.Tests;

[Collection("Database")]
public class ExportImportServiceTests(DatabaseFixture fixture)
{
    private ExportImportService CreateService() => new(
        new ClientRepository(fixture.ConnectionFactory),
        new ProjectRepository(fixture.ConnectionFactory),
        new TimeEntryRepository(fixture.ConnectionFactory),
        new DayOffRepository(fixture.ConnectionFactory),
        new NominalHoursRepository(fixture.ConnectionFactory),
        fixture.ConnectionFactory);

    [Fact]
    public async Task ExportAsync_ThenApplyAsync_RoundTripsAllTables()
    {
        var clients = new ClientRepository(fixture.ConnectionFactory);
        var projects = new ProjectRepository(fixture.ConnectionFactory);
        var entries = new TimeEntryRepository(fixture.ConnectionFactory);
        var daysOff = new DayOffRepository(fixture.ConnectionFactory);
        var nominalHours = new NominalHoursRepository(fixture.ConnectionFactory);

        var clientId = await clients.CreateAsync(new Client { Name = "RoundTrip Client" });
        var projectId = await projects.CreateAsync(new Project { ClientId = clientId, Name = "RoundTrip Project", Color = "#123456" });
        await entries.CreateAsync(new TimeEntry
        {
            Date = new DateOnly(2031, 1, 6), ProjectId = projectId, DurationMinutes = 90,
            StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 30),
        });
        await daysOff.CreateAsync(new DayOff { Date = new DateOnly(2031, 1, 7), Type = DayOffType.Vacation, Note = "Trip" });
        await nominalHours.CreateAsync(new NominalHoursSetting { EffectiveFrom = new DateOnly(2031, 1, 1), WeeklyHours = 37.5m });

        var service = CreateService();
        var document = await service.ExportAsync();

        Assert.Contains(document.Clients, c => c.Name == "RoundTrip Client");
        Assert.Contains(document.Projects, p => p.Name == "RoundTrip Project" && p.Color == "#123456");
        Assert.Contains(document.TimeEntries, t => t.Date == new DateOnly(2031, 1, 6) && t.DurationMinutes == 90);
        Assert.Contains(document.DaysOff, d => d.Date == new DateOnly(2031, 1, 7) && d.Type == DayOffType.Vacation);
        Assert.Contains(document.NominalHoursSettings, n => n.EffectiveFrom == new DateOnly(2031, 1, 1) && n.WeeklyHours == 37.5m);
    }

    [Fact]
    public async Task ExportAsync_SerializesAndDeserializesThroughJsonUnchanged()
    {
        var clients = new ClientRepository(fixture.ConnectionFactory);
        var projects = new ProjectRepository(fixture.ConnectionFactory);
        var entries = new TimeEntryRepository(fixture.ConnectionFactory);
        var daysOff = new DayOffRepository(fixture.ConnectionFactory);
        var clientId = await clients.CreateAsync(new Client { Name = "JSON Client" });
        var projectId = await projects.CreateAsync(new Project { ClientId = clientId, Name = "JSON Project" });
        await entries.CreateAsync(new TimeEntry
        {
            Date = new DateOnly(2035, 6, 15), ProjectId = projectId, DurationMinutes = 105,
            StartTime = new TimeOnly(8, 30), EndTime = new TimeOnly(10, 30), BreakMinutes = 15,
        });
        await daysOff.CreateAsync(new DayOff { Date = new DateOnly(2035, 6, 16), Type = DayOffType.Sickness });

        var service = CreateService();
        var document = await service.ExportAsync();

        var json = JsonSerializer.SerializeToUtf8Bytes(document);
        var roundTripped = JsonSerializer.Deserialize<ExportDocument>(json);

        Assert.NotNull(roundTripped);
        var entry = Assert.Single(roundTripped!.TimeEntries, t => t.Date == new DateOnly(2035, 6, 15));
        Assert.Equal(new TimeOnly(8, 30), entry.StartTime);
        Assert.Equal(new TimeOnly(10, 30), entry.EndTime);
        Assert.Equal(15, entry.BreakMinutes);
        var dayOff = Assert.Single(roundTripped.DaysOff, d => d.Date == new DateOnly(2035, 6, 16));
        Assert.Equal(DayOffType.Sickness, dayOff.Type);
    }

    [Fact]
    public async Task BuildPreviewAsync_DetectsDateConflicts()
    {
        var clients = new ClientRepository(fixture.ConnectionFactory);
        var projects = new ProjectRepository(fixture.ConnectionFactory);
        var entries = new TimeEntryRepository(fixture.ConnectionFactory);
        var clientId = await clients.CreateAsync(new Client { Name = "Preview Client" });
        var projectId = await projects.CreateAsync(new Project { ClientId = clientId, Name = "Preview Project" });
        var conflictDate = new DateOnly(2032, 3, 1);
        await entries.CreateAsync(new TimeEntry { Date = conflictDate, ProjectId = projectId, DurationMinutes = 60 });

        var service = CreateService();
        var document = await service.ExportAsync();
        var preview = await service.BuildPreviewAsync(document);

        Assert.Contains(conflictDate, preview.ConflictingTimeEntryDates);
    }

    [Fact]
    public async Task ApplyAsync_SkipResolution_LeavesExistingDataUntouched()
    {
        var clients = new ClientRepository(fixture.ConnectionFactory);
        var projects = new ProjectRepository(fixture.ConnectionFactory);
        var entries = new TimeEntryRepository(fixture.ConnectionFactory);
        var clientId = await clients.CreateAsync(new Client { Name = "Skip Client" });
        var projectId = await projects.CreateAsync(new Project { ClientId = clientId, Name = "Skip Project" });
        var date = new DateOnly(2033, 4, 1);
        await entries.CreateAsync(new TimeEntry { Date = date, ProjectId = projectId, DurationMinutes = 60, Note = "Original" });

        var service = CreateService();
        var document = await service.ExportAsync();

        // Mutate the document's copy so we can tell whether it actually got applied.
        var incoming = document.TimeEntries.Single(t => t.Date == date);
        incoming.Note = "From import";
        incoming.DurationMinutes = 999;

        await service.ApplyAsync(document, ConflictResolution.Skip);

        var afterEntries = await entries.GetByDateAsync(date);
        var afterEntry = Assert.Single(afterEntries);
        Assert.Equal("Original", afterEntry.Note);
        Assert.Equal(60, afterEntry.DurationMinutes);
    }

    [Fact]
    public async Task ApplyAsync_OverwriteResolution_ReplacesExistingData()
    {
        var clients = new ClientRepository(fixture.ConnectionFactory);
        var projects = new ProjectRepository(fixture.ConnectionFactory);
        var entries = new TimeEntryRepository(fixture.ConnectionFactory);
        var clientId = await clients.CreateAsync(new Client { Name = "Overwrite Client" });
        var projectId = await projects.CreateAsync(new Project { ClientId = clientId, Name = "Overwrite Project" });
        var date = new DateOnly(2034, 5, 1);
        await entries.CreateAsync(new TimeEntry { Date = date, ProjectId = projectId, DurationMinutes = 60, Note = "Original" });

        var service = CreateService();
        var document = await service.ExportAsync();
        var incoming = document.TimeEntries.Single(t => t.Date == date);
        incoming.Note = "From import";
        incoming.DurationMinutes = 120;

        await service.ApplyAsync(document, ConflictResolution.Overwrite);

        var afterEntries = await entries.GetByDateAsync(date);
        var afterEntry = Assert.Single(afterEntries);
        Assert.Equal("From import", afterEntry.Note);
        Assert.Equal(120, afterEntry.DurationMinutes);
    }

    [Fact]
    public async Task ApplyAsync_MatchesClientsAndProjectsByNameRatherThanDuplicating()
    {
        var clients = new ClientRepository(fixture.ConnectionFactory);
        var projects = new ProjectRepository(fixture.ConnectionFactory);
        var clientId = await clients.CreateAsync(new Client { Name = "Match Client" });
        await projects.CreateAsync(new Project { ClientId = clientId, Name = "Match Project" });

        var service = CreateService();
        var document = await service.ExportAsync();

        // Re-applying the same export (no new dated data, so no conflicts) must not create
        // duplicate clients/projects.
        await service.ApplyAsync(document, ConflictResolution.Skip);

        var allClients = await clients.GetAllAsync(includeInactive: true);
        var allProjects = await projects.GetAllAsync(includeInactive: true);
        Assert.Single(allClients, c => c.Name == "Match Client");
        Assert.Single(allProjects, p => p.Name == "Match Project");
    }
}
