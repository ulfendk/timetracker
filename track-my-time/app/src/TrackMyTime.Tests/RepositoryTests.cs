using TrackMyTime.Web.Models;
using TrackMyTime.Web.Repositories;
using TrackMyTime.Web.Services;

namespace TrackMyTime.Tests;

[Collection("Database")]
public class RepositoryTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task ClientAndProject_RoundTripThroughSqlite()
    {
        var clients = new ClientRepository(fixture.ConnectionFactory);
        var projects = new ProjectRepository(fixture.ConnectionFactory);

        var clientId = await clients.CreateAsync(new Client { Name = "Acme Corp" });
        var projectId = await projects.CreateAsync(new Project { ClientId = clientId, Name = "Website Revamp", Color = "#7E57C2" });

        var all = await projects.GetAllAsync();
        var project = Assert.Single(all, p => p.Id == projectId);
        Assert.Equal("Website Revamp", project.Name);
        Assert.Equal("Acme Corp", project.ClientName);
        Assert.Equal("#7E57C2", project.Color);
    }

    [Fact]
    public async Task TimeEntry_DateOnlyRoundTripsCorrectlyThroughDapperAndSqlite()
    {
        var clients = new ClientRepository(fixture.ConnectionFactory);
        var projects = new ProjectRepository(fixture.ConnectionFactory);
        var entries = new TimeEntryRepository(fixture.ConnectionFactory);

        var clientId = await clients.CreateAsync(new Client { Name = "Contoso" });
        var projectId = await projects.CreateAsync(new Project { ClientId = clientId, Name = "Support Retainer" });

        var date = new DateOnly(2026, 9, 3);
        await entries.CreateAsync(new TimeEntry { Date = date, ProjectId = projectId, DurationMinutes = 90, Note = "Standup + fixes" });

        var byDate = await entries.GetByDateAsync(date);
        var entry = Assert.Single(byDate);
        Assert.Equal(date, entry.Date);
        Assert.Equal(90, entry.DurationMinutes);
        Assert.Equal("Support Retainer", entry.ProjectName);
    }

    [Fact]
    public async Task DayOff_RoundTripsAndIsFoundByDate()
    {
        var daysOff = new DayOffRepository(fixture.ConnectionFactory);
        var date = new DateOnly(2026, 12, 24);

        await daysOff.CreateAsync(new DayOff { Date = date, Note = "Christmas Eve" });

        var found = await daysOff.GetByDateAsync(date);
        Assert.NotNull(found);
        Assert.Equal("Christmas Eve", found!.Note);
    }

    [Fact]
    public async Task NominalHoursSetting_DecimalRoundTripsExactly()
    {
        var nominalHours = new NominalHoursRepository(fixture.ConnectionFactory);
        await nominalHours.CreateAsync(new NominalHoursSetting { EffectiveFrom = new DateOnly(2026, 1, 1), WeeklyHours = 37.5m });

        var all = await nominalHours.GetAllAsync();
        Assert.Equal(37.5m, all.Single(s => s.EffectiveFrom == new DateOnly(2026, 1, 1)).WeeklyHours);
    }

    [Fact]
    public async Task TimeSummaryService_CombinesRepositoriesIntoAWeekSummary()
    {
        var clients = new ClientRepository(fixture.ConnectionFactory);
        var projects = new ProjectRepository(fixture.ConnectionFactory);
        var entries = new TimeEntryRepository(fixture.ConnectionFactory);
        var daysOff = new DayOffRepository(fixture.ConnectionFactory);
        var nominalHours = new NominalHoursRepository(fixture.ConnectionFactory);

        var clientId = await clients.CreateAsync(new Client { Name = "Fabrikam" });
        var projectId = await projects.CreateAsync(new Project { ClientId = clientId, Name = "Audit" });
        await nominalHours.CreateAsync(new NominalHoursSetting { EffectiveFrom = new DateOnly(2020, 1, 1), WeeklyHours = 37.5m });

        // Monday of a known week.
        var monday = new DateOnly(2026, 9, 7);
        await entries.CreateAsync(new TimeEntry { Date = monday, ProjectId = projectId, DurationMinutes = 8 * 60 });

        var summaryService = new TimeSummaryService(entries, daysOff, nominalHours);
        var week = await summaryService.GetWeekAsync(monday);

        Assert.Equal(37.5m, week.NominalHours);
        Assert.Equal(8m, week.ActualWeekdayHours);
    }
}
