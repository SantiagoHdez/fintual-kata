namespace PortfolioRebalancing.Tests.Scenarios;

/// <summary>Checks that scenarios repeat after every rebalance.</summary>
internal static class PortfolioAssert
{
    // Rounding quantities to four decimals moves a stock's share of the portfolio by far less than this.
    private const decimal WeightTolerance = 0.0001m;

    // Rounding to four decimals can be off by half of the last digit.
    private const decimal HalfRoundingStep = 0.00005m;

    /// <summary>Each stock's share of the total value matches its target weight, and nothing else is held.</summary>
    public static void MatchesAllocation(Portfolio portfolio)
    {
        var total = portfolio.TotalValue();

        foreach (var (stock, weight) in portfolio.Allocation)
        {
            var held = portfolio.Holdings.GetValueOrDefault(stock);
            var share = held * stock.CurrentPrice() / total;

            Assert.InRange(Math.Abs(share - weight), 0m, WeightTolerance);
        }

        Assert.All(portfolio.Holdings.Keys, stock => Assert.True(portfolio.Allocation.ContainsKey(stock)));
    }

    /// <summary>The most that rounding can make buys and sells differ in value: half a step per order.</summary>
    public static decimal MaxRoundingError(IEnumerable<Order> orders) =>
        orders.Sum(order => HalfRoundingStep * order.Stock.CurrentPrice());

    /// <summary>Money spent on buys and money raised by sells agree, within the rounding bound.</summary>
    public static void BuysMatchSells(IReadOnlyList<Order> orders)
    {
        var bought = ValueOf(orders, Side.Buy);
        var sold = ValueOf(orders, Side.Sell);

        Assert.InRange(Math.Abs(bought - sold), 0m, MaxRoundingError(orders));
    }

    /// <summary>The total value did not change, within the rounding bound.</summary>
    public static void ValueIsConserved(decimal before, decimal after, IReadOnlyList<Order> orders) =>
        Assert.InRange(Math.Abs(after - before), 0m, MaxRoundingError(orders));

    private static decimal ValueOf(IEnumerable<Order> orders, Side side) =>
        orders.Where(order => order.Side == side).Sum(order => order.Quantity * order.Stock.CurrentPrice());
}
