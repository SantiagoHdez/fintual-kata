using System.Text.Json;
using System.Text.Json.Serialization;

namespace PortfolioRebalancing.Cli;

/// <summary>Turns the JSON description of a portfolio into the JSON list of orders that rebalances it.</summary>
internal static class RebalanceRunner
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };

    /// <summary>Rebalances the portfolio described by <paramref name="json"/> and returns the orders as JSON.</summary>
    /// <exception cref="InputException">The input is not valid JSON, is incomplete, or cannot be rebalanced.</exception>
    public static string Run(string json)
    {
        try
        {
            var input = JsonSerializer.Deserialize<RebalanceInput>(json, Options)
                ?? throw new InputException("The input must be a JSON object.");

            return Rebalance(input);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            // The library reports bad data with these exceptions, and their messages already say what is wrong.
            throw new InputException(exception.Message, exception);
        }
    }

    private static string Rebalance(RebalanceInput input)
    {
        var stocks = new Dictionary<string, Stock>(StringComparer.OrdinalIgnoreCase);

        foreach (var (symbol, price) in input.Prices)
        {
            if (!stocks.TryAdd(symbol.Trim(), new Stock(symbol, () => price)))
            {
                throw new InputException($"'{symbol.Trim()}' is listed more than once in prices.");
            }
        }

        var portfolio = new Portfolio();

        foreach (var holding in input.Holdings)
        {
            portfolio.Buy(StockFor(stocks, holding.Symbol), holding.Quantity);
        }

        var weights = new Dictionary<Stock, decimal>();

        foreach (var (symbol, weight) in input.Allocation)
        {
            if (!weights.TryAdd(StockFor(stocks, symbol), weight))
            {
                throw new InputException($"'{symbol.Trim()}' is listed more than once in allocation.");
            }
        }

        portfolio.SetAllocation(weights);

        var orders = portfolio.Rebalance()
            .Select(order => new OrderOutput(order.Stock.Symbol, order.Side.ToString(), WithoutTrailingZeros(order.Quantity)))
            .ToList();

        return JsonSerializer.Serialize(new RebalanceOutput(orders), Options);
    }

    private static Stock StockFor(Dictionary<string, Stock> stocks, string symbol) =>
        stocks.TryGetValue(symbol.Trim(), out var stock)
            ? stock
            : throw new InputException($"No price was given for '{symbol.Trim()}'.");

    // Dividing by 1 with the maximum scale drops trailing zeros, so 13.0 is written as 13.
    private static decimal WithoutTrailingZeros(decimal value) => value / 1.0000000000000000000000000000m;

    private sealed record RebalanceInput
    {
        public required List<HoldingInput> Holdings { get; init; }

        public required Dictionary<string, decimal> Prices { get; init; }

        public required Dictionary<string, decimal> Allocation { get; init; }
    }

    private sealed record HoldingInput(string Symbol, decimal Quantity);

    private sealed record RebalanceOutput(List<OrderOutput> Orders);

    private sealed record OrderOutput(string Symbol, string Side, decimal Quantity);
}
