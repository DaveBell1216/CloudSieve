namespace Seive2.Shared.Models;

/// <summary>
/// Represents a segment of work to be processed by a worker
/// </summary>
public class WorkSegment
{
    public Guid SegmentId { get; set; }
    public Guid AssignmentId { get; set; }
    public long StartNumber { get; set; }
    public long EndNumber { get; set; }
    public DateTime AssignedAt { get; set; }
    public string? WorkerId { get; set; }
    public SegmentStatus Status { get; set; }
}

public enum SegmentStatus
{
    Pending,
    Assigned,
    Completed,
    Failed
}
