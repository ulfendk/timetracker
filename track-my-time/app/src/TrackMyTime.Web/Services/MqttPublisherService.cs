using System.Globalization;
using System.Text.Json;
using MQTTnet;

namespace TrackMyTime.Web.Services;

/// <summary>Publishes today/week/month actual-vs-nominal figures to Home Assistant via MQTT
/// Discovery, so they show up as ordinary sensor entities in any dashboard. Runs entirely
/// best-effort: with no broker configured, or while disconnected, the app keeps working — this
/// service just skips publishing and logs why.</summary>
public sealed class MqttPublisherService(
    IServiceScopeFactory scopeFactory,
    HomeAssistantSupervisorClient supervisorClient,
    ILogger<MqttPublisherService> logger) : BackgroundService
{
    private const string DeviceId = "trackmytime";
    private const string StatusTopic = "trackmytime/status";
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _refreshRequested = new(1, 1);
    private IMqttClient? _client;

    /// <summary>For the Settings page's status indicator.</summary>
    public bool IsConnected => _client?.IsConnected ?? false;

    /// <summary>Call after any change to time entries, days off, or nominal hours so the
    /// dashboard figures update promptly instead of waiting for the next periodic refresh.</summary>
    public void RequestRefresh()
    {
        if (_refreshRequested.CurrentCount == 0)
        {
            _refreshRequested.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var mqttService = await supervisorClient.TryGetMqttServiceAsync(stoppingToken);
        if (mqttService is null)
        {
            return;
        }

        var factory = new MqttClientFactory();
        _client = factory.CreateMqttClient();

        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithClientId("track-my-time")
            .WithTcpServer(mqttService.Host, mqttService.Port)
            .WithWillTopic(StatusTopic)
            .WithWillPayload("offline")
            .WithWillRetain(true);

        if (mqttService.Ssl)
        {
            optionsBuilder = optionsBuilder.WithTlsOptions(o => o.UseTls());
        }
        if (!string.IsNullOrEmpty(mqttService.Username))
        {
            optionsBuilder = optionsBuilder.WithCredentials(mqttService.Username, mqttService.Password);
        }

        try
        {
            await _client.ConnectAsync(optionsBuilder.Build(), stoppingToken);
            await PublishDiscoveryConfigAsync(stoppingToken);
            await PublishRetainedAsync(StatusTopic, "online", stoppingToken);
            logger.LogInformation("Connected to MQTT broker at {Host}:{Port} for Home Assistant discovery",
                mqttService.Host, mqttService.Port);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not connect to the MQTT broker - MQTT publishing is disabled for this run.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishStateAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Failed to publish state to MQTT");
            }

            await WaitForNextRefreshAsync(RefreshInterval, _refreshRequested, stoppingToken);
        }
    }

    /// <summary>Waits for whichever comes first: the periodic interval, or an on-demand
    /// <see cref="RequestRefresh"/> call. Pulled out as its own testable method because the
    /// obvious alternative - PeriodicTimer raced via Task.WhenAny against the refresh signal -
    /// is broken: PeriodicTimer only supports one outstanding WaitForNextTickAsync call at a
    /// time, and Task.WhenAny abandons whichever side loses the race without awaiting it out.
    /// If the refresh signal wins, the previous timer wait is left dangling, and starting a
    /// second one on the same PeriodicTimer next iteration throws InvalidOperationException
    /// (this shipped and crashed the whole host under a real broker - see
    /// MqttPublisherServiceTests.WaitForNextRefreshAsync_SurvivesRapidRefreshRequests). A fresh
    /// Task.Delay per call has no such restriction and is safe to abandon.</summary>
    internal static async Task WaitForNextRefreshAsync(TimeSpan interval, SemaphoreSlim refreshRequested, CancellationToken cancellationToken)
    {
        var delay = Task.Delay(interval, cancellationToken);
        var refresh = refreshRequested.WaitAsync(cancellationToken);
        await Task.WhenAny(delay, refresh);
    }

    private async Task PublishStateAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var summaryService = scope.ServiceProvider.GetRequiredService<TimeSummaryService>();

        var today = DateOnly.FromDateTime(DateTime.Now);
        var todaySummary = await summaryService.GetTodayAsync(today);
        var week = await summaryService.GetWeekAsync(today);
        var month = await summaryService.GetMonthAsync(today);

        await PublishRetainedAsync("trackmytime/sensor/today_actual_hours/state", Round(todaySummary.TotalHours), cancellationToken);
        await PublishRetainedAsync("trackmytime/sensor/week_actual_hours/state", Round(week.ActualHours), cancellationToken);
        await PublishRetainedAsync("trackmytime/sensor/week_nominal_hours/state", Round(week.NominalHours), cancellationToken);
        await PublishRetainedAsync("trackmytime/sensor/week_delta_hours/state", Round(week.DeltaHours), cancellationToken);
        await PublishRetainedAsync("trackmytime/sensor/month_actual_hours/state", Round(month.ActualHours), cancellationToken);
        await PublishRetainedAsync("trackmytime/sensor/month_nominal_hours/state", Round(month.NominalHours), cancellationToken);
        await PublishRetainedAsync("trackmytime/sensor/month_delta_hours/state", Round(month.DeltaHours), cancellationToken);
    }

    private async Task PublishDiscoveryConfigAsync(CancellationToken cancellationToken)
    {
        (string ObjectId, string Name, string Icon)[] sensors =
        [
            ("today_actual_hours", "TMT Today Actual Hours", "mdi:clock-check-outline"),
            ("week_actual_hours", "TMT Week Actual Hours", "mdi:calendar-week"),
            ("week_nominal_hours", "TMT Week Nominal Hours", "mdi:calendar-week-outline"),
            ("week_delta_hours", "TMT Week Delta Hours", "mdi:scale-balance"),
            ("month_actual_hours", "TMT Month Actual Hours", "mdi:calendar-month"),
            ("month_nominal_hours", "TMT Month Nominal Hours", "mdi:calendar-month-outline"),
            ("month_delta_hours", "TMT Month Delta Hours", "mdi:scale-balance"),
        ];

        foreach (var (objectId, name, icon) in sensors)
        {
            var config = new
            {
                name,
                unique_id = $"{DeviceId}_{objectId}",
                state_topic = $"trackmytime/sensor/{objectId}/state",
                availability_topic = StatusTopic,
                unit_of_measurement = "h",
                icon,
                device = new
                {
                    identifiers = new[] { DeviceId },
                    name = "Track My Time",
                    model = "TMT",
                    manufacturer = "Track My Time",
                },
            };

            var topic = $"homeassistant/sensor/{DeviceId}/{objectId}/config";
            await PublishRetainedAsync(topic, JsonSerializer.Serialize(config), cancellationToken);
        }
    }

    private async Task PublishRetainedAsync(string topic, string payload, CancellationToken cancellationToken)
    {
        if (_client is not { IsConnected: true })
        {
            return;
        }

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithRetainFlag(true)
            .Build();

        await _client.PublishAsync(message, cancellationToken);
    }

    private static string Round(decimal hours) => Math.Round(hours, 2).ToString(CultureInfo.InvariantCulture);
}
