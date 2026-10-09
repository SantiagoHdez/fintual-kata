using System.Text.Json.Nodes;

using PortfolioRebalancing.Cli;

namespace PortfolioRebalancing.Tests.Cli;

public class RebalanceRunnerTests
{
    private const string SpecExample = """
        {
          "holdings": [{ "symbol": "META", "quantity": 10 }, { "symbol": "AAPL", "quantity": 5 }],
          "prices": { "META": 500, "AAPL": 200 },
          "allocation": { "META": 0.4, "AAPL": 0.6 }
        }
        """;

    private static JsonArray OrdersOf(string output) => JsonNode.Parse(output)!["orders"]!.AsArray();

    [Fact]
    public void Run_ReturnsTheSellsAndBuysForTheSpecExample()
    {
        var output = RebalanceRunner.Run(SpecExample);

        var orders = OrdersOf(output);
        Assert.Equal(2, orders.Count);
        Assert.Equal("META", orders[0]!["symbol"]!.GetValue<string>());
        Assert.Equal("Sell", orders[0]!["side"]!.GetValue<string>());
        Assert.Equal(5.2m, orders[0]!["quantity"]!.GetValue<decimal>());
        Assert.Equal("AAPL", orders[1]!["symbol"]!.GetValue<string>());
        Assert.Equal("Buy", orders[1]!["side"]!.GetValue<string>());
        Assert.Equal(13m, orders[1]!["quantity"]!.GetValue<decimal>());
    }

    [Fact]
    public void Run_ReturnsAnEmptyListWhenThereIsNothingToTrade()
    {
        var output = RebalanceRunner.Run("""
            {
              "holdings": [],
              "prices": { "META": 500, "AAPL": 200 },
              "allocation": { "META": 0.4, "AAPL": 0.6 }
            }
            """);

        Assert.Empty(OrdersOf(output));
    }

    [Fact]
    public void Run_WritesQuantitiesWithoutTrailingZeros()
    {
        var orders = OrdersOf(RebalanceRunner.Run(SpecExample));

        Assert.Equal("5.2", orders[0]!["quantity"]!.ToJsonString());
        Assert.Equal("13", orders[1]!["quantity"]!.ToJsonString());
    }
}
