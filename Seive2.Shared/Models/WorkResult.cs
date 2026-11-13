namespace Seive2.Shared.Models;

/// <summary>
/// Result from worker after processing a segment
/// </summary>
public class WorkResult
{
    public Guid SegmentId { get; set; }
    public Guid AssignmentId { get; set; }
    public required string WorkerId { get; set; }
    public long PrimesFound { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The largest prime number found in this segment (0 if none found)
    /// </summary>
    public long LargestPrimeFound { get; set; }

    // Future: Add list of actual primes for database storage
    // public List<long>? Primes { get; set; }
}
