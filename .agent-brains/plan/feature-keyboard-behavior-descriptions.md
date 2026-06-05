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
- [ ] Create branch `feature/keyboard-behavior-descriptions` from main
- [ ] Commit pending backlog housekeeping (dropped items 2–4) as its own `chore:` commit on this branch
- [ ] `KeyBehaviorOption` record (`Value`, `Label`, `Description`) in App ViewModels;
      replace `MapToKeyboardBehaviors` string list with option list (keep static, shared
      by both pages via `ControllerSetupPageViewModel.MapToKeyboardBehaviors` passthrough)
- [ ] `BindingsPageViewModel`: expose `EditMapToKeyboardBehaviorDescription` computed
      property (raises with `EditMapToKeyboardBehavior`) for the caption
- [ ] `BindingsPageView.axaml`: ComboBox → `SelectedValue` binding + two-line
      `ItemTemplate` (Label + dim wrapped Description, `x:DataType`) + single-line
      `SelectionBoxItemTemplate` + caption TextBlock below
- [ ] `ControllerSetupPageView.axaml`: same treatment (binds via `BindingEditor.…`)
- [ ] Optional polish: `BoundActionViewModel` summary (line ~116) shows spaced label
      (`[Press Only]`) instead of raw enum name
- [ ] Tests: option list covers every `KeyBehavior` enum value exactly once; values
      parse via `Enum.TryParse`; description property follows selection
- [ ] `dotnet build` 0 warnings + full test suite green (baseline 355)
- [ ] Run app, visually verify both pages (dropdown, caption, save/reload round-trip)

## Progress Log
- 2026-06-05 — Plan created after discussion reversed the drop-behaviors proposal;
  UX variant "Both" confirmed by user; Avalonia 12.0.1 API availability verified.
