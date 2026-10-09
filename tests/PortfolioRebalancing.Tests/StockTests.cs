namespace PortfolioRebalancing.Tests;

public class StockTests
{
    [Fact]
    public void CurrentPrice_ReturnsPriceFromSource()
    {
        var stock = new Stock("META", () => 500m);

        Assert.Equal("META", stock.Symbol);
        Assert.Equal(500m, stock.CurrentPrice());
    }

    [Theory]
    [InlineData("meta", "META")]
    [InlineData("  aapl ", "AAPL")]
    public void Symbol_IsTrimmedAndUppercased(string input, string expected)
    {
        var stock = new Stock(input, () => 1m);

        Assert.Equal(expected, stock.Symbol);
    }
}
