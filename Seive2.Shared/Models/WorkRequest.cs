namespace Seive2.Shared.Models;

/// <summary>
/// Request from worker to get next work segment(s)
/// </summary>
public class WorkRequest
{
    public required string WorkerId { get; set; }
    public string? WorkerName { get; set; }

    /// <summary>
    /// Number of segments to request (default: 1)
    /// </summary>
    public int BatchSize { get; set; } = 1;
}
