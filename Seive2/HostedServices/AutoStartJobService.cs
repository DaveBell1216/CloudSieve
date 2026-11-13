using Seive2.Services;

namespace Seive2.HostedServices;

/// <summary>
/// Background service that automatically starts a prime sieve job on application startup
/// </summary>
public class AutoStartJobService : IHostedService
{
    private readonly SqlWorkCoordinator _coordinator;
    private readonly ILogger<AutoStartJobService> _logger;

    public AutoStartJobService(SqlWorkCoordinator coordinator, ILogger<AutoStartJobService> logger)
    {
        _coordinator = coordinator;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        const long maxNumber = 4_000_000_000_000;
        const long segmentSize = 100_000;

        try
        {
            _logger.LogInformation("Auto-starting sieve job on application startup");

            const long startNumber = 2;
            _logger.LogInformation("Starting the configured auto-start job from {StartNumber:N0}", startNumber);
            _logger.LogInformation("Max Number: {MaxNumber:N0}", maxNumber);
            _logger.LogInformation("Segment Size: {SegmentSize:N0}", segmentSize);

            var job = await _coordinator.StartJobAsync(startNumber, maxNumber, segmentSize, cancellationToken);

            _logger.LogInformation("✅ Job auto-started successfully!");
            _logger.LogInformation("Job ID: {JobId}", job.JobId);
            _logger.LogInformation("Total Segments: {TotalSegments:N0}", job.TotalSegments);
            _logger.LogInformation("Ready for workers to connect and start processing");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-start job on application startup");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Auto-start job service stopping");
        return Task.CompletedTask;
    }
}
