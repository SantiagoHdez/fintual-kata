namespace PortfolioRebalancing;

/// <summary>A set of stock holdings that can be bought and sold.</summary>
public sealed class Portfolio
{
    // Order quantities are rounded to this many decimals, with banker's rounding.
    private const int QuantityDecimals = 4;

    private readonly Dictionary<Stock, decimal> _holdings = [];
    private Dictionary<Stock, decimal> _allocation = [];

    public Portfolio()
    {
        Holdings = _holdings.AsReadOnly();
    }

    /// <summary>The number of shares held of each stock. Read-only, but reflects later changes.</summary>
    public IReadOnlyDictionary<Stock, decimal> Holdings { get; }

    /// <summary>The target weight of each stock, as fractions of 1. Empty until an allocation is set.</summary>
    public IReadOnlyDictionary<Stock, decimal> Allocation => _allocation.AsReadOnly();

    /// <summary>Sets the target weight of each stock, for example 0.4 for 40%.</summary>
    /// <exception cref="ArgumentException">The allocation is empty or its weights do not sum to exactly 1.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A weight is not greater than 0 and at most 1.</exception>
    public void SetAllocation(IReadOnlyDictionary<Stock, decimal> allocation)
    {
        ArgumentNullException.ThrowIfNull(allocation);

        if (allocation.Count == 0)
        {
            throw new ArgumentException("The allocation must contain at least one stock.", nameof(allocation));
        }

        foreach (var (stock, weight) in allocation)
        {
            if (weight is <= 0 or > 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(allocation), weight, $"The weight of {stock} must be greater than 0 and at most 1.");
            }
        }

        var total = allocation.Values.Sum();

        if (total != 1)
        {
            throw new ArgumentException($"The weights must sum to exactly 1, but sum to {total}.", nameof(allocation));
        }

        _allocation = new Dictionary<Stock, decimal>(allocation);
    }

    /// <summary>Adds shares of a stock to the portfolio.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The quantity is not positive.</exception>
    public void Buy(Stock stock, decimal quantity)
    {
        ArgumentNullException.ThrowIfNull(stock);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        _holdings[stock] = _holdings.GetValueOrDefault(stock) + quantity;
    }

    /// <summary>Removes shares of a stock from the portfolio.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The quantity is not positive.</exception>
    /// <exception cref="InvalidOperationException">The portfolio holds fewer shares than requested.</exception>
    public void Sell(Stock stock, decimal quantity)
    {
        ArgumentNullException.ThrowIfNull(stock);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        var held = _holdings.GetValueOrDefault(stock);

        if (quantity > held)
        {
            throw new InvalidOperationException($"Cannot sell {quantity} of {stock}: only {held} held.");
        }

        var remaining = held - quantity;

        if (remaining == 0)
        {
            _holdings.Remove(stock);
        }
        else
        {
            _holdings[stock] = remaining;
        }
    }

    /// <summary>The market value of all holdings: shares times current price, summed.</summary>
    /// <remarks>
    /// This is the only place that defines the amount to distribute across the allocation.
    /// If cash is ever added to the portfolio, it would be included here.
    /// </remarks>
    public decimal TotalValue()
    {
        return _holdings.Sum(holding => holding.Value * holding.Key.CurrentPrice());
    }

    /// <summary>
    /// Works out the trades that bring the portfolio to its target allocation, without changing the portfolio.
    /// Sells come first, then buys, each sorted by symbol.
    /// </summary>
    /// <exception cref="InvalidOperationException">No allocation has been set.</exception>
    public IReadOnlyList<Order> Rebalance()
    {
        if (_allocation.Count == 0)
        {
            throw new InvalidOperationException("Set an allocation before rebalancing.");
        }

        var total = TotalValue();

        if (total == 0)
        {
            return [];
        }

        var orders = new List<Order>();

        // A held stock missing from the allocation has a target weight of 0, so it is sold entirely.
        foreach (var stock in _holdings.Keys.Union(_allocation.Keys))
        {
            var weight = _allocation.GetValueOrDefault(stock);
            var shares = total * weight / stock.CurrentPrice() - _holdings.GetValueOrDefault(stock);

            var side = shares < 0 ? Side.Sell : Side.Buy;
            var quantity = Math.Round(Math.Abs(shares), QuantityDecimals, MidpointRounding.ToEven);

            if (side == Side.Sell)
            {
                // Rounding up must never sell more than the shares held.
                quantity = Math.Min(quantity, _holdings.GetValueOrDefault(stock));
            }

            if (quantity == 0)
            {
                continue;
            }

            orders.Add(new Order(stock, side, quantity));
        }

        return orders
            .OrderBy(order => order.Side == Side.Buy)
            .ThenBy(order => order.Stock.Symbol, StringComparer.Ordinal)
            .ToList();
    }
}
