using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PortfolioRebalancing.Cli;

/// <summary>Turns the JSON description of a portfolio into the JSON list of orders that rebalances it.</summary>
internal static partial class RebalanceRunner
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
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
        catch (JsonException exception)
        {
            throw new InputException(Describe(exception), exception);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            // The library reports bad data with these exceptions, and their messages already say what is wrong.
            throw new InputException(WithoutDetails(exception.Message), exception);
        }
    }

    private static string Rebalance(RebalanceInput input)
    {
        var stocks = new Dictionary<string, Stock>(StringComparer.OrdinalIgnoreCase);

        foreach (var (symbol, price) in input.Prices)
        {
            RequireSymbol(symbol);

            if (!stocks.TryAdd(symbol.Trim(), new Stock(symbol, () => price)))
            {
                throw new InputException($"'{symbol.Trim()}' is listed more than once in prices.");
            }
        }

        var portfolio = new Portfolio();

        foreach (var holding in input.Holdings)
        {
            if (holding.Quantity <= 0)
            {
                throw new InputException(
                    $"The quantity of '{holding.Symbol.Trim()}' in holdings must be positive, but was {holding.Quantity}.");
            }

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

    private static Stock StockFor(Dictionary<string, Stock> stocks, string symbol)
    {
        RequireSymbol(symbol);

        return stocks.TryGetValue(symbol.Trim(), out var stock)
            ? stock
            : throw new InputException($"No price was given for '{symbol.Trim()}'.");
    }

    private static void RequireSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new InputException("A symbol is empty.");
        }
    }

    // The serializer's messages mention internal type names, so the common problems are reworded here.
    private static string Describe(JsonException exception)
    {
        var location = exception.Path is null or "$" ? string.Empty : $" at {exception.Path}";

        if (UnknownProperty().Match(exception.Message) is { Success: true } unknown)
        {
            // The path ends with the unknown property itself, so point at the object that contains it.
            var name = unknown.Groups[1].Value;
            var parent = exception.Path?.EndsWith("." + name, StringComparison.Ordinal) == true
                ? exception.Path[..^(name.Length + 1)]
                : exception.Path;

            return $"Unknown property '{name}'{(parent is null or "$" ? string.Empty : $" at {parent}")}.";
        }

        if (MissingProperty().Match(exception.Message) is { Success: true } missing)
        {
            return $"The '{missing.Groups[1].Value}' property is required{location}.";
        }

        return $"The input is not valid at {exception.Path} "
            + $"(line {exception.LineNumber + 1}, position {exception.BytePositionInLine + 1}).";
    }

    // Keeps the first line and drops the "(Parameter 'x')" suffix that .NET adds to argument exceptions.
    private static string WithoutDetails(string message) =>
        ParameterSuffix().Replace(message.Split('\n')[0].TrimEnd(), string.Empty);

    [GeneratedRegex(@"JSON property '([^']+)' could not be mapped")]
    private static partial Regex UnknownProperty();

    [GeneratedRegex(@"missing required properties including: '([^']+)'")]
    private static partial Regex MissingProperty();

    [GeneratedRegex(@" \(Parameter '[^']*'\)")]
    private static partial Regex ParameterSuffix();

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
