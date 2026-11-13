using Seive2.OneShot;

const int limit = 10_000;
bool[] composite = new bool[limit + 1];
for (int p = 2; p * p <= limit; p++)
{
    if (!composite[p])
    {
        for (int multiple = p * p; multiple <= limit; multiple += p)
        {
            composite[multiple] = true;
        }
    }
}

ulong largest = 0;
for (int number = 2; number <= limit; number++)
{
    bool expectedPrime = !composite[number];
    ulong? divisor = PrimeSearch.FindDivisor((ulong)number);
    if ((divisor is null) != expectedPrime ||
        (divisor is ulong factor && (factor <= 1 || factor >= (ulong)number || (ulong)number % factor != 0)))
    {
        throw new InvalidOperationException($"Incorrect divisor {divisor} for {number}.");
    }

    if (expectedPrime) largest = (ulong)number;
    if (PrimeSearch.FindLargestPrime((ulong)number) != largest)
    {
        throw new InvalidOperationException($"Incorrect largest prime for upper bound {number}.");
    }
}

var examined = new List<(ulong Candidate, ulong? Divisor)>();
ulong result = PrimeSearch.FindLargestPrime(PrimeSearch.Maximum56BitValue, (candidate, divisor) => examined.Add((candidate, divisor)));
if (result != 72_057_594_037_927_931UL || examined.Count != 3 ||
    examined[0] != (72_057_594_037_927_935UL, 3UL) ||
    examined[1] != (72_057_594_037_927_933UL, 181UL) ||
    examined[2] != (result, null))
{
    throw new InvalidOperationException("The 56-bit search or its candidate evidence is incorrect.");
}

foreach (ulong invalid in new[] { 0UL, 1UL, PrimeSearch.Maximum56BitValue + 1 })
{
    try
    {
        PrimeSearch.FindLargestPrime(invalid);
        throw new InvalidOperationException($"Accepted invalid upper bound {invalid}.");
    }
    catch (ArgumentOutOfRangeException)
    {
    }
}

Console.WriteLine("All one-shot prime search checks passed.");
