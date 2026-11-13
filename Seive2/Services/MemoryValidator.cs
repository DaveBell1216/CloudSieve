using System;

namespace Seive2.Services;

/// <summary>
/// Validates memory requirements for prime sieve operations
/// </summary>
public static class MemoryValidator
{
    // Conservative memory limits
    private const long MAX_SEGMENT_SIZE = 100_000_000; // 100 million per segment
    private const long MAX_BASE_PRIME_LIMIT = 2_000_000_000L; // 2 billion (under int.MaxValue)
    private const long MAX_SAFE_NUMBER = 4_000_000_000_000_000_000L; // 4 * 10^18

    /// <summary>
    /// Validates if the job parameters are within safe memory limits
    /// </summary>
    public static (bool IsValid, string? ErrorMessage) ValidateJobParameters(long maxNumber, long segmentSize)
    {
        // Check for negative or invalid values
        if (maxNumber < 2)
        {
            return (false, "MaxNumber must be at least 2");
        }

        if (segmentSize < 1000)
        {
            return (false, "SegmentSize must be at least 1000");
        }

        // Check segment size limit (must fit in int for array indexing)
        if (segmentSize > MAX_SEGMENT_SIZE)
        {
            return (false, $"SegmentSize cannot exceed {MAX_SEGMENT_SIZE:N0} (100 million) due to memory constraints");
        }

        // Check if maxNumber is within safe range
        if (maxNumber > MAX_SAFE_NUMBER)
        {
            return (false, $"MaxNumber cannot exceed {MAX_SAFE_NUMBER:N0} due to algorithm limitations");
        }

        // Calculate sqrt(maxNumber) for base prime generation
        long sqrtMaxNumber = (long)Math.Sqrt(maxNumber);

        // Check if base prime generation will exceed int.MaxValue
        if (sqrtMaxNumber > MAX_BASE_PRIME_LIMIT)
        {
            return (false, $"MaxNumber is too large: generating base primes would require sqrt({maxNumber:N0}) = {sqrtMaxNumber:N0}, which exceeds the safe limit of {MAX_BASE_PRIME_LIMIT:N0}");
        }

        // Estimate memory usage
        var memoryEstimate = EstimateMemoryUsage(maxNumber, segmentSize);

        // Warn if memory usage is very high (over 4GB for base primes)
        if (memoryEstimate.BasePrimeMemoryMB > 4096)
        {
            return (false, $"Estimated base prime memory usage ({memoryEstimate.BasePrimeMemoryMB:N0} MB) exceeds safe limit of 4GB");
        }

        return (true, null);
    }

    /// <summary>
    /// Estimates memory usage for a given job configuration
    /// </summary>
    public static MemoryEstimate EstimateMemoryUsage(long maxNumber, long segmentSize)
    {
        long sqrtMaxNumber = (long)Math.Sqrt(maxNumber);

        // Estimate base primes memory (BitArray uses 1 bit per element = 1/8 byte)
        long basePrimeArraySize = sqrtMaxNumber + 1;
        long basePrimeMemoryBytes = basePrimeArraySize / 8;

        // Estimate prime list memory (approximately sqrt(n)/ln(sqrt(n)) primes up to sqrt(n))
        // For safety, assume 1 in 10 numbers is prime (overestimate)
        long estimatedPrimeCount = sqrtMaxNumber / 10;
        long primeListMemoryBytes = estimatedPrimeCount * sizeof(long);

        // Segment memory (BitArray)
        long segmentMemoryBytes = segmentSize / 8;

        return new MemoryEstimate
        {
            BasePrimeMemoryMB = (basePrimeMemoryBytes + primeListMemoryBytes) / (1024 * 1024),
            SegmentMemoryMB = segmentMemoryBytes / (1024 * 1024),
            EstimatedPrimeCount = estimatedPrimeCount,
            SqrtMaxNumber = sqrtMaxNumber
        };
    }
}

/// <summary>
/// Memory usage estimate for a sieve job
/// </summary>
public class MemoryEstimate
{
    public long BasePrimeMemoryMB { get; set; }
    public long SegmentMemoryMB { get; set; }
    public long EstimatedPrimeCount { get; set; }
    public long SqrtMaxNumber { get; set; }
}
