using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace TrackMyTime.Web.Services;

public sealed record MqttServiceInfo(string Host, int Port, bool Ssl, string? Username, string? Password);

/// <summary>Talks to the Home Assistant Supervisor API to discover the configured MQTT broker
/// (via `services: ["mqtt:want"]` in config.yaml) without asking you to enter broker details
/// yourself. Supervisor injects SUPERVISOR_TOKEN into every app container automatically; no
/// "hassio_api: true" is needed just for the "/services/*" endpoints this uses.</summary>
public sealed class HomeAssistantSupervisorClient(HttpClient httpClient, ILogger<HomeAssistantSupervisorClient> logger)
{
    public async Task<MqttServiceInfo?> TryGetMqttServiceAsync(CancellationToken cancellationToken = default)
    {
        var token = Environment.GetEnvironmentVariable("SUPERVISOR_TOKEN");
        if (string.IsNullOrEmpty(token))
        {
            logger.LogInformation(
                "SUPERVISOR_TOKEN is not set (not running under Home Assistant Supervisor) - MQTT publishing is disabled.");
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "http://supervisor/services/mqtt");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogInformation("No MQTT broker is configured in Home Assistant - MQTT publishing is disabled.");
                return null;
            }

            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<SupervisorServiceResponse>(cancellationToken);
            var data = payload?.Data;
            if (data is null || string.IsNullOrEmpty(data.Host))
            {
                return null;
            }

            return new MqttServiceInfo(data.Host, data.Port ?? 1883, data.Ssl ?? false, data.Username, data.Password);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not resolve the MQTT service from Home Assistant Supervisor.");
            return null;
        }
    }

    private sealed class SupervisorServiceResponse
    {
        [JsonPropertyName("data")] public SupervisorMqttData? Data { get; set; }
    }

    private sealed class SupervisorMqttData
    {
        [JsonPropertyName("host")] public string? Host { get; set; }
        [JsonPropertyName("port")] public int? Port { get; set; }
        [JsonPropertyName("ssl")] public bool? Ssl { get; set; }
        [JsonPropertyName("username")] public string? Username { get; set; }
        [JsonPropertyName("password")] public string? Password { get; set; }
    }
}
