namespace LoraGateway.Services.Firmware.RandomLinearCoding;

/// <summary>
///     https://www.geeksforgeeks.org/program-to-check-for-irreducibility-using-eisensteins-irreducibility-criterion/
/// </summary>
public static class IrreduciblePolynomial
{
    /// <summary>
    ///     // Function to check for Eisensteins Irreducubility Criterion
    /// </summary>
    /// <param name="poly"></param>
    /// <param name="n"></param>
    /// <returns></returns>
    public static int CheckIrreducibility(int[] poly, int n)
    {
        // Stores the largest element in A
        var max = -1;

        // Find the maximum element in A
        for (var i = 0; i < n; i++) max = Math.Max(max, poly[i]);

        // Stores all the prime numbers
        var primes = SieveOfEratosthenes(max + 1);

        // Check if any prime satisfies the conditions
        return primes.Any(t => Check(poly, t, n) == 1) ? 1 : 0;
    }

    /// <summary>
    ///     Function to to implement the sieve of eratosthenes
    /// </summary>
    /// <param name="size"></param>
    /// <returns></returns>
    private static IEnumerable<int> SieveOfEratosthenes(int size)
    {
        // Stores the prime numbers
        var isPrime = new bool[size + 1];

        // Initialize the prime numbers
        for (var i = 0; i < size + 1; i++)
            isPrime[i] = true;

        for (var p = 2; p * p <= size; p++)
        {
            // If isPrime[p] is not changed,
            // then it is a prime
            if (isPrime[p] != true) continue;

            // Update all multiples of
            // p as non-prime
            for (var i = p * p; i <= size; i += p) isPrime[i] = false;
        }

        // Stores all prime numbers less
        // than M
        var prime = new List<int>();

        for (var i = 2; i <= size; i++)
            // If the i is the prime numbers
            if (isPrime[i])
                prime.Add(i);

        // Return array having the primes
        return prime;
    }

    /// <summary>
    ///     Function to check whether the three conditions of Eisenstein's Irreducibility criterion for prime P
    /// </summary>
    /// <param name="a"></param>
    /// <param name="p"></param>
    /// <param name="n"></param>
    /// <returns></returns>
    private static int Check(IReadOnlyList<int> a, int p, int n)
    {
        // 1st condition
        if (a[0] % p == 0)
            return 0;

        // 2nd condition
        for (var i = 1; i < n; i++)
            if (a[i] % p != 0)
                return 0;

        // 3rd condition
        return a[n - 1] % (p * p) == 0 ? 0 : 1;
    }
}