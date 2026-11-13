namespace Seive2.OneShot;

public static class PrimeSearch
{
    public const ulong Maximum56BitValue = (1UL << 56) - 1;

    public static ulong FindLargestPrime(ulong upperBound, Action<ulong, ulong?>? candidateExamined = null)
    {
        if (upperBound < 2 || upperBound > Maximum56BitValue)
        {
            throw new ArgumentOutOfRangeException(nameof(upperBound), "Upper bound must be between 2 and the maximum unsigned 56-bit value.");
        }

        if (upperBound == 2)
        {
            candidateExamined?.Invoke(2, null);
            return 2;
        }

        for (ulong candidate = upperBound | 1; candidate >= 3; candidate -= 2)
        {
            if (candidate > upperBound)
            {
                continue;
            }

            ulong? divisor = FindDivisor(candidate);
            candidateExamined?.Invoke(candidate, divisor);
            if (divisor is null)
            {
                return candidate;
            }
        }

        candidateExamined?.Invoke(2, null);
        return 2;
    }

    public static ulong? FindDivisor(ulong number)
    {
        if (number < 2 || number > Maximum56BitValue)
        {
            throw new ArgumentOutOfRangeException(nameof(number), "Number must be between 2 and the maximum unsigned 56-bit value.");
        }

        if (number % 2 == 0) return number == 2 ? null : 2;
        if (number % 3 == 0) return number == 3 ? null : 3;

        for (ulong divisor = 5; divisor <= number / divisor; divisor += 6)
        {
            if (number % divisor == 0) return divisor;
            ulong nextDivisor = divisor + 2;
            if (nextDivisor <= number / nextDivisor && number % nextDivisor == 0) return nextDivisor;
        }

        return null;
    }
}
