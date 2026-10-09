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

    private static string Input(string holdings, string prices, string allocation) =>
        "{ \"holdings\": " + holdings + ", \"prices\": " + prices + ", \"allocation\": " + allocation + " }";

    private const string GoodHoldings = "[{ \"symbol\": \"META\", \"quantity\": 10 }]";
    private const string GoodPrices = "{ \"META\": 500, \"AAPL\": 200 }";
    private const string GoodAllocation = "{ \"META\": 0.4, \"AAPL\": 0.6 }";

    public static TheoryData<string, string> InvalidInputs => new()
    {
        { "null", "The input must be a JSON object." },
        { "{ \"holdings\": [], \"prices\": {} }", "The 'allocation' property is required." },
        { "{ \"holdings\": [], \"prices\": {}, \"alocation\": {} }", "Unknown property 'alocation'." },
        {
            Input("[{ \"symbol\": \"META\" }]", GoodPrices, GoodAllocation),
            "The 'quantity' property is required at $.holdings[0]."
        },
        {
            Input("[{ \"symbol\": \"META\", \"quantity\": 1, \"qty\": 2 }]", GoodPrices, GoodAllocation),
            "Unknown property 'qty' at $.holdings[0]."
        },
        {
            Input(GoodHoldings, "{ \"META\": \"cheap\" }", GoodAllocation),
            "The input is not valid at $.prices.META (line 1, position 82)."
        },
        {
            Input("[{ \"symbol\": \"GOOG\", \"quantity\": 1 }]", GoodPrices, GoodAllocation),
            "No price was given for 'GOOG'."
        },
        {
            Input(GoodHoldings, GoodPrices, "{ \"META\": 0.5, \"GOOG\": 0.5 }"),
            "No price was given for 'GOOG'."
        },
        {
            Input(GoodHoldings, "{ \"META\": 500, \"meta\": 501 }", "{ \"META\": 1 }"),
            "'meta' is listed more than once in prices."
        },
        {
            Input(GoodHoldings, GoodPrices, "{ \"META\": 0.4, \"meta\": 0.6 }"),
            "'meta' is listed more than once in allocation."
        },
        {
            Input("[{ \"symbol\": \"META\", \"quantity\": 0 }]", GoodPrices, GoodAllocation),
            "The quantity of 'META' in holdings must be positive, but was 0."
        },
        {
            Input(GoodHoldings, GoodPrices, "{ \"META\": 0.5, \"AAPL\": 0.6 }"),
            "The weights must sum to exactly 1, but sum to 1.1."
        },
        {
            Input(GoodHoldings, GoodPrices, "{ \"META\": 1.5 }"),
            "The weight of META must be greater than 0 and at most 1."
        },
        { Input(GoodHoldings, GoodPrices, "{}"), "The allocation must contain at least one stock." },
        {
            Input(GoodHoldings, "{ \"META\": 0, \"AAPL\": 200 }", GoodAllocation),
            "The price of META must be positive, but was 0."
        },
        { Input("[{ \"symbol\": \" \", \"quantity\": 1 }]", GoodPrices, GoodAllocation), "A symbol is empty." },
        { Input(GoodHoldings, "{ \" \": 500, \"META\": 500 }", GoodAllocation), "A symbol is empty." },
    };

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public void Run_RejectsInvalidInputWithAMessageThatSaysWhy(string json, string expectedMessage)
    {
        var exception = Assert.Throws<InputException>(() => RebalanceRunner.Run(json));

        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void Run_SaysWhereMalformedJsonStops()
    {
        var exception = Assert.Throws<InputException>(() => RebalanceRunner.Run("{ \"holdings\": ["));

        Assert.StartsWith("The input is not valid at $", exception.Message);
        Assert.Contains("line 1", exception.Message);
    }

    [Fact]
    public void Run_MatchesSymbolsIgnoringCaseAndSpaces()
    {
        var output = RebalanceRunner.Run(Input(
            "[{ \"symbol\": \" meta \", \"quantity\": 10 }, { \"symbol\": \"aapl\", \"quantity\": 5 }]",
            "{ \"META\": 500, \"Aapl\": 200 }",
            "{ \"Meta\": 0.4, \"AAPL\": 0.6 }"));

        Assert.Equal(2, OrdersOf(output).Count);
    }

    [Fact]
    public void Run_AddsUpASymbolListedTwiceInHoldings()
    {
        var output = RebalanceRunner.Run(Input(
            "[{ \"symbol\": \"META\", \"quantity\": 4 }, { \"symbol\": \"META\", \"quantity\": 6 }, { \"symbol\": \"AAPL\", \"quantity\": 5 }]",
            GoodPrices,
            GoodAllocation));

        Assert.Equal(5.2m, OrdersOf(output)[0]!["quantity"]!.GetValue<decimal>());
    }
}
