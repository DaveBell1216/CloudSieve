using System.Collections;

namespace Seive2.Worker;

/// <summary>
/// Implements segmented Sieve of Eratosthenes for distributed prime computation
/// </summary>
public class PrimeSieve
{
    /// <summary>
    /// Result of prime computation including count and largest prime
    /// </summary>
    public record PrimeResult(long Count, long LargestPrime);

    /// <summary>
    /// Find all primes in the given range using segmented sieve
    /// </summary>
    public static long CountPrimesInRange(long start, long end)
    {
        return FindPrimesInRange(start, end).Count;
    }

    /// <summary>
    /// Find all primes in the given range and return count + largest prime
    /// </summary>
    public static PrimeResult FindPrimesInRange(long start, long end)
    {
        if (start > end) return new PrimeResult(0, 0);
        if (end < 2) return new PrimeResult(0, 0);

        // Adjust start to at least 2
        start = Math.Max(2, start);

        // For small ranges, use simple sieve
        if (end <= 10_000_000)
        {
            return SimpleCountPrimesWithLargest(start, end);
        }

        // For larger ranges, use segmented sieve
        return SegmentedCountPrimesWithLargest(start, end);
    }

    /// <summary>
    /// Simple sieve for smaller ranges using BitArray for memory efficiency
    /// </summary>
    private static long SimpleCountPrimes(long start, long end)
    {
        return SimpleCountPrimesWithLargest(start, end).Count;
    }

    /// <summary>
    /// Simple sieve with largest prime tracking
    /// </summary>
    private static PrimeResult SimpleCountPrimesWithLargest(long start, long end)
    {
        int size = (int)(end - start + 1);

        // Use BitArray instead of bool[] - saves 8x memory (1 bit vs 8 bits per element)
        var isPrime = new BitArray(size, true);

        // Sieve
        long sqrtEnd = (long)Math.Sqrt(end);
        for (long p = 2; p <= sqrtEnd; p++)
        {
            if (IsPrime(p))
            {
                // Find first multiple >= start
                long firstMultiple = ((start + p - 1) / p) * p;
                if (firstMultiple == p) firstMultiple += p; // Don't mark p itself if in range

                for (long i = firstMultiple; i <= end; i += p)
                {
                    isPrime[(int)(i - start)] = false;
                }
            }
        }

        // Count primes and track largest
        long count = 0;
        long largestPrime = 0;
        for (int i = 0; i < size; i++)
        {
            if (isPrime[i])
            {
                long num = start + i;
                if (num >= 2)
                {
                    count++;
                    largestPrime = num; // Update largest as we go (scanning forward)
                }
            }
        }

        return new PrimeResult(count, largestPrime);
    }

    /// <summary>
    /// Segmented sieve for larger ranges
    /// </summary>
    private static long SegmentedCountPrimes(long start, long end)
    {
        return SegmentedCountPrimesWithLargest(start, end).Count;
    }

    /// <summary>
    /// Segmented sieve with largest prime tracking
    /// </summary>
    private static PrimeResult SegmentedCountPrimesWithLargest(long start, long end)
    {
        long sqrtEnd = (long)Math.Sqrt(end);

        // First, find all primes up to sqrt(end) using simple sieve
        List<long> basePrimes = GenerateBasePrimes(sqrtEnd);

        long count = 0;
        long largestPrime = 0;
        int segmentSize = 1_000_000; // Process in 1M chunks

        // Process each segment
        for (long low = start; low <= end; low += segmentSize)
        {
            long high = Math.Min(low + segmentSize - 1, end);
            var segmentResult = CountPrimesInSegmentWithLargest(low, high, basePrimes);
            count += segmentResult.Count;
            if (segmentResult.LargestPrime > largestPrime)
            {
                largestPrime = segmentResult.LargestPrime;
            }
        }

        return new PrimeResult(count, largestPrime);
    }

    /// <summary>
    /// Count primes in a single segment using base primes and BitArray
    /// </summary>
    private static long CountPrimesInSegment(long low, long high, List<long> basePrimes)
    {
        return CountPrimesInSegmentWithLargest(low, high, basePrimes).Count;
    }

    /// <summary>
    /// Count primes in a single segment with largest prime tracking
    /// </summary>
    private static PrimeResult CountPrimesInSegmentWithLargest(long low, long high, List<long> basePrimes)
    {
        int size = (int)(high - low + 1);

        // Use BitArray for 8x memory savings
        var isPrime = new BitArray(size, true);

        // Sieve this segment with base primes
        foreach (long p in basePrimes)
        {
            // Find first multiple of p >= low
            long firstMultiple = ((low + p - 1) / p) * p;

            // Don't mark p itself if it's in this segment
            if (firstMultiple == p && firstMultiple >= low)
            {
                firstMultiple += p;
            }

            for (long i = firstMultiple; i <= high; i += p)
            {
                isPrime[(int)(i - low)] = false;
            }
        }

        // Count primes in this segment and track largest
        long count = 0;
        long largestPrime = 0;
        for (int i = 0; i < size; i++)
        {
            if (isPrime[i])
            {
                long num = low + i;
                if (num >= 2)
                {
                    count++;
                    largestPrime = num;
                }
            }
        }

        return new PrimeResult(count, largestPrime);
    }

    /// <summary>
    /// Generate base primes up to limit using BitArray sieve (memory efficient)
    /// Supports limits up to 2 billion safely
    /// </summary>
    private static List<long> GenerateBasePrimes(long limit)
    {
        if (limit < 2) return new List<long>();

        // Guard against limits that are too large
        if (limit > int.MaxValue)
        {
            throw new ArgumentException($"Base prime limit {limit:N0} exceeds maximum safe value of {int.MaxValue:N0}");
        }

        int size = (int)limit + 1;

        // Use BitArray - 8x more memory efficient than bool[]
        var isPrime = new BitArray(size, true);
        isPrime[0] = isPrime[1] = false;

        // Sieve of Eratosthenes
        int sqrtLimit = (int)Math.Sqrt(limit);
        for (int p = 2; p <= sqrtLimit; p++)
        {
            if (isPrime[p])
            {
                // Mark multiples as composite
                for (long i = (long)p * p; i <= limit; i += p)
                {
                    isPrime[(int)i] = false;
                }
            }
        }

        // Collect primes
        var primes = new List<long>();
        for (int i = 2; i < size; i++)
        {
            if (isPrime[i]) primes.Add(i);
        }

        return primes;
    }

    /// <summary>
    /// Simple primality test for small numbers
    /// </summary>
    private static bool IsPrime(long n)
    {
        if (n < 2) return false;
        if (n == 2) return true;
        if (n % 2 == 0) return false;

        long sqrt = (long)Math.Sqrt(n);
        for (long i = 3; i <= sqrt; i += 2)
        {
            if (n % i == 0) return false;
        }

        return true;
    }
}
