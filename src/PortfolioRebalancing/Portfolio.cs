namespace PortfolioRebalancing;

/// <summary>A set of stock holdings that can be bought and sold.</summary>
public sealed class Portfolio
{
    private readonly Dictionary<Stock, decimal> _holdings = [];

    public Portfolio()
    {
        Holdings = _holdings.AsReadOnly();
    }

    /// <summary>The number of shares held of each stock. Read-only, but reflects later changes.</summary>
    public IReadOnlyDictionary<Stock, decimal> Holdings { get; }

    /// <summary>Adds shares of a stock to the portfolio.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The quantity is not positive.</exception>
    public void Buy(Stock stock, decimal quantity)
    {
        ArgumentNullException.ThrowIfNull(stock);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        _holdings[stock] = _holdings.GetValueOrDefault(stock) + quantity;
    }
}
