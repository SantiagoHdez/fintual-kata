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

    [Fact]
    public void Rebalance_ReturnsNoOrdersAfterTheOrdersAreApplied()
    {
        var portfolio = UnbalancedPortfolio();

        portfolio.Apply(portfolio.Rebalance());

        Assert.Empty(portfolio.Rebalance());
    }

    [Fact]
    public void Apply_ChangesNothingWhenAnOrderCannotBeExecuted()
    {
        var portfolio = UnbalancedPortfolio();
        var orders = new[]
        {
            new Order(Meta(), Side.Sell, 1m),
            new Order(Apple(), Side.Sell, 1000m),
        };

        Assert.Throws<InvalidOperationException>(() => portfolio.Apply(orders));

        Assert.Equal(10m, portfolio.Holdings[Meta()]);
        Assert.Equal(5m, portfolio.Holdings[Apple()]);
    }

    [Fact]
    public void Apply_ChangesNothingWhenAnOrderHasANonPositiveQuantity()
    {
        var portfolio = UnbalancedPortfolio();
        var orders = new[]
        {
            new Order(Meta(), Side.Sell, 1m),
            new Order(Apple(), Side.Buy, 0m),
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => portfolio.Apply(orders));

        Assert.Equal(10m, portfolio.Holdings[Meta()]);
    }

    [Fact]
    public void Apply_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new Portfolio().Apply(null!));
    }

    [Fact]
    public void Holdings_ReflectsAppliedOrdersEvenWhenReadBeforehand()
    {
        var portfolio = UnbalancedPortfolio();
        var holdings = portfolio.Holdings;

        portfolio.Apply(portfolio.Rebalance());

        Assert.Equal(4.8m, holdings[Meta()]);
    }
}
