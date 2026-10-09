namespace PortfolioRebalancing.Tests;

public class PortfolioTests
{
    private static Stock Meta(decimal price = 500m) => new("META", () => price);

    private static Stock Apple(decimal price = 200m) => new("AAPL", () => price);

    [Fact]
    public void Holdings_StartEmpty()
    {
        var portfolio = new Portfolio();

        Assert.Empty(portfolio.Holdings);
    }

    [Fact]
    public void Buy_AddsSharesToHoldings()
    {
        var portfolio = new Portfolio();

        portfolio.Buy(Meta(), 2.5m);

        Assert.Equal(2.5m, portfolio.Holdings[Meta()]);
    }

    [Fact]
    public void Buy_AccumulatesSharesOfTheSameStock()
    {
        var portfolio = new Portfolio();

        portfolio.Buy(Meta(), 1m);
        portfolio.Buy(Meta(), 2m);

        Assert.Equal(3m, portfolio.Holdings[Meta()]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Buy_RejectsQuantityThatIsNotPositive(double quantity)
    {
        var portfolio = new Portfolio();

        Assert.Throws<ArgumentOutOfRangeException>(() => portfolio.Buy(Meta(), (decimal)quantity));
        Assert.Empty(portfolio.Holdings);
    }

    [Fact]
    public void Buy_RejectsNullStock()
    {
        var portfolio = new Portfolio();

        Assert.Throws<ArgumentNullException>(() => portfolio.Buy(null!, 1m));
    }

    [Fact]
    public void Sell_ReducesTheSharesHeld()
    {
        var portfolio = new Portfolio();
        portfolio.Buy(Meta(), 5m);

        portfolio.Sell(Meta(), 2m);

        Assert.Equal(3m, portfolio.Holdings[Meta()]);
    }

    [Fact]
    public void Sell_RemovesTheStockWhenThePositionIsSoldEntirely()
    {
        var portfolio = new Portfolio();
        portfolio.Buy(Meta(), 5m);

        portfolio.Sell(Meta(), 5m);

        Assert.Empty(portfolio.Holdings);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Sell_RejectsQuantityThatIsNotPositive(double quantity)
    {
        var portfolio = new Portfolio();
        portfolio.Buy(Meta(), 5m);

        Assert.Throws<ArgumentOutOfRangeException>(() => portfolio.Sell(Meta(), (decimal)quantity));
        Assert.Equal(5m, portfolio.Holdings[Meta()]);
    }

    [Fact]
    public void Sell_RejectsNullStock()
    {
        var portfolio = new Portfolio();

        Assert.Throws<ArgumentNullException>(() => portfolio.Sell(null!, 1m));
    }

    [Fact]
    public void Sell_RejectsAStockThatIsNotHeld()
    {
        var portfolio = new Portfolio();
        portfolio.Buy(Meta(), 5m);

        Assert.Throws<InvalidOperationException>(() => portfolio.Sell(Apple(), 1m));
    }

    [Fact]
    public void Sell_RejectsMoreSharesThanHeld()
    {
        var portfolio = new Portfolio();
        portfolio.Buy(Meta(), 5m);

        Assert.Throws<InvalidOperationException>(() => portfolio.Sell(Meta(), 5.0001m));
        Assert.Equal(5m, portfolio.Holdings[Meta()]);
    }

    [Fact]
    public void TotalValue_IsZeroForAnEmptyPortfolio()
    {
        Assert.Equal(0m, new Portfolio().TotalValue());
    }

    [Fact]
    public void TotalValue_SumsQuantityTimesCurrentPrice()
    {
        var portfolio = new Portfolio();
        portfolio.Buy(Meta(500m), 2m);
        portfolio.Buy(Apple(200m), 3.5m);

        Assert.Equal(1700m, portfolio.TotalValue());
    }

    [Fact]
    public void TotalValue_ReadsEachPriceOnce()
    {
        var calls = 0;
        var meta = new Stock("META", () =>
        {
            calls++;
            return 500m;
        });
        var portfolio = new Portfolio();
        portfolio.Buy(meta, 2m);

        portfolio.TotalValue();

        Assert.Equal(1, calls);
    }

    [Fact]
    public void Allocation_StartsEmpty()
    {
        Assert.Empty(new Portfolio().Allocation);
    }

    [Fact]
    public void SetAllocation_StoresTheTargetWeights()
    {
        var portfolio = new Portfolio();

        portfolio.SetAllocation(new Dictionary<Stock, decimal> { [Meta()] = 0.4m, [Apple()] = 0.6m });

        Assert.Equal(0.4m, portfolio.Allocation[Meta()]);
        Assert.Equal(0.6m, portfolio.Allocation[Apple()]);
    }

    [Fact]
    public void SetAllocation_IsNotAffectedByLaterChangesToTheCallersDictionary()
    {
        var portfolio = new Portfolio();
        var weights = new Dictionary<Stock, decimal> { [Meta()] = 0.4m, [Apple()] = 0.6m };
        portfolio.SetAllocation(weights);

        weights[Meta()] = 0.9m;

        Assert.Equal(0.4m, portfolio.Allocation[Meta()]);
    }

    [Fact]
    public void SetAllocation_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new Portfolio().SetAllocation(null!));
    }

    [Fact]
    public void SetAllocation_RejectsAnEmptyAllocation()
    {
        Assert.Throws<ArgumentException>(() => new Portfolio().SetAllocation(new Dictionary<Stock, decimal>()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    [InlineData(1.5)]
    public void SetAllocation_RejectsAWeightOutsideZeroExclusiveToOneInclusive(double weight)
    {
        var allocation = new Dictionary<Stock, decimal> { [Meta()] = (decimal)weight, [Apple()] = 1m };

        Assert.Throws<ArgumentOutOfRangeException>(() => new Portfolio().SetAllocation(allocation));
    }

    [Theory]
    [InlineData(0.4, 0.5)]
    [InlineData(0.5, 0.6)]
    public void SetAllocation_RejectsWeightsThatDoNotSumToOne(double meta, double apple)
    {
        var allocation = new Dictionary<Stock, decimal> { [Meta()] = (decimal)meta, [Apple()] = (decimal)apple };

        Assert.Throws<ArgumentException>(() => new Portfolio().SetAllocation(allocation));
    }

    [Fact]
    public void SetAllocation_AcceptsASingleStockWithFullWeight()
    {
        var portfolio = new Portfolio();

        portfolio.SetAllocation(new Dictionary<Stock, decimal> { [Meta()] = 1m });

        Assert.Equal(1m, portfolio.Allocation[Meta()]);
    }

    [Fact]
    public void SetAllocation_KeepsThePreviousAllocationWhenTheNewOneIsInvalid()
    {
        var portfolio = new Portfolio();
        portfolio.SetAllocation(new Dictionary<Stock, decimal> { [Meta()] = 0.4m, [Apple()] = 0.6m });

        Assert.Throws<ArgumentException>(() =>
            portfolio.SetAllocation(new Dictionary<Stock, decimal> { [Meta()] = 0.5m, [Apple()] = 0.6m }));

        Assert.Equal(0.4m, portfolio.Allocation[Meta()]);
    }

    [Fact]
    public void SetAllocation_RequiresAThreeWaySplitToSumExactlyToOne()
    {
        var google = new Stock("GOOG", () => 150m);
        var rounded = new Dictionary<Stock, decimal> { [Meta()] = 0.3333m, [Apple()] = 0.3333m, [google] = 0.3333m };
        var exact = new Dictionary<Stock, decimal> { [Meta()] = 0.3333m, [Apple()] = 0.3333m, [google] = 0.3334m };
        var portfolio = new Portfolio();

        Assert.Throws<ArgumentException>(() => portfolio.SetAllocation(rounded));

        portfolio.SetAllocation(exact);
        Assert.Equal(0.3334m, portfolio.Allocation[google]);
    }
}
