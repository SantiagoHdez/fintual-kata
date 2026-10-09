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
}
