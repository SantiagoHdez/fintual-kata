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
}
