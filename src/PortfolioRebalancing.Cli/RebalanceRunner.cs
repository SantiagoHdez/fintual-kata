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

    public static string Run(string json)
    {
        var input = JsonSerializer.Deserialize<RebalanceInput>(json, Options)!;

        var stocks = input.Prices.ToDictionary(
            price => price.Key.Trim(),
            price => new Stock(price.Key, () => price.Value),
            StringComparer.OrdinalIgnoreCase);

        var portfolio = new Portfolio();

        foreach (var holding in input.Holdings)
        {
            portfolio.Buy(stocks[holding.Symbol.Trim()], holding.Quantity);
        }

        portfolio.SetAllocation(input.Allocation.ToDictionary(
            weight => stocks[weight.Key.Trim()],
            weight => weight.Value));

        var orders = portfolio.Rebalance()
            .Select(order => new OrderOutput(order.Stock.Symbol, order.Side.ToString(), WithoutTrailingZeros(order.Quantity)))
            .ToList();

        return JsonSerializer.Serialize(new RebalanceOutput(orders), Options);
    }

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
