using Seive2.OneShot;

Console.WriteLine($"Searching for the largest prime at or below {PrimeSearch.Maximum56BitValue}.");
ulong largestPrime = PrimeSearch.FindLargestPrime(PrimeSearch.Maximum56BitValue, (candidate, divisor) =>
{
    Console.WriteLine(divisor.HasValue
        ? $"{candidate} is composite; divisible by {divisor.Value}."
        : $"{candidate} is prime; no divisor exists up to its square root.");
});
Console.WriteLine($"Largest unsigned 56-bit prime: {largestPrime}");
