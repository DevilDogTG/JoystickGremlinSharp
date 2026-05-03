---
name: code-review
description: >
  Perform expert code reviews of C#, Avalonia XAML, and .NET code. Use this skill
  whenever a user asks for a review, audit, critique, or feedback on C# or Avalonia code.
  This skill applies the expertise of a senior C#/.NET engineer to catch architectural
  flaws, reactive binding traps, threading violations, memory leaks, and style issues — not
  just surface syntax errors.
---

Review the changes on this branch compared to `main` using the instructions below.

If the review is for an existing PR, do not stop at local analysis: after producing the review,
publish it to GitHub as a PR review, then after fixes are applied publish a follow-up PR comment
summarizing what was fixed and the resulting review status.


# C# & Avalonia Code Review Skill

You are performing a structured code review as a senior C#/.NET engineer specialized in
Avalonia MVVM. Your job is to find real problems and give direct, actionable feedback —
not to be polite or vague.

## Step 0 - Project specifications
- **Runtime**: .NET 10
- **UI Framework**: Avalonia 11.x with ReactiveUI MVVM
- **Target platform**: Windows (vJoy/DILL drivers)
- **Architecture intent**: Clean separation — Core (domain logic, no UI), Interop (P/Invoke wrappers),
  App (Avalonia MVVM presentation)


## Step 1 — Structured Review

Work through the code in this fixed order. For each category, list findings as
**[CRITICAL]**, **[WARNING]**, or **[STYLE]**:

- **CRITICAL** = will cause crashes, data loss, threading violations, or incorrect
  behavior at runtime
- **WARNING** = will cause performance issues, memory leaks, binding loops,
  or maintenance problems
- **STYLE** = deviates from C# idioms or team standards; fix when touching the file

### 2.1 Architecture & Separation of Concerns

Check:
- [ ] Business logic leaking into ViewModels (should be in Core services)
- [ ] Views or ViewModels with direct P/Invoke or driver calls (should go through Interop)
- [ ] Complex ObservableCollections that should use ReactiveList or custom model
- [ ] ViewModels directly setting View.DataContext or manipulating visual trees by name (fragile)
- [ ] Missing DI registration for services used across ViewModels

### 2.2 Threading & Safety

Check:
- [ ] UI property modifications from background threads without `Dispatcher.UIThread.Post`
- [ ] `async void` except for event handlers (breaks exception handling)
- [ ] `CancellationToken` not threaded through async call chains
- [ ] No null-guard before dereferencing results of async operations
- [ ] Mutable shared state accessed from multiple threads without locks

### 2.3 Memory & Object Lifetime

Check:
- [ ] Observable collections holding stale references after clear/rebuild
- [ ] Event subscriptions not unsubscribed (leak if ViewModel outlives listener)
- [ ] Large objects cached in ViewModels without size limits
- [ ] Circular references between services and ViewModels

### 2.4 ReactiveUI Patterns & Bindings

Check:
- [ ] Binding loops: property A `WhenAnyValue` depends on B, B depends on A
- [ ] `RaiseAndSetIfChanged` not used consistently (breaks binding change detection)
- [ ] `WhenAnyValue` subscriptions without proper cleanup (`Dispose` or subscription disposal)
- [ ] Commands with no canExecute guard (always enabled, potential state corruption)
- [ ] ObservableCollection modified while bindings are evaluating (thread safety)

### 2.5 XAML & Binding Correctness

Check:
- [ ] Hard-coded layout values instead of theme tokens or relative sizing
- [ ] Binding paths without fallback (null reference if DataContext missing)
- [ ] `{Binding}` in ListBox items without proper `DataTemplate` (loses context)
- [ ] Unnecessary `IsVisible` bindings that should use `IsEnabled` + opacity
- [ ] ListBox/ItemsControl items that should be virtualized (performance)

### 2.6 C# & .NET Correctness

Check:
- [ ] Mutable value types passed by reference unintentionally
- [ ] String comparisons not using `StringComparison.Ordinal` (culture bugs)
- [ ] Null-dereference after null-coalescing without re-guard (e.g., `x?.Property` then `x.Property`)
- [ ] `async Task` methods not awaited (fire-and-forget bugs)
- [ ] `IDisposable` not properly implemented (unmanaged resources leak)
- [ ] LINQ `.First()` / `.Single()` without guard (crashes on empty)


## Step 3 — Summary Table

After the findings, emit a compact summary:

```
| Category                      | CRITICAL | WARNING | STYLE |
|-------------------------------|----------|---------|-------|
| Architecture & Separation     |    0     |    2    |   1   |
| Threading & Safety            |    1     |    0    |   0   |
| Memory & Lifetime             |    0     |    1    |   0   |
| ReactiveUI & Bindings         |    0     |    1    |   1   |
| XAML & Binding Correctness    |    0     |    1    |   2   |
| C# & .NET Correctness         |    0     |    1    |   0   |
```

Then: **Overall verdict** — one sentence, honest.


## Step 4 — Top 3 Fixes

Pick the three highest-impact issues and show corrected code side-by-side with the
original. Label clearly:

```csharp
// ❌ Before:
// (original code with issue)

// ✅ After:
// (corrected code)
```

Always include complete imports in C# snippets. Always include the surrounding
context (full method or property in ViewModels, full XAML element for bindings) so
the fix is unambiguous.


## Step 5 — Refactor Suggestions (optional, if warranted)

If the code has systemic architectural problems that a fix-by-fix patch won't resolve,
describe a refactor path:
- What the target architecture looks like
- Which ViewModels/Services to split or merge
- What the migration order should be (least disruptive first)

Only include this section if there are ≥2 CRITICAL findings or the architecture is
fundamentally inverted (logic in View, data in ViewModel without encapsulation, etc.).

## Step 6 — Publish Review To GitHub

When a PR number is available, publish the review result instead of leaving it only in terminal output:

1. Choose the GitHub review state from the findings:
   - **APPROVE**: no blocking findings remain
   - **COMMENT**: findings are advisory or informational only
   - **REQUEST_CHANGES**: one or more blocking findings remain
2. Submit the review to the PR with a concise summary of the findings
3. Verify the PR now shows the submitted review state

If fixes are made after the initial review:

1. Re-review the updated diff
2. Post a PR comment with a **fix summary**:
   - what issues were fixed
   - what issues, if any, still remain
   - the resulting final status (approve / comment / request changes)
3. Update the PR review status if the verdict changed after the fixes


## Tone & Style Rules

- Be direct. "This will deadlock" beats "this might potentially have threading concerns."
- Show the corrected code, don't just describe the fix.
- Reference C# / Avalonia docs by section name so the team can self-serve:
  e.g., *Avalonia Documentation → Reactive Bindings*, *ReactiveUI → WhenAnyValue*
- If something is genuinely fine, say so. Don't pad findings.
- If you need to assume a .NET version or library version, state it once at the top of the review.
- A review is not complete until it is published to the PR when a PR exists.
