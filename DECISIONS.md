# Design Decisions

Log of design decisions for the Portfolio Rebalancing kata. Each decision records the options considered, the tradeoffs, and what was chosen (and why). Decisions are made by the author; the LLM proposes options and tradeoffs.

## Task summary

Build a `Portfolio` class holding a collection of `Stock`s. Each `Stock` exposes a "current price" (last available price). The portfolio also has a target allocation (e.g. 40% META, 60% AAPL). Provide a `Rebalance` method that tells which stocks to sell and which to buy so the portfolio matches its target allocation.

## Status overview

| # | Decision | Status |
|---|----------|--------|
| 1 | Language and project setup | Decided (layout pending) |
| 2 | Domain model (Stock, price source, holdings/quantities) | Decided |
| 3 | Numeric type for money and shares | Decided (rounding policy pending) |
| 4 | Allocation representation and validation | Open |
| 5 | Share granularity (fractional vs whole) and leftover | Fractional decided; rounding precision pending |
| 6 | Cash balance and self-financing rebalance | Open |
| 7 | Rebalance output (pure orders vs mutation) | Open |
| 8 | Edge cases | Open |
| 9 | Drift tolerance / minimum trade size | Open |
| 10 | Testing and documentation style | Open |

---

## D1: Language and project setup

**Status:** Decided (project layout still pending).

**Options considered:**
- Python: most readable, `Decimal`/`dataclasses` built in, minimal setup. Dynamic typing.
- TypeScript: strong typing, but `number` is a float so money needs a library; more setup.
- Ruby: matches a likely backend stack, but only worthwhile if the author is fluent.
- Java/Kotlin: strong typing and `BigDecimal`, but verbose.
- C# / .NET: strong typing, native `decimal` type for money, records, good test tooling.

**Decision:** C# 14 on .NET 10.

**Rationale:** The author is most familiar with it. `decimal` is a built-in exact-decimal type suited to money, and records / immutable types keep the domain model concise.

**Open sub-question:** project layout. Single project plus a test project in one solution, vs a single-file/minimal layout.

---

## D2: Domain model

**Status:** Decided.

### 2a: How a Stock provides its price

**Options considered:**
- A: `Stock` class with an abstract/virtual `CurrentPrice()`; tests subclass or stub it. Literal to the spec, but mixes identity with live external data.
- B: `Stock` is an immutable record (symbol only) and an `IPriceProvider` is injected. Clean separation, but deviates from "Stock has a CurrentPrice method".
- C: `Stock` has a symbol plus a price source (`Func<decimal>` / interface) passed at construction. Keeps `stock.CurrentPrice()` as in the spec and stays easy to fake.

**Decision:** C. `Stock` keeps a `CurrentPrice()` method backed by an injected price source.

**Rationale:** Faithful to the task's wording while remaining testable without a real market feed.

**Consequences:**
- `Stock` is a class with identity, not a plain value. Equality must be defined by symbol so it works as a dictionary key.
- Prices can change between calls, so rebalance must read each price once and compute from that snapshot.

### 2b: What "a collection of Stocks" means

**Options considered:**
- A: `Dictionary<Stock, quantity>` (uniqueness per stock is natural).
- B: `Holding(Stock, Quantity)` records in a list (explicit/extensible, but uniqueness must be enforced).

**Decision:** A. Holdings are a map from stock to quantity.

**Rationale:** Simplest structure that guarantees one entry per stock.

**Consequences:** If the portfolio reveals its holdings, it must do so through a read-only view so callers can't mutate the underlying dictionary. The quantity's numeric type depends on D3/D5.

### 2c: Can holdings change after construction?

**Options considered:**
- Mutable: public `Buy`/`Sell` change the holdings in place. Natural for an app, but state changes over time and invalid operations (e.g. selling more than owned) must be handled.
- Immutable: holdings fixed at construction. Simpler to test, but something else must apply trades.

**Decision:** Mutable. The portfolio's holdings change over time through buy and sell operations.

**Rationale:** In a real trading app the holdings do change. Modelling that directly is more faithful than rebuilding the portfolio each time.

**Consequences:**
- `Buy`/`Sell` must validate input (positive quantity, cannot sell more than owned). Tracked under D8.
- Whether `Rebalance` itself applies the trades or only returns them is still open in D7.

---

## D3: Numeric type for money and shares

**Status:** Decided. Rounding policy is pending (see D5).

**Options considered (money):** `decimal`, `double`, integer minor units, a `Money` wrapper around `decimal`.

**Decision:** `decimal` for all money (prices, portfolio value, trade amounts). No `Money` wrapper.

**Rationale:** Exact for base-10 values, built into C#, and the idiomatic choice for money. `double` drifts (`0.1 + 0.2 != 0.3`). A `Money` wrapper is over-engineering since the task has no currencies; it can be mentioned as a future extension.

**Quantity type:** `decimal`, since fractional shares are allowed (D5).

**Open:** `decimal` still cannot represent values like 1/3 exactly, so an explicit rounding policy is needed (mode and where it is applied). Tracked in D5.

---

## D5 (partial): Share granularity

**Decision so far:** Fractional shares are allowed.

**Consequences:**
- Quantities are `decimal`.
- The whole-shares leftover-cash problem does not arise in the main flow.

**Still open:** the precision and rounding mode for quantities, since a split like 1/3 is a repeating decimal.
