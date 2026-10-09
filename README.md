# Portfolio Rebalancing

A small portfolio module for a personal investments app. A `Portfolio` holds stocks and a target allocation (for example 40% META, 60% AAPL). `Rebalance()` says which stocks to sell and which to buy so the portfolio matches that allocation.

The reasoning behind each design choice is in [DECISIONS.md](DECISIONS.md), written as a sequence of decisions with options and tradeoffs.

## Requirements

- .NET 10 SDK (C# 14)

## Build and test

```bash
dotnet build
dotnet test                                   # all tests
dotnet test --filter Category=Scenario        # only the end-to-end scenarios
```

## Example

```csharp
var meta = new Stock("META", () => 500m);   // the function returns the last available price
var apple = new Stock("AAPL", () => 200m);

var portfolio = new Portfolio();
portfolio.Buy(meta, 10m);
portfolio.Buy(apple, 5m);
portfolio.SetAllocation(new Dictionary<Stock, decimal> { [meta] = 0.4m, [apple] = 0.6m });

IReadOnlyList<Order> orders = portfolio.Rebalance();
// Sell 5.2 META, Buy 13 AAPL

portfolio.Apply(orders);   // optional: execute the orders
```

## Command line

A small CLI lets you try the module without writing C#. It reads a portfolio as JSON from a file, or from stdin when no file is given, and prints the orders that rebalance it as JSON.

```bash
dotnet run --project src/PortfolioRebalancing.Cli -- samples/example.json
cat samples/example.json | dotnet run --project src/PortfolioRebalancing.Cli
```

Input (`samples/example.json`):

```json
{
  "holdings": [{ "symbol": "META", "quantity": 10 }, { "symbol": "AAPL", "quantity": 5 }],
  "prices": { "META": 500, "AAPL": 200 },
  "allocation": { "META": 0.4, "AAPL": 0.6 }
}
```

Output:

```json
{ "orders": [
  { "symbol": "META", "side": "Sell", "quantity": 5.2 },
  { "symbol": "AAPL", "side": "Buy", "quantity": 13 }
] }
```

- All three properties are required, and unknown properties are rejected, so a typo is an error and not silently ignored.
- Every held or allocated symbol needs a price. Symbols are matched ignoring case and spaces.
- Exit codes: `0` success, `1` invalid input or unreadable file (the reason is on stderr and stdout stays empty), `2` wrong usage. `--help` shows the usage.
- Prices are fixed numbers, so the CLI shows one rebalance. It does not apply the plan.

## How it works

1. Read the price of every stock that is held or in the allocation, once. The rest of the calculation uses that snapshot.
2. The total value is the sum of shares times price. This is the amount to distribute.
3. For each stock, the target is `total * weight / price` shares. A held stock that is not in the allocation has weight 0, so it is sold entirely.
4. The difference from the shares held is the order: a negative difference is a sell, a positive one is a buy.
5. Quantities are rounded to 4 decimals (banker's rounding) only at this last step. A sell is never larger than the shares held, and orders that round to zero are dropped.
6. The result lists sells first, then buys, each sorted by symbol.

`Rebalance()` never changes the portfolio. `Apply(orders)` does, and it is all-or-nothing: if any order is invalid, no order is applied.

## Project layout

```
src/PortfolioRebalancing/          Stock, Portfolio, Order, Side
src/PortfolioRebalancing.Cli/      small JSON command line (see above)
tests/PortfolioRebalancing.Tests/  unit tests, Scenarios/ with end-to-end stories, Cli/ tests
samples/example.json               input for the command line
DECISIONS.md                       design decisions, options and tradeoffs
CLAUDE.md                          working agreement and conventions for the LLM pair
```

## Assumptions

- **No cash.** The portfolio only holds stocks, so a rebalance is self-financing: what is sold pays for what is bought. An empty portfolio has nothing to trade.
- **Fractional shares are allowed.** Quantities and money are `decimal`.
- **Weights are fractions that sum to exactly 1.** An allocation that does not is rejected when it is set.
- **Rounding leaves a tiny gap.** Buys and sells can differ by up to half a rounding step (0.00005 shares) per order, times the price. It is accepted, and the tests check it against that bound.
- **Prices are read when `Rebalance()` runs.** Orders are share quantities, so a plan applied after prices move still runs but may end off target. Plan again right before applying.
- **Not thread-safe.** A `Portfolio` is meant to be used from one thread at a time.

## Possible extensions

Left out on purpose to keep the module small:

- A cash balance, so new deposits can be invested. The total value is computed in one place (`Portfolio.TotalValue`) to make this a contained change.
- A drift tolerance or minimum trade size to avoid tiny trades. Skipping trades would break the sells-pay-for-buys balance, so the matching trades would need to be dropped or scaled too.
- A tolerance on the weights, so an equal three-way split could be written as 0.3333 each.
- A `Money` type with a currency instead of plain `decimal`.
- Whole-share trading, with the leftover cash tracked.
