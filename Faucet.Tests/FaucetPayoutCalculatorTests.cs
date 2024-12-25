using Faucet.Utilities;
using NBitcoin;
using Xunit.Abstractions;

namespace Faucet.Tests;

using Xunit;

public class FaucetPayoutCalculatorTests
{
    private readonly ITestOutputHelper _output;

    public FaucetPayoutCalculatorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void DefaultConfiguration_CalculatesCorrectPayouts()
    {
        // Arrange: Create a calculator with default values
        var calculator = new FaucetPayoutCalculator();

        // Act & Assert: Verify payouts for specific requests
        Assert.Equal(99900049, calculator.CalculatePayout(1));
        Assert.Equal(90483741, calculator.CalculatePayout(100));
        Assert.Equal(1000000, calculator.CalculatePayout(10_000));

        // Act & Assert: Verify cumulative payouts
        Assert.Equal(9_511_500_808 /* 95 tBTC */, calculator.CalculateCumulativePayout(100)); // Cumulative for 100 requests
        Assert.Equal(104_345_335_816 /* 1034 tBTC */, calculator.CalculateCumulativePayout(10_000)); // Cumulative for 10,000 requests
    }

    [Fact]
    public void CustomDecayRate_CalculatesCorrectPayouts()
    {
        // Arrange: Create a calculator with a custom decay rate
        var calculator = new FaucetPayoutCalculator(
            initialPayout: 10_000_000, // 0.1 tBTC
            minimumPayout: 1_000_000, // 0.01 tBTC
            decayRate: 0.01 // Custom decay rate
        );

        // Act & Assert: Verify payouts for specific requests
        Assert.Equal(9900498, calculator.CalculatePayout(1)); 
        Assert.Equal(3678794, calculator.CalculatePayout(100)); 
        Assert.Equal(1_000_000, calculator.CalculatePayout(20_000));

        // Act & Assert: Verify cumulative payouts
        Assert.Equal(628_965_173, calculator.CalculateCumulativePayout(100)); // Cumulative for 100 requests
        Assert.Equal(10_665_249_831 /* 106 tBTC */, calculator.CalculateCumulativePayout(10_000)); // Cumulative for 10,000 requests
    }

    [Fact]
    public void FixedPayout_CalculatesCorrectPayouts()
    {
        // Arrange: Create a calculator for fixed payouts
        var calculator = new FaucetPayoutCalculator(
            initialPayout: 1_000_000, // Fixed payout (0.01 tBTC)
            minimumPayout: 1_000_000, // Same as initial payout
            decayRate: 0 // No decay
        );

        // Act & Assert: Verify payouts for specific requests
        Assert.Equal(1_000_000, calculator.CalculatePayout(1)); // Request 1
        Assert.Equal(1_000_000, calculator.CalculatePayout(100)); // Request 100
        Assert.Equal(1_000_000, calculator.CalculatePayout(10_000)); // Request 10,000

        // Act & Assert: Verify cumulative payouts
        Assert.Equal(100_000_000, calculator.CalculateCumulativePayout(100)); // Cumulative for 100 requests
        Assert.Equal(1_000_000_000, calculator.CalculateCumulativePayout(1_000)); // Cumulative for 1,000 requests
        Assert.Equal(10_000_000_000, calculator.CalculateCumulativePayout(10_000)); // Cumulative for 10,000 requests
    }

    [Theory]
    [InlineData(100_000_000, 1_000_000, 0.001)]
    [InlineData(50_000_000, 1_000_000, 0.001)]
    [InlineData(10_000_000, 1_000_000, 0.001)]
    public void Run(long initialPayout, long minimumPayout, double decayRate)
    {
        var calculator = new FaucetPayoutCalculator(
            initialPayout,
            minimumPayout,
            decayRate: decayRate
        );
        
        int[] dataPoints = [1, 50, 100, 200, 500, 1000, 2000, 3000, 4000, 5000, 10000, 20000];
        
        foreach (var dataPoint in dataPoints)
        {
            var payout = calculator.CalculatePayout(dataPoint);
            var totalPayout = calculator.CalculateCumulativePayout(dataPoint);
            _output.WriteLine($"Request {dataPoint}: {payout} sats ({payout / (double)Money.COIN} tBTC), Total: {totalPayout / (double)Money.COIN} tBTC");
        }
    }
}