# CLAUDE.md

## Project

Take-home kata: a portfolio management module for a personal investments/trading app. A `Portfolio` holds `Stock`s and a target allocation (e.g. 40% META / 60% AAPL). A rebalance method reports which stocks to sell and which to buy so the portfolio matches the target. See `DECISIONS.md` for the full task summary.

## Working agreement

- This is a pair-programming session. The user makes every design decision; Claude proposes options with advantages and tradeoffs, gives a recommendation, and waits.
- Present one decision at a time. Do not write production code until the user says so.
- When a decision is made, record it in `DECISIONS.md` (options, tradeoffs, choice, rationale) and update the summary below.
- Code must be documented with comments explaining the reasoning, since the deliverable is judged on the thought process.
- LLM conversations used for this task must be shared with the submission. Keep `DECISIONS.md` as a readable trail of the reasoning.

## Decisions so far

- **D1 Stack:** C# 14, .NET 10. Project layout still pending.
- **D2a Price:** `Stock` has a symbol and an injected price source; `stock.CurrentPrice()` as in the spec. Equality by symbol. Rebalance reads each price once (snapshot).
- **D2b Holdings:** `Dictionary<Stock, quantity>`; expose only a read-only view.
- **D2c Mutability:** open, deferred to D7.

(Keep this list in sync with `DECISIONS.md`.)
