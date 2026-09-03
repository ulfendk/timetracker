using System.Globalization;
using System.Text.Json;
using MQTTnet;
using TrackMyTime.Web.Models;
using TrackMyTime.Web.Repositories;

namespace TrackMyTime.Web.Services;

/// <summary>Publishes today/week/month actual-vs-nominal figures to Home Assistant via MQTT
/// Discovery, so they show up as ordinary sensor entities in any dashboard. Runs entirely
/// best-effort: with no broker configured, or while disconnected, the app keeps working — this
/// service just skips publishing and logs why. Connects with retry/backoff at startup (a broker
/// not yet up when this app starts is normal, not fatal) and reconnects the same way after any
/// mid-session drop.</summary>
public sealed class MqttPublisherService(
    IServiceScopeFactory scopeFactory,
    HomeAssistantSupervisorClient supervisorClient,
    ILogger<MqttPublisherService> logger) : BackgroundService
{
    private const string DeviceId = "trackmytime";
    private const string StatusTopic = "trackmytime/status";
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan InitialReconnectDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromSeconds(60);

    private readonly SemaphoreSlim _refreshRequested = new(1, 1);
    private readonly SemaphoreSlim _disconnected = new(0, 1);

    // MQTTnet's IMqttClient docs prohibit calling ConnectAsync/DisconnectAsync concurrently with
    // publish operations. Once reconnects can happen on their own loop, concurrently with the
    // publish loop, that's a real hazard - this serializes the two operation types against each
    // other (publish-vs-publish concurrency isn't a concern: PublishStateAsync already awaits its
    // publishes one at a time).
    private readonly SemaphoreSlim _clientLock = new(1, 1);

    private IMqttClient? _client;

    /// <summary>For the Settings page's status indicator. Reads the client's own live connection
    /// state, so it correctly reads false for the whole span of any retry/backoff attempt.</summary>
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
        _client.DisconnectedAsync += OnDisconnectedAsync;

        var options = BuildClientOptions(mqttService.Host, mqttService.Port, mqttService.Ssl, mqttService.Username, mqttService.Password);

        await ConnectWithRetryAsync(options, mqttService.Host, mqttService.Port, stoppingToken);

        await Task.WhenAll(
            PublishLoopAsync(stoppingToken),
            ReconnectLoopAsync(options, mqttService.Host, mqttService.Port, stoppingToken));
    }

    private static MqttClientOptions BuildClientOptions(string host, int port, bool ssl, string? username, string? password)
    {
        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithClientId("track-my-time")
            .WithTcpServer(host, port)
            .WithWillTopic(StatusTopic)
            .WithWillPayload("offline")
            .WithWillRetain(true);

        if (ssl)
        {
            optionsBuilder = optionsBuilder.WithTlsOptions(o => o.UseTls());
        }
        if (!string.IsNullOrEmpty(username))
        {
            optionsBuilder = optionsBuilder.WithCredentials(username, password);
        }

        return optionsBuilder.Build();
    }

    /// <summary>One connect attempt. On success, (re)publishes discovery configs and "online"
    /// status - safe to repeat on every reconnect, since a retained publish with identical
    /// payload is a no-op from Home Assistant's perspective, and this is what re-announces
    /// entities if the broker's retained-message store was lost (restart, different instance).</summary>
    private async Task<bool> TryConnectAsync(MqttClientOptions options, string host, int port, CancellationToken cancellationToken)
    {
        try
        {
            await _clientLock.WaitAsync(cancellationToken);
            try
            {
                await _client!.ConnectAsync(options, cancellationToken);
            }
            finally
            {
                _clientLock.Release();
            }

            await PublishDiscoveryConfigAsync(cancellationToken);
            await PublishRetainedAsync(StatusTopic, "online", cancellationToken);
            logger.LogInformation("Connected to MQTT broker at {Host}:{Port} for Home Assistant discovery", host, port);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not connect to the MQTT broker at {Host}:{Port}; will retry", host, port);
            return false;
        }
    }

    /// <summary>Retries TryConnectAsync with exponential backoff until it succeeds or the
    /// service is stopped. Used for both the initial connect and every reconnect after a
    /// mid-session drop, so a broker that isn't up yet when this app starts - or goes away
    /// temporarily later - is retried rather than given up on permanently.</summary>
    private async Task ConnectWithRetryAsync(MqttClientOptions options, string host, int port, CancellationToken cancellationToken)
    {
        var delay = InitialReconnectDelay;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (await TryConnectAsync(options, host, port, cancellationToken))
            {
                return;
            }

            await Task.Delay(delay, cancellationToken);
            delay = NextReconnectDelay(delay, MaxReconnectDelay);
        }
    }

    /// <summary>Computes the next reconnect delay by doubling, capped at <paramref name="maxDelay"/>.
    /// Pulled out as its own pure, testable method for the same reason WaitForNextRefreshAsync is:
    /// so the retry/backoff policy can be verified without a real MQTT broker.</summary>
    internal static TimeSpan NextReconnectDelay(TimeSpan currentDelay, TimeSpan maxDelay)
    {
        var doubled = currentDelay * 2;
        return doubled < maxDelay ? doubled : maxDelay;
    }

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        // ClientWasConnected is false for a failed *initial* connect attempt (MQTTnet raises
        // DisconnectedAsync there too) - that case is already handled by ConnectWithRetryAsync's
        // own retry loop. Only a drop after a successful connect should wake the reconnect loop.
        if (args.ClientWasConnected && _disconnected.CurrentCount == 0)
        {
            _disconnected.Release();
        }
        return Task.CompletedTask;
    }

    /// <summary>Waits for a mid-session disconnect, then reconnects with the same retry/backoff
    /// as the initial connect. A plain sequential await, not raced via Task.WhenAny against
    /// anything with a "single outstanding wait" restriction - see WaitForNextRefreshAsync's doc
    /// comment for the bug class that would reintroduce.</summary>
    private async Task ReconnectLoopAsync(MqttClientOptions options, string host, int port, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _disconnected.WaitAsync(stoppingToken);
            logger.LogWarning("Lost connection to the MQTT broker at {Host}:{Port}; attempting to reconnect", host, port);
            await ConnectWithRetryAsync(options, host, port, stoppingToken);
        }
    }

    private async Task PublishLoopAsync(CancellationToken stoppingToken)
    {
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
        var dayOffRepository = scope.ServiceProvider.GetRequiredService<IDayOffRepository>();

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

        var yearStart = new DateOnly(today.Year, 1, 1);
        var yearEnd = new DateOnly(today.Year, 12, 31);
        var sickDays = await dayOffRepository.CountByTypeAndDateRangeAsync(DayOffType.Sickness, yearStart, yearEnd);
        var vacationDays = await dayOffRepository.CountByTypeAndDateRangeAsync(DayOffType.Vacation, yearStart, yearEnd);
        await PublishRetainedAsync("trackmytime/sensor/sick_days_ytd/state", sickDays.ToString(CultureInfo.InvariantCulture), cancellationToken);
        await PublishRetainedAsync("trackmytime/sensor/vacation_days_ytd/state", vacationDays.ToString(CultureInfo.InvariantCulture), cancellationToken);
    }

    private async Task PublishDiscoveryConfigAsync(CancellationToken cancellationToken)
    {
        (string ObjectId, string Name, string Icon, string Unit)[] sensors =
        [
            ("today_actual_hours", "TMT Today Actual Hours", "mdi:clock-check-outline", "h"),
            ("week_actual_hours", "TMT Week Actual Hours", "mdi:calendar-week", "h"),
            ("week_nominal_hours", "TMT Week Nominal Hours", "mdi:calendar-week-outline", "h"),
            ("week_delta_hours", "TMT Week Delta Hours", "mdi:scale-balance", "h"),
            ("month_actual_hours", "TMT Month Actual Hours", "mdi:calendar-month", "h"),
            ("month_nominal_hours", "TMT Month Nominal Hours", "mdi:calendar-month-outline", "h"),
            ("month_delta_hours", "TMT Month Delta Hours", "mdi:scale-balance", "h"),
            ("sick_days_ytd", "TMT Sick Days (YTD)", "mdi:emoticon-sick-outline", "d"),
            ("vacation_days_ytd", "TMT Vacation Days (YTD)", "mdi:beach", "d"),
        ];

        foreach (var (objectId, name, icon, unit) in sensors)
        {
            var config = new
            {
                name,
                unique_id = $"{DeviceId}_{objectId}",
                state_topic = $"trackmytime/sensor/{objectId}/state",
                availability_topic = StatusTopic,
                unit_of_measurement = unit,
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

        await _clientLock.WaitAsync(cancellationToken);
        try
        {
            await _client.PublishAsync(message, cancellationToken);
        }
        finally
        {
            _clientLock.Release();
        }
    }

    private static string Round(decimal hours) => Math.Round(hours, 2).ToString(CultureInfo.InvariantCulture);
}
