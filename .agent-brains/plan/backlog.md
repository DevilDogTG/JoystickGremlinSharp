# Project Roadmap

## Completed

- [x] **feature/autoload-rework** — Auto-load process→profile rework *(released v10.6.0)*
- [x] **feature/wix-installer** — Replace Velopack with WiX v6 MSI installer *(PR #62, ready to merge)*

  - [x] Remove Velopack NuGet + `VelopackApp.Build()` from app
  - [x] WiX v6 `.wixproj` + `Package.wxs` with wizard UI, path selection, optional desktop shortcut
  - [x] Update `build-installer.ps1` + `publish.yml` CI
  - [x] Code review findings resolved (2 warnings, 2 style)
  - [x] Re-review verdict: Approved

## Active

- [ ] **fix/startup-crash-dill-cwd** — Non-admin users crash at launch after MSI install because `dill.dll` writes `dill_debug.log` to CWD. See `fix-startup-crash-dill-cwd.md`.

## Backlog (Optional Features)

- [ ] **In-app GitHub Releases version checker** — check latest release, compare semver, show download link (no auto-install)
- [ ] Response curve editor (axes)
- [ ] Condition-based action pipeline
- [ ] UI for button mapping configuration
