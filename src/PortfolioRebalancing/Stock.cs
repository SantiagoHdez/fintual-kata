namespace PortfolioRebalancing;

/// <summary>A tradable stock that can report its last available price.</summary>
public sealed class Stock : IEquatable<Stock>
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
    /// <exception cref="InvalidOperationException">The price source returned a price that is not positive.</exception>
    public decimal CurrentPrice()
    {
        var price = _priceSource();

        if (price <= 0)
        {
            throw new InvalidOperationException($"The price of {Symbol} must be positive, but was {price}.");
        }

        return price;
    }

    /// <summary>Two stocks are equal when they have the same symbol, whatever their price source.</summary>
    public bool Equals(Stock? other) => other is not null && Symbol == other.Symbol;

    public override bool Equals(object? obj) => Equals(obj as Stock);

    public override int GetHashCode() => Symbol.GetHashCode();

    public override string ToString() => Symbol;
}
