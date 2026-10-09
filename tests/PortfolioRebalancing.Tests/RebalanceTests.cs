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
}
