namespace PortfolioRebalancing.Tests;

public class RebalanceTests
{
    private static Stock Meta(decimal price = 500m) => new("META", () => price);

    private static Stock Apple(decimal price = 200m) => new("AAPL", () => price);

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
}
