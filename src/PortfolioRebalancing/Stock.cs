namespace PortfolioRebalancing;

/// <summary>A tradable stock that can report its last available price.</summary>
public sealed class Stock
{
    private readonly Func<decimal> _priceSource;

    /// <param name="symbol">The ticker symbol, for example "META".</param>
    /// <param name="priceSource">Returns the last available price each time it is called.</param>
    public Stock(string symbol, Func<decimal> priceSource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentNullException.ThrowIfNull(priceSource);

        Symbol = symbol.Trim().ToUpperInvariant();
        _priceSource = priceSource;
    }

    public string Symbol { get; }

    /// <summary>Returns the last available price.</summary>
    public decimal CurrentPrice() => _priceSource();
}
