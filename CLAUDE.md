# CLAUDE.md

## Project

Take-home kata: a portfolio management module for a personal investments/trading app. A `Portfolio` holds `Stock`s and a target allocation (e.g. 40% META / 60% AAPL). A rebalance method reports which stocks to sell and which to buy so the portfolio matches the target. See `DECISIONS.md` for the full task summary.

## Working agreement

- This is a pair-programming session. The user makes every design decision; Claude proposes options with advantages and tradeoffs, gives a recommendation, and waits.
- Present one decision at a time. Do not write production code until the user says so.
- When a decision is made, record it in `DECISIONS.md` (options, tradeoffs, choice, rationale) and update the summary below.
- Commit after every advance (each recorded decision or code step) so the history tells the story. Keep commits small and descriptive.
- Code must be documented with comments explaining the reasoning, since the deliverable is judged on the thought process.
- LLM conversations used for this task must be shared with the submission. Keep `DECISIONS.md` as a readable trail of the reasoning.

## Decisions so far

- **D1 Stack:** C# 14, .NET 10. Layout in D10.
- **D2a Price:** `Stock` has a symbol and an injected price source; `stock.CurrentPrice()` as in the spec. Equality by symbol. Rebalance reads each price once (snapshot).
- **D2b Holdings:** `Dictionary<Stock, quantity>`; expose only a read-only view.
- **D2c Mutability:** mutable; `Buy`/`Sell` change holdings and must validate input. Whether `Rebalance` applies trades or only returns them is open (D7).
- **D3 Numbers:** `decimal` for all money. No `Money` wrapper.
- **D5 Rounding:** fractional shares (`decimal` quantities). Round only final order quantities, to 4 decimals, banker's rounding (`ToEven`). Intermediate math keeps full precision. Clamp sells to shares held (D8); tolerate tiny buy/sell residual (D6).
- **D4 Allocation:** `Dictionary<Stock, decimal>` of fractions on `Portfolio`. Validated on set (fail fast): non-empty, each weight in (0, 1], sum exactly 1.
- **D6 Cash:** no cash balance; rebalance is self-financing. Rounding residual accepted and documented; tests use a small tolerance. Keep the "amount to distribute" in one calculation so cash can be added later (YAGNI: do not build it now).
- **D7 Output:** `Rebalance()` is pure and returns a flat list of immutable `Order(Stock, Side, Quantity)` (positive quantity, sells before buys, zero-quantity orders dropped). A separate `Apply(orders)` executes them through `Buy`/`Sell`.
- **D8 Edge cases:** held-but-unallocated stock is sold entirely; allocated-but-unheld is bought; total value 0 returns an empty list; no allocation set throws `InvalidOperationException`; non-positive price throws naming the stock; `Buy`/`Sell` validate (qty > 0, sell <= held, full sell removes entry); `Apply` is all-or-nothing; symbols are trimmed/uppercased, null/blank rejected, equality by symbol.
- **D9 Threshold:** none. Trade whenever the rounded delta is not 0. Tolerance / minimum trade size is a documented future extension (self-financing caveat), not built.
- **D10 Process:** solution with `src/` library and `tests/` project (proposed names `PortfolioRebalancing`, `PortfolioRebalancing.Tests`). xUnit, plain `Assert` (no FluentAssertions). TDD in small steps, one commit each. Order: Stock, holdings, allocation validation, Rebalance, Apply. Tests use a fake price source.
- **Comments:** simple XML docs on public types/methods; inline comments only where intent is unclear. No noise.
- **Deliverables:** `README.md` (build/test, algorithm, assumptions, future extensions) and the conversation transcript in `docs/conversation.md` before submission.

(Keep this list in sync with `DECISIONS.md`.)
