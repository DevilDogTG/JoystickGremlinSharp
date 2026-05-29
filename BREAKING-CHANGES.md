# Breaking Changes

## v11.0.0 — 2026-05-29

### Auto-load triggers moved into each profile

The global process-to-profile mapping list (`AppSettings.ProcessMappings` in
`settings.json`) has been removed. Each profile now owns its own list of
process triggers via `Profile.AutoLoadTriggers`.

**What changes for users**
- Existing auto-load mappings configured under v10.x are not migrated. On first
  launch of v11.0, the Auto-load page will appear empty.
- Recreate your triggers on the Auto-load page: each profile now expands into
  its own triggers list. A trigger attached to a profile activates that
  profile when the matched executable becomes the foreground window.
- Sharing or copying a profile JSON file now carries its triggers along —
  there is no separate list to keep in sync.

**What changes for files on disk**
- `%APPDATA%\JoystickGremlin\settings.json` (legacy folder name) is no longer
  read or written. The new path is
  `%APPDATA%\JoystickGremlinSharp\settings.json`, matching the existing
  `%APPDATA%\JoystickGremlinSharp\profiles\` root.
- The legacy file at `%APPDATA%\JoystickGremlin\settings.json` is left
  untouched; users who downgrade keep their old mappings.
- Profile JSON files gain an `AutoLoadTriggers` array property.

**What changes for developers / contributors**
- `JoystickGremlin.Core.Configuration.ProcessProfileMapping` is gone. The
  replacement type is `JoystickGremlin.Core.Profile.ProcessTrigger`
  (no `ProfilePath` field — the trigger lives inside its target profile).
- `JoystickGremlin.Core.Configuration.ProcessMatchType` has moved to the
  `JoystickGremlin.Core.Profile` namespace.
- `ProcessProfileResolver.Resolve(string, IEnumerable<ProcessProfileMapping>)`
  is replaced by
  `ProcessProfileResolver.Resolve(string, IEnumerable<ProfileEntry>)` which
  returns a `ProcessTriggerMatch` pairing the profile and the matching
  trigger.
- `ProfileEntry` is now `record(Name, Category, FilePath, AutoLoadTriggers)` —
  the new positional `AutoLoadTriggers` parameter is the snapshot read from
  the JSON file during the latest library scan.
- `AppSettings.EnableAutoLoading` is retained as a global kill-switch.
