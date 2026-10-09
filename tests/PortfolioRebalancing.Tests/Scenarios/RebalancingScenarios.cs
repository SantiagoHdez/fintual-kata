namespace PortfolioRebalancing.Tests.Scenarios;

[Trait("Category", "Scenario")]
public class RebalancingScenarios
{
    private readonly FakeMarket _market = new();

    private static Dictionary<Stock, decimal> Weights(params (Stock Stock, decimal Weight)[] weights) =>
        weights.ToDictionary(entry => entry.Stock, entry => entry.Weight);

    [Fact]
    public void SpecExample_RebalancesToFortyPercentMetaAndSixtyPercentApple()
    {
        // Given 10 META at 500 and 5 AAPL at 200, worth 6000, and a 40/60 target
        var meta = _market.List("META", 500m);
        var apple = _market.List("AAPL", 200m);
        var portfolio = new Portfolio();
        portfolio.Buy(meta, 10m);
        portfolio.Buy(apple, 5m);
        portfolio.SetAllocation(Weights((meta, 0.4m), (apple, 0.6m)));
        var valueBefore = portfolio.TotalValue();

        // When we rebalance and apply the plan
        var orders = portfolio.Rebalance();
        portfolio.Apply(orders);

        // Then META is sold, AAPL is bought, and the portfolio is on target
        Assert.Equal([new Order(meta, Side.Sell, 5.2m), new Order(apple, Side.Buy, 13m)], orders);
        Assert.Equal(4.8m, portfolio.Holdings[meta]);
        Assert.Equal(18m, portfolio.Holdings[apple]);
        PortfolioAssert.MatchesAllocation(portfolio);
        PortfolioAssert.ValueIsConserved(valueBefore, portfolio.TotalValue(), orders);
    }

    [Fact]
    public void Drift_PricesMoveAwayFromTheTargetAndRebalancingRestoresIt()
    {
        // Given a portfolio that starts exactly on its 40/60 target
        var meta = _market.List("META", 500m);
        var apple = _market.List("AAPL", 200m);
        var portfolio = new Portfolio();
        portfolio.Buy(meta, 4.8m);
        portfolio.Buy(apple, 18m);
        portfolio.SetAllocation(Weights((meta, 0.4m), (apple, 0.6m)));
        Assert.Empty(portfolio.Rebalance());

        // When META rises and AAPL falls
        _market.SetPrice("META", 650m);
        _market.SetPrice("AAPL", 190m);
        var orders = portfolio.Rebalance();

        // Then some META is sold to buy more AAPL
        Assert.Equal([new Order(meta, Side.Sell, 0.7754m), new Order(apple, Side.Buy, 2.6526m)], orders);

        // And once applied, the portfolio is back on target with nothing left to do
        portfolio.Apply(orders);
        PortfolioAssert.MatchesAllocation(portfolio);
        Assert.Empty(portfolio.Rebalance());
    }

    [Fact]
    public void ManyCycles_StaysOnTargetThroughRepeatedPriceMoves()
    {
        // Given an unbalanced portfolio and a 40/60 target
        var meta = _market.List("META", 500m);
        var apple = _market.List("AAPL", 200m);
        var portfolio = new Portfolio();
        portfolio.Buy(meta, 10m);
        portfolio.Buy(apple, 5m);
        portfolio.SetAllocation(Weights((meta, 0.4m), (apple, 0.6m)));

        // When prices move and we rebalance after each move
        (decimal Meta, decimal Apple)[] moves = [(500m, 200m), (520m, 180m), (480m, 230m), (610m, 210m), (450m, 250m)];

        foreach (var (metaPrice, applePrice) in moves)
        {
            _market.SetPrice("META", metaPrice);
            _market.SetPrice("AAPL", applePrice);
            var valueBefore = portfolio.TotalValue();

            var orders = portfolio.Rebalance();
            portfolio.Apply(orders);

            // Then every cycle ends on target without gaining or losing value
            PortfolioAssert.MatchesAllocation(portfolio);
            PortfolioAssert.BuysMatchSells(orders);
            PortfolioAssert.ValueIsConserved(valueBefore, portfolio.TotalValue(), orders);
        }
    }

    [Fact]
    public void ChangingTheTarget_MovesAnExistingPortfolioToTheNewWeights()
    {
        // Given a portfolio on its 40/60 target
        var meta = _market.List("META", 500m);
        var apple = _market.List("AAPL", 200m);
        var portfolio = new Portfolio();
        portfolio.Buy(meta, 4.8m);
        portfolio.Buy(apple, 18m);
        portfolio.SetAllocation(Weights((meta, 0.4m), (apple, 0.6m)));

        // When the target changes to 50/50 and we rebalance
        portfolio.SetAllocation(Weights((meta, 0.5m), (apple, 0.5m)));
        var orders = portfolio.Rebalance();
        portfolio.Apply(orders);

        // Then AAPL is sold to buy META, and the new weights are reached
        Assert.Equal([new Order(apple, Side.Sell, 3m), new Order(meta, Side.Buy, 1.2m)], orders);
        PortfolioAssert.MatchesAllocation(portfolio);
    }

    [Fact]
    public void NewTarget_BuysAStockThatEntersAndSellsOneThatLeaves()
    {
        // Given a portfolio of META and AAPL on its 40/60 target
        var meta = _market.List("META", 500m);
        var apple = _market.List("AAPL", 200m);
        var google = _market.List("GOOG", 150m);
        var portfolio = new Portfolio();
        portfolio.Buy(meta, 4.8m);
        portfolio.Buy(apple, 18m);
        portfolio.SetAllocation(Weights((meta, 0.4m), (apple, 0.6m)));

        // When the target becomes 50% META and 50% GOOG, so AAPL leaves and GOOG enters
        portfolio.SetAllocation(Weights((meta, 0.5m), (google, 0.5m)));
        var orders = portfolio.Rebalance();
        portfolio.Apply(orders);

        // Then AAPL is sold entirely, GOOG is bought, and AAPL is no longer held
        Assert.Equal(
            [new Order(apple, Side.Sell, 18m), new Order(google, Side.Buy, 20m), new Order(meta, Side.Buy, 1.2m)],
            orders);
        Assert.DoesNotContain(apple, portfolio.Holdings.Keys);
        PortfolioAssert.MatchesAllocation(portfolio);
    }
}
