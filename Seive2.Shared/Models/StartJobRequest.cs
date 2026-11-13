namespace Seive2.Shared.Models;

/// <summary>
/// Request to start a new sieve job
/// </summary>
public class StartJobRequest
{
    public long MaxNumber { get; set; }
    public long SegmentSize { get; set; } = 1_000_000; // Default 1 million per segment
}
