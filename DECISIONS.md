# Design Decisions

Log of design decisions for the Portfolio Rebalancing kata. Each decision records the options considered, the tradeoffs, and what was chosen (and why). Decisions are made by the author; the LLM proposes options and tradeoffs.

## Task summary

Build a `Portfolio` class holding a collection of `Stock`s. Each `Stock` exposes a "current price" (last available price). The portfolio also has a target allocation (e.g. 40% META, 60% AAPL). Provide a `Rebalance` method that tells which stocks to sell and which to buy so the portfolio matches its target allocation.

## Status overview

| # | Decision | Status |
|---|----------|--------|
| 1 | Language and project setup | Decided (layout pending) |
| 2 | Domain model (Stock, price source, holdings/quantities) | 2a, 2b decided; 2c deferred to D7 |
| 3 | Numeric type for money and shares | Open |
| 4 | Allocation representation and validation | Open |
| 5 | Share granularity (fractional vs whole) and leftover | Open |
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

**Status:** 2a and 2b decided; 2c deferred to D7.

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

**Status:** Open, deferred to D7. Mutable (`Buy`/`Sell` methods) vs immutable (fixed at construction). The task only asks rebalance to report trades, not execute them, which leans immutable / no public `Buy`/`Sell`.
