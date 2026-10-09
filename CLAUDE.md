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

## Git workflow

- `develop` is the integration branch. Never commit development work directly on `develop` or `main`.
- Start every piece of development on a new branch cut from `develop`: `feature/<short-name>` for code, `docs/<short-name>` for documentation-only changes.
- Commit after every advance on the branch (see the working agreement above).
- When the task is complete, merge the branch into `develop` with `git merge --no-ff` so each feature stays visible in the history, then delete the merged branch locally.
- Do not push, open PRs, or touch `main` unless the user asks.

## C# 14 / .NET 10 standards

Follow the Microsoft conventions: [C# coding conventions](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions), [identifier naming](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/identifier-names), and the [.NET design guidelines](https://learn.microsoft.com/dotnet/standard/design-guidelines/).

**Project setup**
- Target `net10.0` with `LangVersion` left at the default (C# 14).
- Enable `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>` and `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. Share settings through `Directory.Build.props`.
- Use the default `.slnx` solution format that `dotnet new sln` creates on .NET 10.
- Keep an `.editorconfig` that encodes these conventions, and run `dotnet format` before committing.

**Naming**
- `PascalCase` for types, methods, properties, events and constants. `camelCase` for parameters and locals. `_camelCase` for private fields. Interfaces start with `I`.
- Names say what the thing is. No abbreviations beyond well-known ones.

**Layout**
- Allman braces, 4-space indentation, one statement per line, one type per file named after the type.
- File-scoped namespaces. `using` directives at the top, outside the namespace, `System` first.

**Language use**
- Use records for immutable data (`Order`) and classes for objects with identity or behavior (`Stock`, `Portfolio`).
- Prefer `var` when the type is obvious from the right-hand side, explicit types otherwise. Use target-typed `new()` and collection expressions (`[]`) where they read well.
- Prefer pattern matching and `is null` / `is not null`. Use string interpolation over concatenation.
- Validate arguments with the framework helpers: `ArgumentNullException.ThrowIfNull`, `ArgumentOutOfRangeException.ThrowIfNegativeOrZero`, `ArgumentException.ThrowIfNullOrWhiteSpace`.
- Use C# 14 features (such as the `field` keyword in properties) only where they make the code simpler. Do not use them for their own sake.
- Expose collections as `IReadOnlyDictionary` / `IReadOnlyList`, never the mutable type.
- Use `decimal` for money and quantities (D3, D5). Never `double`.

**Documentation**
- Simple XML docs (`<summary>`, plus `<param>`, `<returns>`, `<exception>` where they add information) on public types and members.
- Inline comments only where the intent is not clear from the code.

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
