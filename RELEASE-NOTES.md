## What's new

- **Global auto-load triggers** (#71) — process triggers now live in one central list
  (Settings → Auto-load) instead of inside each profile file. The new page shows every
  trigger in a single table with a profile picker per row: pick the profile, pick the
  application (running process or `.exe` path), set per-trigger options (AutoStart,
  Stay Active, enable/disable), and order rows by priority — the first enabled match
  wins, top to bottom.
- **Automatic trigger migration** (#71) — triggers embedded in v11.x/v12.0 profiles are
  lifted into the global list automatically on first start. Profile files shared from
  older installations and copied in later are detected on the Auto-load page, which
  offers a one-click **Migrate now**.
- Faster profile library scans — profile files are no longer parsed for triggers on
  every scan (#71).
- Release notes are now curated per release instead of an auto-generated PR list (#72).

## Bug fixes

- None affecting v12.0.1 functionality (fixes in this cycle applied to the new
  auto-load feature before release).

## Breaking changes

None. Existing per-profile triggers migrate automatically and losslessly; all
per-trigger options and priority order are preserved.

## Migration guide

Nothing to do — on first launch of v12.1.0, embedded auto-load triggers move from your
profile files into `settings.json` automatically. If you later copy in a profile from an
older installation, open the Auto-load page and click **Migrate now** when the banner
appears. Note: a trigger references its profile by file path, so if you move or rename a
profile, re-point the trigger via its profile dropdown (a ⚠ marker shows next to
unresolved entries).
