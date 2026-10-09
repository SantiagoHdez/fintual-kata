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

    [Fact]
    public void Constructor_RejectsNullSymbol()
    {
        Assert.Throws<ArgumentNullException>(() => new Stock(null!, () => 1m));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsBlankSymbol(string symbol)
    {
        Assert.Throws<ArgumentException>(() => new Stock(symbol, () => 1m));
    }

    [Fact]
    public void Constructor_RejectsNullPriceSource()
    {
        Assert.Throws<ArgumentNullException>(() => new Stock("META", null!));
    }

    [Fact]
    public void Equals_ComparesBySymbolIgnoringPrice()
    {
        var first = new Stock("META", () => 500m);
        var second = new Stock(" meta", () => 999m);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, new Stock("AAPL", () => 500m));
    }

    [Fact]
    public void Stock_WorksAsDictionaryKey()
    {
        var holdings = new Dictionary<Stock, decimal> { [new Stock("META", () => 500m)] = 2m };

        Assert.Equal(2m, holdings[new Stock("meta", () => 1m)]);
    }
}
