# Plan: Keyboard Behavior Descriptions

**Status:** active
**Created:** 2026-06-05
**Branch:** feature/keyboard-behavior-descriptions

## Goal
An inexperienced user opening the map-to-keyboard config can tell from the UI alone
which behavior (Hold / Toggle / Press Only / Release Only) fits their use case —
two-line descriptions in the open dropdown + a caption under the closed combo.

## Background
- Session 2026-06-05: proposal to drop Toggle/PressOnly/ReleaseOnly was challenged and
  reversed — the latched-switch case (PressOnly + ReleaseOnly on 2-position HOTAS
  switches) and Toggle (latching a momentary button) justify all behaviors. Decision:
  keep all four, fix discoverability instead.
- Current UI: bare `ComboBox` over plain strings in two views —
  `BindingsPageView.axaml:324` and `ControllerSetupPageView.axaml:636`, both fed by
  `BindingsPageViewModel.MapToKeyboardBehaviors` (static `IReadOnlyList<string>`).
- UX decision (user-confirmed): **Both** — two-line dropdown items AND selected-item
  caption below the combo.
- Avalonia 12.0.1: `SelectedValue`/`SelectedValueBinding` + `SelectionBoxItemTemplate`
  available. Compiled bindings on by default → templates need `x:DataType`.

## Constraints
- **No profile format change**: persisted JSON value stays the enum name string
  (`"Hold"`, `"Toggle"`, `"PressOnly"`, `"ReleaseOnly"`);
  `EditMapToKeyboardBehavior` remains a `string`.
- UI copy lives in the App layer (Core enum + XML docs untouched).

## Approved copy
| Value | Label | Description |
|---|---|---|
| Hold | Hold | Keys stay pressed while the button is held, released when you let go. Standard choice for normal (momentary) buttons. |
| Toggle | Toggle | Each press flips the keys between pressed and released. Latches a state from a momentary button (e.g. push-to-talk → mic on/off). |
| PressOnly | Press Only | Sends one quick key tap when the button is pressed; nothing on release. Use for 2-position switches so a key isn't held down forever. |
| ReleaseOnly | Release Only | Sends one quick key tap when the button is released. Pair with Press Only on the same input to send different keys on switch flip-up vs flip-down. |

## Checklist
- [x] Create branch `feature/keyboard-behavior-descriptions` from main
- [x] Commit pending backlog housekeeping (dropped items 2–4) as its own `chore:` commit on this branch
- [x] `KeyBehaviorOption` record (`Value`, `Label`, `Description`) in App ViewModels
      with static `All` list; both pages share it via the existing
      `ControllerSetupPageViewModel.MapToKeyboardBehaviors` passthrough
- [x] `BindingsPageViewModel`: `EditMapToKeyboardBehaviorDescription` computed
      property (raised from the `EditMapToKeyboardBehavior` setter) for the caption
- [x] `BindingsPageView.axaml`: ComboBox → `SelectedValue` binding + two-line
      `ItemTemplate` (Label + dim wrapped Description, `x:DataType`) + single-line
      `SelectionBoxItemTemplate` + caption TextBlock below
- [x] `ControllerSetupPageView.axaml`: same treatment (binds via `BindingEditor.…`)
- [x] Polish: `BoundActionViewModel` summary shows spaced label (`[Press Only]`)
- [x] Tests: ~~App-level option tests~~ — no App test project exists (UI layer untested
      by convention); enum coverage is enforced at compile time instead: option values
      use `nameof(MapToKeyboardActionDescriptor.KeyBehavior.…)`, so removing/renaming
      an enum member breaks the build
- [x] `dotnet build -warnaserror` 0 warnings + full test suite green (355/355)
- [x] Run app, visually verify both pages (dropdown, caption, save/reload round-trip)
      — user confirmed 2026-06-05: "visuality is good as expected"

## Review Round 1 (PR #77) — all findings fixed
- [x] WARNING: behavior string canonicalized at form-load via `Enum.TryParse`
      (ignoreCase) → enum name, mirroring the functor's Hold fallback
- [x] STYLE: caption property converted to `ObservableAsPropertyHelper` driven by
      `WhenAnyValue`; setter back to plain `RaiseAndSetIfChanged`; OAPH disposed
- [x] STYLE: duplicated picker XAML lifted into shared `Views/KeyBehaviorPicker`
      UserControl (BindingsPageView inherits DataContext; ControllerSetupPageView
      passes `BindingEditor`); unused `ControllerSetupPageViewModel.MapToKeyboardBehaviors`
      passthrough removed

## Progress Log
- 2026-06-05 — Plan created after discussion reversed the drop-behaviors proposal;
  UX variant "Both" confirmed by user; Avalonia 12.0.1 API availability verified.
- 2026-06-05 — Implemented: `KeyBehaviorOption` (record + `All`), VM description
  property, both XAML pickers reworked (two-line items + caption, `SelectedValue`
  keeps the persisted string untouched — no profile format change), summary label
  polish. Build 0 warnings, 355/355 tests. Pending: visual verification in the app.
