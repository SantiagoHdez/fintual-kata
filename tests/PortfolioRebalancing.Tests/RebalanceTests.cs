namespace PortfolioRebalancing.Tests;

public class RebalanceTests
{
    private static Stock Meta(decimal price = 500m) => new("META", () => price);

    private static Stock Apple(decimal price = 200m) => new("AAPL", () => price);

    private static Stock Named(string symbol) => new(symbol, () => 100m);

    private static Dictionary<Stock, decimal> Weights(params (Stock Stock, decimal Weight)[] weights) =>
        weights.ToDictionary(entry => entry.Stock, entry => entry.Weight);

    [Fact]
    public void Rebalance_ThrowsWhenNoAllocationIsSet()
    {
        var portfolio = new Portfolio();
        portfolio.Buy(Meta(), 1m);

        Assert.Throws<InvalidOperationException>(() => portfolio.Rebalance());
    }

    [Fact]
    public void Rebalance_ReturnsNoOrdersForAnEmptyPortfolio()
    {
        var portfolio = new Portfolio();
        portfolio.SetAllocation(Weights((Meta(), 0.4m), (Apple(), 0.6m)));

        Assert.Empty(portfolio.Rebalance());
    }

    [Fact]
    public void Rebalance_SellsAndBuysToReachTheTargetAllocation()
    {
        // 10 META at 500 and 5 AAPL at 200 are worth 6000. A 40/60 split needs
        // 2400 of META (4.8 shares) and 3600 of AAPL (18 shares).
        var portfolio = new Portfolio();
        portfolio.Buy(Meta(), 10m);
        portfolio.Buy(Apple(), 5m);
        portfolio.SetAllocation(Weights((Meta(), 0.4m), (Apple(), 0.6m)));

        var orders = portfolio.Rebalance();

        Assert.Equal(
            [new Order(Meta(), Side.Sell, 5.2m), new Order(Apple(), Side.Buy, 13m)],
            orders);
    }

    [Fact]
    public void Rebalance_ReturnsNoOrdersWhenAlreadyBalanced()
    {
        var portfolio = new Portfolio();
        portfolio.Buy(Meta(), 4.8m);
        portfolio.Buy(Apple(), 18m);
        portfolio.SetAllocation(Weights((Meta(), 0.4m), (Apple(), 0.6m)));

        Assert.Empty(portfolio.Rebalance());
    }

    [Fact]
    public void Rebalance_SellsEntirelyAStockThatIsNotInTheAllocation()
    {
        // 10 GOOG at 100 are worth 1000, so META (500) gets all of it: 2 shares.
        var google = new Stock("GOOG", () => 100m);
        var portfolio = new Portfolio();
        portfolio.Buy(google, 10m);
        portfolio.SetAllocation(Weights((Meta(), 1m)));

        var orders = portfolio.Rebalance();

        Assert.Equal(
            [new Order(google, Side.Sell, 10m), new Order(Meta(), Side.Buy, 2m)],
            orders);
    }

    [Fact]
    public void Rebalance_BuysAStockThatIsInTheAllocationButNotHeld()
    {
        var portfolio = new Portfolio();
        portfolio.Buy(Meta(), 2m);
        portfolio.SetAllocation(Weights((Meta(), 0.5m), (Apple(), 0.5m)));

        var orders = portfolio.Rebalance();

        Assert.Equal(
            [new Order(Meta(), Side.Sell, 1m), new Order(Apple(), Side.Buy, 2.5m)],
            orders);
    }

    [Fact]
    public void Rebalance_ListsSellsBeforeBuysEachSortedBySymbol()
    {
        var portfolio = new Portfolio();
        portfolio.Buy(Named("ZZZ"), 10m);
        portfolio.Buy(Named("MMM"), 10m);
        portfolio.SetAllocation(Weights((Named("CCC"), 0.5m), (Named("AAA"), 0.5m)));

        var orders = portfolio.Rebalance();

        Assert.Equal(
            [
                new Order(Named("MMM"), Side.Sell, 10m),
                new Order(Named("ZZZ"), Side.Sell, 10m),
                new Order(Named("AAA"), Side.Buy, 10m),
                new Order(Named("CCC"), Side.Buy, 10m),
            ],
            orders);
    }

    [Theory]
    [InlineData(3, 16.6667)] // 50 / 3 = 16.66666...
    [InlineData(200000, 0.0002)] // 50 / 200000 = 0.00025, an exact tie that goes to the even digit
    public void Rebalance_RoundsBuyQuantitiesToFourDecimalsWithBankersRounding(double price, double expected)
    {
        var other = new Stock("OTHER", () => (decimal)price);
        var portfolio = new Portfolio();
        portfolio.Buy(Named("HELD"), 1m);
        portfolio.SetAllocation(Weights((Named("HELD"), 0.5m), (other, 0.5m)));

        var buy = portfolio.Rebalance().Single(order => order.Side == Side.Buy);

        Assert.Equal((decimal)expected, buy.Quantity);
    }

    [Fact]
    public void Rebalance_OmitsOrdersThatRoundToZero()
    {
        var expensive = new Stock("EXPENSIVE", () => 10_000_000m);
        var portfolio = new Portfolio();
        portfolio.Buy(Named("HELD"), 1m);
        portfolio.SetAllocation(Weights((Named("HELD"), 0.5m), (expensive, 0.5m)));

        var orders = portfolio.Rebalance();

        Assert.Equal([new Order(Named("HELD"), Side.Sell, 0.5m)], orders);
    }

    [Fact]
    public void Rebalance_NeverSellsMoreThanIsHeld()
    {
        // Rounding 1.00007 to four decimals gives 1.0001, which is more than the shares held.
        var google = new Stock("GOOG", () => 100m);
        var portfolio = new Portfolio();
        portfolio.Buy(google, 1.00007m);
        portfolio.SetAllocation(Weights((Meta(), 1m)));

        var sell = portfolio.Rebalance().Single(order => order.Side == Side.Sell);

        Assert.Equal(1.00007m, sell.Quantity);
    }

    [Fact]
    public void Rebalance_ReadsEachPriceOnlyOnce()
    {
        var calls = new Dictionary<string, int>();
        Stock Counted(string symbol, decimal price) => new(symbol, () =>
        {
            calls[symbol] = calls.GetValueOrDefault(symbol) + 1;
            return price;
        });
        var meta = Counted("META", 500m);
        var apple = Counted("AAPL", 200m);
        var portfolio = new Portfolio();
        portfolio.Buy(meta, 10m);
        portfolio.SetAllocation(Weights((meta, 0.4m), (apple, 0.6m)));

        portfolio.Rebalance();

        Assert.Equal(1, calls["META"]);
        Assert.Equal(1, calls["AAPL"]);
    }

    [Fact]
    public void Rebalance_ThrowsWhenAPriceIsNotPositive()
    {
        var broken = new Stock("BROKEN", () => 0m);
        var portfolio = new Portfolio();
        portfolio.Buy(Meta(), 1m);
        portfolio.SetAllocation(Weights((Meta(), 0.5m), (broken, 0.5m)));

        var exception = Assert.Throws<InvalidOperationException>(() => portfolio.Rebalance());

        Assert.Contains("BROKEN", exception.Message);
    }

    [Fact]
    public void Rebalance_DoesNotChangeThePortfolio()
    {
        var portfolio = new Portfolio();
        portfolio.Buy(Meta(), 10m);
        portfolio.Buy(Apple(), 5m);
        portfolio.SetAllocation(Weights((Meta(), 0.4m), (Apple(), 0.6m)));

        portfolio.Rebalance();

        Assert.Equal(10m, portfolio.Holdings[Meta()]);
        Assert.Equal(5m, portfolio.Holdings[Apple()]);
    }

    [Fact]
    public void Rebalance_BuysCostAboutAsMuchAsSellsRaise()
    {
        // Rounding quantities leaves a tiny gap between what is raised and what is spent.
        var a = new Stock("A", () => 33.33m);
        var b = new Stock("B", () => 77.77m);
        var c = new Stock("C", () => 12.34m);
        var portfolio = new Portfolio();
        portfolio.Buy(a, 7.5m);
        portfolio.Buy(b, 3.25m);
        portfolio.Buy(c, 20m);
        portfolio.SetAllocation(Weights((a, 0.2m), (b, 0.5m), (c, 0.3m)));

        var orders = portfolio.Rebalance();

        decimal Value(Side side) => orders.Where(order => order.Side == side)
            .Sum(order => order.Quantity * order.Stock.CurrentPrice());
        Assert.NotEmpty(orders);
        Assert.InRange(Math.Abs(Value(Side.Buy) - Value(Side.Sell)), 0m, 0.05m);
    }
}
