# Design Decisions

Log of design decisions for the Portfolio Rebalancing kata. Each decision records the options considered, the tradeoffs, and what was chosen (and why). Decisions are made by the author; the LLM proposes options and tradeoffs.

## Task summary

Build a `Portfolio` class holding a collection of `Stock`s. Each `Stock` exposes a "current price" (last available price). The portfolio also has a target allocation (e.g. 40% META, 60% AAPL). Provide a `Rebalance` method that tells which stocks to sell and which to buy so the portfolio matches its target allocation.

## Status overview

| # | Decision | Status |
|---|----------|--------|
| 1 | Language and project setup | Decided (layout in D10) |
| 2 | Domain model (Stock, price source, holdings/quantities) | Decided |
| 3 | Numeric type for money and shares | Decided |
| 4 | Allocation representation and validation | Decided |
| 5 | Share granularity (fractional vs whole) and rounding | Decided |
| 6 | Cash balance and self-financing rebalance | Decided |
| 7 | Rebalance output (pure orders vs mutation) | Decided |
| 8 | Edge cases | Decided |
| 9 | Drift tolerance / minimum trade size | Decided |
| 10 | Testing and documentation style | Decided |
| 11 | Scenario testing | Decided |
| 12 | Small JSON CLI | Decided |

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

**Project layout:** decided in D10.

---

## D2: Domain model

**Status:** Decided.

### 2a: How a Stock provides its price

**Options considered:**
- A: `Stock` class with an abstract/virtual `CurrentPrice()`; tests subclass or stub it. Literal to the spec, but mixes identity with live external data.
- B: `Stock` is an immutable record (symbol only) and an `IPriceProvider` is injected. Clean separation, but deviates from "Stock has a CurrentPrice method".
- C: `Stock` has a symbol plus a price source (`Func<decimal>` / interface) passed at construction. Keeps `stock.CurrentPrice()` as in the spec and stays easy to fake.

**Decision:** C. `Stock` keeps a `CurrentPrice()` method backed by an injected price source.

**Refinement:** the price source is a plain `Func<decimal>` passed to the constructor. No `IPriceSource` interface: it needs no extra type, and tests can pass `() => 100m` or a captured variable to simulate price changes.

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

**Status:** Decided. Rounding policy is in D5.

**Options considered (money):** `decimal`, `double`, integer minor units, a `Money` wrapper around `decimal`.

**Decision:** `decimal` for all money (prices, portfolio value, trade amounts). No `Money` wrapper.

**Rationale:** Exact for base-10 values, built into C#, and the idiomatic choice for money. `double` drifts (`0.1 + 0.2 != 0.3`). A `Money` wrapper is over-engineering since the task has no currencies; it can be mentioned as a future extension.

**Quantity type:** `decimal`, since fractional shares are allowed (D5).

**Open:** `decimal` still cannot represent values like 1/3 exactly, so an explicit rounding policy is needed (mode and where it is applied). Resolved in D5.

---

## D5: Share granularity and rounding

**Status:** Decided.

**Options considered:**
- Granularity: fractional vs whole shares.
- Precision: no rounding (full `decimal`), fixed N decimals, or configurable.
- Mode: `ToZero` (truncate), `ToEven` (banker's), `AwayFromZero`.
- Placement: only on final order quantities, or at every step.

**Decisions:**
- Fractional shares are allowed, so quantities are `decimal`.
- Quantities are rounded to a fixed 4 decimal places.
- Rounding mode is banker's rounding (`MidpointRounding.ToEven`).
- Rounding is applied only once, on the final order quantities. All intermediate math (total value, target values, deltas) keeps full `decimal` precision.

**Rationale:** Fractional shares remove the whole-share leftover-cash problem. A fixed precision keeps orders realistic and the API simple. Banker's rounding is unbiased in aggregate. Rounding only at the end avoids compounding errors.

**Tradeoffs accepted:**
- Banker's rounding can overshoot by up to half a unit of the last place (0.00005 shares). A sell could marginally exceed the shares held, and total buys could marginally exceed total sell proceeds. Consequences to handle:
  - D8: clamp a sell quantity to the shares actually held.
  - D6: the self-financing check must tolerate this tiny residual (or define how it is treated).
- The 4-decimal precision is a fixed assumption, not configurable. Making it a parameter is a possible extension.

---

## D4: Allocation representation and validation

**Status:** Decided.

**Options considered:**
- Weight format: fraction (0.40), percentage (40), or a dedicated value type.
- Location: `Dictionary<Stock, decimal>` on `Portfolio`, or a separate `Allocation` class.
- Sum rule: strict sum of 1, or a tolerance with normalization.
- Validation timing: fail fast when set, or validate at rebalance time.

**Decisions:**
- Weights are `decimal` fractions (0.40 = 40%).
- The allocation is a `Dictionary<Stock, decimal>` on the `Portfolio`, mirroring the holdings structure.
- The weights must sum to exactly 1 (strict, no tolerance).
- Validation happens when the allocation is set (fail fast), so `Rebalance` can trust it.

**Validation rules enforced on set:**
- Allocation is not empty.
- Each weight is greater than 0 and at most 1.
- No duplicate stocks (guaranteed by the dictionary key).
- Weights sum to exactly 1.

**Rationale:** Fractions make the math direct (`target value = total * weight`) and avoid off-by-100 bugs. Strict validation is easy to explain and test and fails loudly on bad input. Fail-fast keeps invalid state from ever existing.

**Tradeoffs accepted:**
- An equal three-way split cannot be written as 0.3333 x 3; the caller must supply weights that sum exactly to 1 (e.g. 0.3333, 0.3333, 0.3334). A tolerance with normalization is a possible future extension.
- No separate `Allocation` type; if validation grows, extracting one is the natural next step.

---

## D6: Cash and self-financing

**Status:** Decided.

**Options considered:**
- Cash: no cash; cash included in total value; cash held but excluded from the target.
- Funding: self-financing (buys funded only by sells) vs buys may use available cash.

**Decisions:**
- The portfolio has no cash balance. Total value is the sum of `quantity * CurrentPrice()` over the holdings.
- Rebalance is self-financing: sell proceeds fund the buys, so total value is conserved.
- The tiny residual between sell proceeds and buy cost caused by D5's rounding is accepted and documented. Tests compare it with a small tolerance.
- The design leaves a seam for cash without building it (YAGNI): the amount to be distributed across the allocation is computed in one single place (a `TotalValue`-style calculation), and target values are derived from that amount, not from inline sums. Adding cash later should mean changing that one calculation and adding the field, not reworking the rebalance logic.

**Rationale:** The task describes only stocks and a target distribution, so this is the minimal model that answers it. A cash balance, deposits, and a rule for insufficient cash are real features, but nothing in the task needs them yet.

**Tradeoffs accepted:**
- Cannot deploy newly deposited money or hold a cash buffer.
- The rounding residual has nowhere to go; it is tolerated rather than tracked.

---

## D7: Rebalance output and mutation

**Status:** Decided.

**Options considered:**
- 7a: `Rebalance` is pure and returns orders (A); applies trades itself (B); both, as separate operations (C).
- 7b: flat list of orders; result object with separate `Sells`/`Buys`; signed quantity per stock.
- 7c: include only stocks that need a trade, or every stock.

**Decisions:**
- 7a (C): `Rebalance()` is pure. It reads prices, computes the plan, and returns it without touching the portfolio. A separate operation applies a plan (`Apply(orders)`, executed through the portfolio's `Buy`/`Sell`). Exact naming can be refined when writing the code.
- 7b: the result is a flat list of immutable `Order(Stock, Side, Quantity)` records. `Side` is `Buy` or `Sell`, and quantity is always positive. Sells are listed before buys, which is the natural execution order for a self-financing rebalance.
- 7c: only stocks that need a trade appear. An order whose rounded quantity is 0 is dropped, so an empty list means "already balanced".

**Rationale:** Separating planning from execution keeps `Rebalance` free of side effects (safe to call repeatedly, easy to test, previewable) while still letting the mutable portfolio (D2c) apply the plan. A flat list with positive quantities is the simplest shape to iterate, print and assert on.

**Tradeoffs accepted:**
- Slightly larger API than the task strictly asks for (`Apply` is extra).
- Callers who want only sells or only buys filter the list.

**Consequence for D8:** `Apply` can fail midway (for example a sell that exceeds holdings). Whether it validates everything first (all-or-nothing) is decided in D8.

---

## D8: Edge cases

**Status:** Decided (all recommendations accepted).

| # | Case | Decision |
|---|------|----------|
| 8a | Stock held but not in the allocation | Sell all of it (target weight 0). The allocation is the whole truth about what the portfolio should contain. |
| 8b | Stock in the allocation but not held | Buy it (current quantity 0). |
| 8c | Total value is 0 (empty portfolio) | Return an empty list. With no cash (D6) there is nothing to distribute, and nothing is wrong. |
| 8d | `Rebalance` called with no allocation set | Throw `InvalidOperationException`. Returning an empty list would look like "already balanced". |
| 8e | Price is zero or negative | Throw, naming the stock. Exceptions from the price source propagate unchanged. Each price is read once per rebalance (D2a). |
| 8f | `Buy`/`Sell` input | Quantity must be > 0. A sell cannot exceed the quantity held (a rounded sell is clamped to the shares held, see D5). Selling the whole position removes it from the holdings. Violations throw. |
| 8g | `Apply` atomicity | All-or-nothing: validate every order first, then change anything. A half-applied rebalance is neither the old state nor the target. |
| 8h | Stock identity | Reject null/blank symbols, trim, and uppercase on construction. Equality is by (normalized) symbol. |

**Rationale:** Fail loudly on misuse and on corrupt data (bad prices, invalid quantities) rather than returning plausible-looking but wrong results. Treat "nothing to do" situations (empty portfolio, already balanced) as normal results, not errors.

---

## D9: Drift tolerance and minimum trade size

**Status:** Decided.

**Options considered:**
- A: no threshold; trade whenever the rounded delta is not 0.
- B: drift tolerance (skip stocks within a band around their target).
- C: minimum trade value (skip small orders).
- D: optional threshold parameter defaulting to "none".

**Decision:** A. No threshold. The only filtering is dropping orders whose quantity rounds to 0 (D5, D7).

**Rationale:** The task asks for a portfolio balanced to its allocation. A threshold is a policy layer on top of that, and building it would violate YAGNI, consistent with the cash decision in D6.

**Future extension (documented, not built):** A tolerance or minimum trade size. Caveat: skipping some trades breaks the sells-fund-buys balance of the self-financing model (D6), so a real implementation would have to drop or scale the matching trades too.

---

## D10: Project layout, testing and documentation

**Status:** Decided.

**Options considered:**
- Layout: solution with a library and a test project (A); a single project (B); library, tests and a console demo (C).
- Test framework: xUnit, NUnit, MSTest.
- Strategy: test-first vs tests after.

**Decisions:**
- Layout (A): one solution with a library project in `src/` and a test project in `tests/`. No console demo; the tests demonstrate the behavior. (Superseded in part by D12, which adds a small CLI.) Project names avoid clashing with the `Portfolio` class (proposed: `PortfolioRebalancing` and `PortfolioRebalancing.Tests`).
- Test framework: xUnit with plain `Assert`. FluentAssertions is avoided because it is now commercially licensed.
- Strategy: TDD. One behavior at a time, with a commit per step so the history tells the story. Planned order: `Stock`, `Portfolio` holdings, allocation validation, `Rebalance`, `Apply`.
- Tests use a fake price source (the injected source from D2a), so no market feed is needed and "price changes between calls" can be tested.
- Key scenarios: the spec example (40% META / 60% AAPL); already balanced; held-but-unallocated sold and allocated-but-unheld bought; empty portfolio; invalid allocations; non-positive prices; the rounding residual; `Apply` all-or-nothing; a second `Rebalance` after `Apply` returns an empty list; sell proceeds approximately equal buy cost within tolerance.
- Documentation: simple XML doc comments on public types and methods. Inline comments are kept simple and used only where the code's intent may not be clear. `DECISIONS.md` is the narrative record. A `README.md` covers build and test instructions, a short description of the algorithm, assumptions and future extensions.
- The LLM conversation is saved in the repo (e.g. `docs/conversation.md`) before submission.

**Rationale:** The standard .NET layout is familiar to reviewers. TDD with small commits shows the reasoning step by step. Keeping comments sparse prevents noise and keeps the real decisions in `DECISIONS.md`.

---

## D11: Scenario testing

**Status:** Decided.

**Options considered:**
- Style: plain xUnit with Given/When/Then comments; Gherkin with Reqnroll; table-driven `[Theory]` cases.
- Location: a `Scenarios/` folder in the existing test project (with a trait), or a separate test project.
- Extra checks: invariant assertions inside each scenario, or property-based tests (FsCheck).

**Decisions:**
- Plain xUnit, with Given / When / Then comments in each scenario. No new dependency.
- Scenarios live in a `Scenarios/` folder of the existing test project and carry `[Trait("Category", "Scenario")]`, so they can run on their own: `dotnet test --filter Category=Scenario`.
- A small `FakeMarket` helper holds prices that tests can change, to express time passing.
- Invariants are asserted inside the scenarios (weights match the target within rounding, value is conserved within the rounding bound, buys match sells within the same bound). No property-based testing for now.

**Scenarios:**
1. The spec example (40% META / 60% AAPL), then apply.
2. Drift over time: a balanced portfolio, prices move, rebalance and apply.
3. Several cycles of price moves and rebalances.
4. Changing the target allocation (40/60 to 50/50).
5. A stock enters the allocation and another leaves it.
6. Fractional shares across five stocks with awkward prices.
7. A failed apply changes nothing, then a correct plan works.
8. A stale plan: prices move between `Rebalance` and `Apply`. The orders are share quantities, so the plan still executes, but the result is off target. Documented behavior: re-plan right before applying.
9. An empty portfolio: no cash means nothing can be bought (the D6 limitation).

**Rationale:** The unit tests check single rules. Scenarios show the module working through realistic sequences and double as executable documentation of the behavior and its limits.

---

## D12: A small JSON CLI

**Status:** Decided. This reverses the "no console demo" part of D10.

**Options considered:**
- A: no CLI; the README example and the scenarios are the demo.
- B: minimal CLI project that reads JSON and prints the rebalance plan as JSON.
- C: like B, but it also applies the plan and prints the portfolio afterwards.
- D: a single-file .NET 10 script (`dotnet run demo.cs`) instead of a project.

**Decisions:**
- B. A new project `src/PortfolioRebalancing.Cli`, added to the solution. It uses only the library's public API, and the library is not changed.
- Input comes from a file given as the only argument, or from stdin when there is no argument.
- Output is JSON only, written to stdout. No table format.
- The logic lives in a function that takes the JSON text and returns the JSON text, so tests can run it without starting a process. `Main` only handles arguments, files and streams.
- Exit codes: 0 on success, 1 for invalid input or a failed rebalance (message on stderr, nothing on stdout), 2 for wrong usage.
- Tests live in the existing test project under `Cli/`, which references the CLI project.

**Input:**
```json
{
  "holdings":   [{ "symbol": "META", "quantity": 10 }, { "symbol": "AAPL", "quantity": 5 }],
  "prices":     { "META": 500, "AAPL": 200 },
  "allocation": { "META": 0.4, "AAPL": 0.6 }
}
```

**Output:**
```json
{ "orders": [
  { "symbol": "META", "side": "Sell", "quantity": 5.2 },
  { "symbol": "AAPL", "side": "Buy",  "quantity": 13 }
] }
```

**Rules:**
- All three properties are required. Unknown properties are rejected, so a typo such as `alocation` is an error and not silently ignored.
- Numbers are read as `decimal`, never `double`, to keep the exactness chosen in D3.
- Every held or allocated symbol needs a price. Symbols are matched ignoring case and surrounding spaces, like `Stock`.
- A symbol listed twice in `holdings` adds up. A symbol repeated in `prices` or `allocation` (ignoring case) is an error.
- The library's own validation applies as it does everywhere else (positive quantities and prices, allocation rules).

**Rationale:** It lets a reviewer try the module in seconds without reading C#, and the JSON documents the data contract. It stays small and additive, so the core deliverable is not touched.

**Tradeoffs accepted:**
- It goes beyond what the task asks, and adds a project to review.
- Prices from JSON are fixed numbers, so the CLI shows one rebalance and cannot show prices drifting over time. The scenarios cover that.
- It does not apply the plan (option C was not chosen).

---

## Refinements made while implementing

Small changes to earlier decisions that came up in the code. The original text above is kept as it was written.

- **D7: `Apply` runs the orders in the order given.** An earlier idea was to apply sells before buys whatever the input order. It was dropped: `Rebalance` already lists sells first, and extra ordering rules were not needed. `Apply` accepts any `IEnumerable<Order>`.
- **D7: `Apply` copies back into the same dictionary.** After applying to a working copy, the result is copied into the portfolio's own dictionary, so a `Holdings` view obtained earlier stays valid.
- **D8: all-or-nothing is shared with `Buy`/`Sell`.** `Apply`, `Buy` and `Sell` use the same private helpers, so there is one copy of the quantity rules.
- **D8: an empty portfolio still reads the prices of the allocated stocks.** A non-positive price on an allocated stock therefore throws even when there is nothing to trade.
- **D5: a full sell can exceed 4 decimals.** When a stock is sold entirely and the holding itself has more than 4 decimals (for example 1.00007), the order uses the exact holding, so no sliver of the position is left. This is the "never sell more than held" cap.
- **D6/D11: the tolerance is a bound, not a constant.** The tests allow buys and sells to differ by half a rounding step per order times its price, instead of a fixed number.
