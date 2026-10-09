# Design Decisions

Log of design decisions for the Portfolio Rebalancing kata. Each decision records the options considered, the tradeoffs, and what was chosen (and why). Decisions are made by the author; the LLM proposes options and tradeoffs.

## Task summary

Build a `Portfolio` class holding a collection of `Stock`s. Each `Stock` exposes a "current price" (last available price). The portfolio also has a target allocation (e.g. 40% META, 60% AAPL). Provide a `Rebalance` method that tells which stocks to sell and which to buy so the portfolio matches its target allocation.

## Status overview

| # | Decision | Status |
|---|----------|--------|
| 1 | Language and project setup | Decided (layout pending) |
| 2 | Domain model (Stock, price source, holdings/quantities) | Decided |
| 3 | Numeric type for money and shares | Decided |
| 4 | Allocation representation and validation | Decided |
| 5 | Share granularity (fractional vs whole) and rounding | Decided |
| 6 | Cash balance and self-financing rebalance | Decided |
| 7 | Rebalance output (pure orders vs mutation) | Decided |
| 8 | Edge cases | Decided |
| 9 | Drift tolerance / minimum trade size | Decided |
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
