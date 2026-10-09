namespace PortfolioRebalancing.Tests.Scenarios;

/// <summary>Holds stock prices that a test can change to simulate time passing.</summary>
internal sealed class FakeMarket
{
    private readonly Dictionary<string, decimal> _prices = [];

    /// <summary>Lists a stock at a starting price. Its price follows later <see cref="SetPrice"/> calls.</summary>
    public Stock List(string symbol, decimal price)
    {
        _prices[symbol] = price;
        return new Stock(symbol, () => _prices[symbol]);
    }

    public void SetPrice(string symbol, decimal price) => _prices[symbol] = price;
}
