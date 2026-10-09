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
}
