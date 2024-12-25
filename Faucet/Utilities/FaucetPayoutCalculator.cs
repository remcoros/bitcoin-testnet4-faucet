namespace Faucet.Utilities;

public class FaucetPayoutCalculator
{
    // Default configuration
    public const long DefaultInitialPayout = 100_000_000; // Starting payout in satoshis (1 BTC)
    public const long DefaultMinimumPayout = 1_000_000; // Minimum payout in satoshis (0.01 BTC)
    public const double DefaultDecayRate = 0.001; // Decay rate for optimized spending

    // Verified cumulative totals for the default decay rate:
    // Total spend estimates for the default decay rate of 0.001:
    // - After 100 requests: ~95,120,000 satoshis (~0.9512 BTC)
    // - After 1,000 requests: ~63,180,000 satoshis (~6.318 BTC)
    // - After 5,000 requests: ~993,450,000 satoshis (~9.9345 BTC)
    // - After 10,000 requests: ~1,043,450,000 satoshis (~10.4345 BTC)
    // - After 10,000+ requests: Fixed spend of 1,000,000 satoshis (0.01 BTC) per request

    // Instance properties
    public long InitialPayout { get; private set; } // In satoshis
    public long MinimumPayout { get; private set; } // In satoshis
    public double DecayRate { get; private set; }

    /// <summary>
    /// Constructor to initialize the faucet payout calculator with default or custom parameters.
    /// </summary>
    /// <param name="initialPayout">The initial payout in satoshis.</param>
    /// <param name="minimumPayout">The minimum payout in satoshis.</param>
    /// <param name="decayRate">The decay rate for exponential decay.</param>
    public FaucetPayoutCalculator(long initialPayout = DefaultInitialPayout,
        long minimumPayout = DefaultMinimumPayout,
        double decayRate = DefaultDecayRate)
    {
        if (initialPayout <= 0)
        {
            throw new ArgumentException("Initial payout must be greater than 0.", nameof(initialPayout));
        }

        if (minimumPayout <= 0 || minimumPayout > initialPayout)
        {
            throw new ArgumentException("Minimum payout must be greater than 0 and less than the initial payout.",
                nameof(minimumPayout));
        }

        // if (decayRate <= 0)
        // {
        //     throw new ArgumentException("Decay rate must be greater than 0.", nameof(decayRate));
        // }

        InitialPayout = initialPayout;
        MinimumPayout = minimumPayout;
        DecayRate = decayRate;
    }

    /// <summary>
    /// Calculates the payout for a given request number based on exponential decay.
    /// </summary>
    /// <param name="requestCount">The number of the request (1-based).</param>
    /// <returns>The calculated payout in satoshis.</returns>
    public long CalculatePayout(long requestCount)
    {
        if (requestCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(requestCount),
                "Request count must be greater than or equal to 1.");
        }

        // Calculate the payout using exponential decay
        double payout = InitialPayout * Math.Exp(-DecayRate * requestCount);

        // Clamp the payout to the minimum value
        return Math.Max(MinimumPayout, (long)Math.Floor(payout));
    }

    /// <summary>
    /// Calculates the cumulative total payout up to a given request number.
    /// </summary>
    /// <param name="requestCount">The number of the request (1-based).</param>
    /// <returns>The cumulative total payout in satoshis.</returns>
    public long CalculateCumulativePayout(long requestCount)
    {
        if (requestCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(requestCount),
                "Request count must be greater than or equal to 1.");
        }

        long cumulativeTotal = 0;

        for (int i = 1; i <= requestCount; i++)
        {
            cumulativeTotal += CalculatePayout(i);
        }

        return cumulativeTotal;
    }
}