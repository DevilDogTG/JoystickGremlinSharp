# Plan: Winget Distribution

**Status:** pending (queued — not active; feature/version-checker is the active plan)
**Created:** 2026-06-05
**Branch:** chore/winget-publish (CI part only; bootstrap is outside the repo)

## Goal
`winget install DevilDogTG.JoystickGremlinSharp` works, and every release published by
`publish.yml` automatically opens a manifest-update PR to microsoft/winget-pkgs so
`winget upgrade` picks up new versions without manual steps.

## Why It Fits Already (verified 2026-06-05 against publish.yml + Package.wxs)
- Permanent versioned asset URL: `releases/download/vX.Y.Z/JoystickGremlinSharp-X.Y.Z-Setup.msi`
- MSI = silent-installable by definition; `perMachine`/HKLM ARP entry + stable
  `UpgradeCode` {3BE7219A-DAE0-41D4-BDB3-E0530808F9C3} → `winget upgrade` works natively
- MSI is Authenticode-signed in CI → passes winget validation & SmartScreen
- 3-part semver from version.json drives MSI ProductVersion → clean version matching

## Checklist

### Phase 1 — One-time bootstrap (manual, no repo changes)
- [ ] Decide PackageIdentifier: `DevilDogTG.JoystickGremlinSharp` (publisher.package)
- [ ] Run `wingetcreate new <msi-url>` against the latest release; fill locale metadata
      (publisher, license MIT?, short description, homepage, release-notes URL)
- [ ] Submit initial PR to microsoft/winget-pkgs; respond to moderation feedback
- [ ] Verify after merge: `winget search joystickgremlin` / `winget install DevilDogTG.JoystickGremlinSharp`

### Phase 2 — Per-release automation (repo changes)
- [ ] Create classic PAT (`public_repo` scope) → repo secret `WINGET_TOKEN`
- [ ] Add `Publish to winget` step at end of publish.yml: `wingetcreate update ... --submit`
      (guard with `if: ${{ secrets.WINGET_TOKEN != '' }}`-style check so missing secret
      doesn't fail the release, mirroring the signing-step pattern)
- [ ] Update workspace `release` skill docs + README install section
      (`winget install` as recommended path)
- [ ] Verify end-to-end on the next real release: winget-pkgs PR auto-opened,
      validation passes, auto-merged

## Notes / Risks
- First submission requires a human moderator pass (days, not hours) — bootstrap early.
- `wingetcreate` extracts per-version ProductCode + SHA256 from the MSI automatically;
  WiX auto-generated ProductCode per build is expected and fine.
- Alternative automation: `vedantmgoyal9/winget-releaser` action or Komac — both wrap
  the same flow; plain `wingetcreate update` keeps publish.yml self-contained.
- Synergy: in-app version checker (active plan) can suggest
  `winget upgrade DevilDogTG.JoystickGremlinSharp` in its update dialog once live.

## Progress Log
- 2026-06-05 — Plan created from feasibility discussion; feasibility confirmed against
  publish.yml (asset naming, signing) and installer architecture (UpgradeCode, perMachine).
