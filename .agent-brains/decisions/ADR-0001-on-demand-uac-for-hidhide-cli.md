# ADR-0001: On-demand UAC elevation for HidHide CLI writes

**Date:** 2026-05-25
**Status:** accepted
**Deciders:** DevilDogTG

## Context

HidHide device-hiding integration requires write operations (whitelist app, block/unblock devices)
that the driver only permits from an elevated process. The app previously used `requireAdministrator`
in its manifest, prompting UAC at every launch regardless of whether any HidHide writes were needed.

After a read-first/write-only-if-needed optimisation, the app rarely needs to write to HidHide after
the first launch. Forcing the user to approve a UAC prompt every time — even when the app would do
nothing privileged — was unnecessary friction.

## Options Considered

### Option A: Keep `requireAdministrator` manifest

The app always runs elevated. Simple — no special elevation logic needed.

- **Pros:** Straightforward; HidHide CLI always succeeds.
- **Cons:** UAC prompt at every launch even when no elevated operation is performed. User cannot
  run the app conveniently from a non-admin account. Fails Windows security best-practice "request
  least privilege".

### Option B: `asInvoker` + re-launch whole app elevated when needed

Change manifest to `asInvoker`. When a HidHide write fails due to insufficient privilege,
restart the entire app with `ShellExecute runas`.

- **Pros:** App normally runs non-elevated.
- **Cons:** Restarting the app loses all in-memory state (loaded profile, device state, pipeline
  status). Jarring UX. Overkill — only the CLI subprocess needs elevation, not the whole app.

### Option C: `asInvoker` + subprocess elevation on demand (chosen)

Change manifest to `asInvoker`. `HidHideCliFallback.Run()` first attempts CLI with the inherited
(non-elevated) token. If the OS returns `ERROR_ELEVATION_REQUIRED` (740), the same CLI is
re-launched via `ShellExecute runas`, which shows a Windows UAC dialog just for that subprocess.
If the user cancels (`ERROR_CANCELLED` 1223), `HidHideElevationCancelledException` is thrown and
the caller skips the operation gracefully.

- **Pros:** App runs non-elevated by default. Elevation is scoped to a single short-lived CLI
  subprocess. User can decline without breaking the rest of the app. Paired with read-first checks,
  the UAC prompt only appears on first-ever setup (exe not yet whitelisted).
- **Cons:** Slightly more complex CLI invocation path; cannot capture stderr when using
  `UseShellExecute = true`.

## Decision

Use `asInvoker` manifest and request elevation on-demand only for HidHide CLI subprocesses
(Option C), so the app runs unprivileged by default and the UAC prompt appears only when a write
to the HidHide driver is actually needed.

## Consequences

**Easier:** Normal debugging and daily use without repeated UAC prompts. Aligns with Windows
least-privilege best practice.

**Harder:** Error messages from the CLI are unavailable when running elevated (`UseShellExecute`
blocks stream redirection). Exit-code-only error reporting in the elevated path.

**Follow-up:** None — implementation complete as of commit `84178048`.
