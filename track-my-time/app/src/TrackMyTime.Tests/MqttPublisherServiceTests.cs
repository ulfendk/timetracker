using TrackMyTime.Web.Services;

namespace TrackMyTime.Tests;

public class MqttPublisherServiceTests
{
    /// <summary>Regression test for a bug that shipped and crashed the whole host under a real
    /// MQTT broker: the original implementation raced PeriodicTimer.WaitForNextTickAsync against
    /// a refresh signal via Task.WhenAny, which abandoned the timer wait without awaiting it out
    /// whenever the refresh signal won. PeriodicTimer only tolerates one outstanding waiter, so
    /// the next loop iteration's WaitForNextTickAsync call on the same timer threw
    /// InvalidOperationException. This never surfaced locally because it only triggers once
    /// something is actually calling RequestRefresh() (or - as here - the semaphore's initial
    /// permit is consumed immediately) while a broker is connected; it always fires on the very
    /// first iteration in practice, since the semaphore starts pre-signaled. Calling the current
    /// (Task.Delay-based) implementation back-to-back like this must not throw.</summary>
    [Fact]
    public async Task WaitForNextRefreshAsync_SurvivesRapidRefreshRequests()
    {
        using var refreshRequested = new SemaphoreSlim(1, 1);
        var interval = TimeSpan.FromMinutes(5);

        for (var i = 0; i < 20; i++)
        {
            // Mimics RequestRefresh() firing again before the previous wait's delay would ever
            // elapse, so the refresh signal always wins the race - the exact interleaving that
            // broke the PeriodicTimer-based implementation.
            if (refreshRequested.CurrentCount == 0)
            {
                refreshRequested.Release();
            }

            await MqttPublisherService.WaitForNextRefreshAsync(interval, refreshRequested, CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}
