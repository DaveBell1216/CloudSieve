namespace Seive2.Shared.Models;

/// <summary>
/// Response containing current job status
/// </summary>
public class JobStatusResponse
{
    public Guid JobId { get; set; }
    public JobStatus Status { get; set; }
    public long MaxNumber { get; set; }
    public long TotalPrimesFound { get; set; }
    public int TotalSegments { get; set; }
    public int CompletedSegments { get; set; }
    public int PendingSegments { get; set; }
    public int AssignedSegments { get; set; }
    public double ProgressPercentage { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? ElapsedTime { get; set; }
}
