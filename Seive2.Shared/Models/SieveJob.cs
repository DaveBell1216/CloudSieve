namespace Seive2.Shared.Models;

/// <summary>
/// Represents a prime sieve job configuration
/// </summary>
public class SieveJob
{
    public Guid JobId { get; set; }
    public long MaxNumber { get; set; }
    public long SegmentSize { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public JobStatus Status { get; set; }
    public long TotalPrimesFound { get; set; }
    public int TotalSegments { get; set; }
    public int CompletedSegments { get; set; }
}

public enum JobStatus
{
    NotStarted,
    Running,
    Completed,
    Failed
}
