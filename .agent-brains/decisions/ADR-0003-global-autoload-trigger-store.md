# ADR-0003: Global auto-load trigger store in settings.json

**Date:** 2026-06-05
**Status:** accepted
**Deciders:** Supawat Tanmanee (change request, session 2026-06-05); PR #71

## Context

v11.0 (ADR-less, PR #66) embedded auto-load process triggers inside each profile's
JSON (`Profile.AutoLoadTriggers`) so sharing/deleting a profile carried its triggers.
In practice this fragmented trigger management (one expander group per profile),
forced a per-file JSON read on every library scan (deferred perf item from PR #66
review), and tied trigger priority to library scan order (alphabetical file paths)
rather than user intent. A change request asked for a return to centrally managed
triggers without repeating the v11.0 hard break.

## Options Considered

### Option A: v10.x-style global mapping only (full revert)
Plain process→profile map in settings.json.
- **Pros:** simplest model; single management surface.
- **Cons:** loses per-trigger options shape (AutoStart/StayActive ordering semantics);
  another hard break for v11/v12 users.

### Option B: Global + keep per-profile triggers (layered)
Global list supplements/overrides profile-embedded triggers.
- **Pros:** no migration needed.
- **Cons:** two sources of truth, ambiguous precedence, double UI; worst maintenance.

### Option C: Global trigger store, profile-referencing (chosen)
One ordered list in `AppSettings.AutoLoadTriggers`; each trigger carries
`ProfilePath` + all per-trigger options; profile files no longer carry triggers.
- **Pros:** single source of truth; user-controlled priority (list order, first
  enabled match wins); kills the per-file scan read; lossless auto-migration possible.
- **Cons:** resurrects the path back-pointer (rename/move breaks the reference —
  mitigated with ⚠ unresolved marker + re-point via dropdown); needs migration
  machinery.

## Decision

Adopt Option C: triggers move to a global, ordered, profile-referencing list in
`settings.json`, with idempotent lossless migration (settings saved BEFORE profile
files are stripped; dedupe on retry) run automatically at startup and on demand from
the Auto-load page. Ships as minor v12.1.0 because migration is automatic.

Profile reference is by **file path** (not `Profile.Id` GUID) for consistency with
`ActiveProfilePath`/`ProfileEntry.FilePath` conventions.

Concurrency convention: `AppSettings.AutoLoadTriggers` (list AND elements) is
**replace-only** — the process monitor enumerates it from a non-UI thread; writers
swap a freshly built list, never mutate published instances.

## Consequences

**Easier:** at-a-glance trigger management + explicit priority; faster library scans
(no trigger read); trigger logic isolated in `Core.ProcessMonitor`
(`AutoLoadTrigger`, `ProcessProfileResolver`, `AutoLoadTriggerMigrator`).
**Harder:** profiles are no longer self-contained for sharing (the v11 benefit) —
shared profiles from v11/v12.0 still migrate via the page banner; renamed/moved
profiles need re-pointing.
**Follow-up:** none open — the ScanCore-async backlog item was closed as moot;
path-fixup-on-rename is a possible future enhancement if users hit ⚠ often.
