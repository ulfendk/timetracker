using Microsoft.Data.Sqlite;
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
    public async Task TimeEntry_StartEndBreakRoundTripThroughSqlite()
    {
        var clients = new ClientRepository(fixture.ConnectionFactory);
        var projects = new ProjectRepository(fixture.ConnectionFactory);
        var entries = new TimeEntryRepository(fixture.ConnectionFactory);

        var clientId = await clients.CreateAsync(new Client { Name = "Northwind" });
        var projectId = await projects.CreateAsync(new Project { ClientId = clientId, Name = "Migration" });

        var date = new DateOnly(2026, 9, 4);
        var start = new TimeOnly(9, 0);
        var end = new TimeOnly(16, 15);
        await entries.CreateAsync(new TimeEntry
        {
            Date = date, ProjectId = projectId, DurationMinutes = 405,
            StartTime = start, EndTime = end, BreakMinutes = 30,
        });

        var byDate = await entries.GetByDateAsync(date);
        var entry = Assert.Single(byDate);
        Assert.Equal(start, entry.StartTime);
        Assert.Equal(end, entry.EndTime);
        Assert.Equal(30, entry.BreakMinutes);
        Assert.Equal(405, entry.DurationMinutes);
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
        Assert.Null(found.Type);
    }

    [Fact]
    public async Task DayOff_TypeRoundTripsThroughSqlite()
    {
        var daysOff = new DayOffRepository(fixture.ConnectionFactory);
        var date = new DateOnly(2027, 1, 4);

        await daysOff.CreateAsync(new DayOff { Date = date, Note = "Flu", Type = DayOffType.Sickness });

        var found = await daysOff.GetByDateAsync(date);
        Assert.NotNull(found);
        Assert.Equal(DayOffType.Sickness, found!.Type);
    }

    [Fact]
    public async Task DayOffRepository_UpdateAsync_ChangesDateNoteAndType()
    {
        var daysOff = new DayOffRepository(fixture.ConnectionFactory);
        var id = await daysOff.CreateAsync(new DayOff { Date = new DateOnly(2027, 2, 1), Type = DayOffType.Vacation });

        await daysOff.UpdateAsync(new DayOff
        {
            Id = id, Date = new DateOnly(2027, 2, 2), Note = "Rescheduled", Type = DayOffType.ForcedTimeOff,
        });

        var found = await daysOff.GetByDateAsync(new DateOnly(2027, 2, 2));
        Assert.NotNull(found);
        Assert.Equal("Rescheduled", found!.Note);
        Assert.Equal(DayOffType.ForcedTimeOff, found.Type);
        Assert.Null(await daysOff.GetByDateAsync(new DateOnly(2027, 2, 1)));
    }

    [Fact]
    public async Task DayOffRepository_CountByTypeAndDateRangeAsync_CountsOnlyMatchingTypeInRange()
    {
        var daysOff = new DayOffRepository(fixture.ConnectionFactory);
        await daysOff.CreateAsync(new DayOff { Date = new DateOnly(2027, 3, 1), Type = DayOffType.Sickness });
        await daysOff.CreateAsync(new DayOff { Date = new DateOnly(2027, 3, 2), Type = DayOffType.Sickness });
        await daysOff.CreateAsync(new DayOff { Date = new DateOnly(2027, 3, 3), Type = DayOffType.Vacation });
        await daysOff.CreateAsync(new DayOff { Date = new DateOnly(2027, 4, 1), Type = DayOffType.Sickness }); // out of range

        var count = await daysOff.CountByTypeAndDateRangeAsync(
            DayOffType.Sickness, new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 31));

        Assert.Equal(2, count);
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
    public async Task NominalHoursSetting_UpdateAsync_ChangesEffectiveFromAndWeeklyHours()
    {
        var nominalHours = new NominalHoursRepository(fixture.ConnectionFactory);
        var id = await nominalHours.CreateAsync(new NominalHoursSetting { EffectiveFrom = new DateOnly(2028, 1, 1), WeeklyHours = 37.5m });

        await nominalHours.UpdateAsync(new NominalHoursSetting { Id = id, EffectiveFrom = new DateOnly(2028, 2, 1), WeeklyHours = 30m });

        var all = await nominalHours.GetAllAsync();
        var setting = Assert.Single(all, s => s.Id == id);
        Assert.Equal(new DateOnly(2028, 2, 1), setting.EffectiveFrom);
        Assert.Equal(30m, setting.WeeklyHours);
    }

    [Fact]
    public async Task NominalHoursSetting_UpdateToConflictingEffectiveFrom_ThrowsSqliteException()
    {
        var nominalHours = new NominalHoursRepository(fixture.ConnectionFactory);
        await nominalHours.CreateAsync(new NominalHoursSetting { EffectiveFrom = new DateOnly(2029, 1, 1), WeeklyHours = 37.5m });
        var secondId = await nominalHours.CreateAsync(new NominalHoursSetting { EffectiveFrom = new DateOnly(2029, 2, 1), WeeklyHours = 30m });

        await Assert.ThrowsAsync<SqliteException>(() =>
            nominalHours.UpdateAsync(new NominalHoursSetting { Id = secondId, EffectiveFrom = new DateOnly(2029, 1, 1), WeeklyHours = 30m }));
    }

    [Fact]
    public async Task NominalHoursSetting_DeleteAsync_RemovesRow()
    {
        var nominalHours = new NominalHoursRepository(fixture.ConnectionFactory);
        var id = await nominalHours.CreateAsync(new NominalHoursSetting { EffectiveFrom = new DateOnly(2030, 1, 1), WeeklyHours = 37.5m });

        await nominalHours.DeleteAsync(id);

        Assert.DoesNotContain(await nominalHours.GetAllAsync(), s => s.Id == id);
    }

    [Fact]
    public async Task ClientRepository_UpdateAsync_RenamesClient()
    {
        var clients = new ClientRepository(fixture.ConnectionFactory);
        var id = await clients.CreateAsync(new Client { Name = "Old Name" });

        await clients.UpdateAsync(new Client { Id = id, Name = "New Name", IsActive = true });

        var client = await clients.GetByIdAsync(id);
        Assert.NotNull(client);
        Assert.Equal("New Name", client!.Name);
    }

    [Fact]
    public async Task ProjectRepository_UpdateAsync_ReassignsClientAndRenames()
    {
        var clients = new ClientRepository(fixture.ConnectionFactory);
        var projects = new ProjectRepository(fixture.ConnectionFactory);
        var firstClientId = await clients.CreateAsync(new Client { Name = "First Client" });
        var secondClientId = await clients.CreateAsync(new Client { Name = "Second Client" });
        var projectId = await projects.CreateAsync(new Project { ClientId = firstClientId, Name = "Old Project" });

        await projects.UpdateAsync(new Project { Id = projectId, ClientId = secondClientId, Name = "New Project", IsActive = true });

        var project = await projects.GetByIdAsync(projectId);
        Assert.NotNull(project);
        Assert.Equal(secondClientId, project!.ClientId);
        Assert.Equal("New Project", project.Name);
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

    [Fact]
    public async Task TimeSummaryService_GetWeekAsync_WeeksOffsetLooksAtAnEarlierWeek()
    {
        var clients = new ClientRepository(fixture.ConnectionFactory);
        var projects = new ProjectRepository(fixture.ConnectionFactory);
        var entries = new TimeEntryRepository(fixture.ConnectionFactory);
        var daysOff = new DayOffRepository(fixture.ConnectionFactory);
        var nominalHours = new NominalHoursRepository(fixture.ConnectionFactory);

        var clientId = await clients.CreateAsync(new Client { Name = "Contoso" });
        var projectId = await projects.CreateAsync(new Project { ClientId = clientId, Name = "Support" });
        // A distinct EffectiveFrom from other tests in this collection, which share one database.
        await nominalHours.CreateAsync(new NominalHoursSetting { EffectiveFrom = new DateOnly(2021, 1, 1), WeeklyHours = 37.5m });

        // Monday of a known week, and the Monday of the week before it - distinct from the week
        // used by other tests in this shared-database collection.
        var thisMonday = new DateOnly(2033, 3, 7);
        var lastMonday = thisMonday.AddDays(-7);
        await entries.CreateAsync(new TimeEntry { Date = thisMonday, ProjectId = projectId, DurationMinutes = 4 * 60 });
        await entries.CreateAsync(new TimeEntry { Date = lastMonday, ProjectId = projectId, DurationMinutes = 6 * 60 });

        var summaryService = new TimeSummaryService(entries, daysOff, nominalHours);
        var thisWeek = await summaryService.GetWeekAsync(thisMonday);
        var lastWeek = await summaryService.GetWeekAsync(thisMonday, weeksOffset: -1);

        Assert.Equal(4m, thisWeek.ActualWeekdayHours);
        Assert.Equal(6m, lastWeek.ActualWeekdayHours);
        Assert.Equal(lastMonday, lastWeek.From);
    }
}
