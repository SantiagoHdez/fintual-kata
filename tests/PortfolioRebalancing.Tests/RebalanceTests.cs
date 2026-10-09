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
}
