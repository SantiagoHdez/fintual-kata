namespace PortfolioRebalancing.Tests;

public class ApplyTests
{
    private static Stock Meta(decimal price = 500m) => new("META", () => price);

    private static Stock Apple(decimal price = 200m) => new("AAPL", () => price);

    private static Portfolio UnbalancedPortfolio()
    {
        var portfolio = new Portfolio();
        portfolio.Buy(Meta(), 10m);
        portfolio.Buy(Apple(), 5m);
        portfolio.SetAllocation(new Dictionary<Stock, decimal> { [Meta()] = 0.4m, [Apple()] = 0.6m });
        return portfolio;
    }

    [Fact]
    public void Apply_ExecutesTheOrders()
    {
        var portfolio = UnbalancedPortfolio();

        portfolio.Apply(portfolio.Rebalance());

        Assert.Equal(4.8m, portfolio.Holdings[Meta()]);
        Assert.Equal(18m, portfolio.Holdings[Apple()]);
    }
}
