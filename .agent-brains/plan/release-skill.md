# Plan: Workspace Release Skill + Curated Release Notes

**Status:** active
**Created:** 2026-06-05
**Branch:** feature/release-skill

## Goal
One-command release flow: `sk-release` analyzes merged work since the last tag,
recommends a semver bump (user confirms), generates curated release notes, and drives
this repo's existing automation (release/ PR → tag.yml → publish.yml) — with
publish.yml switched from auto-generated notes to the curated committed file.

## Decisions (user-confirmed 2026-06-05)
- Skill lives at workspace level: `.agent-brains/skills/release/release.md` (id `release`).
  Profile `gh-release` stays generic — its manual-tag flow must NOT be used in this repo
  (would race tag.yml / collide with publish.yml's release step).
- Notes source: `RELEASE-NOTES.md` committed at repo root by the release PR;
  `publish.yml` reads it via `body_path` instead of `generate_release_notes: true`.
  Fallback step writes a minimal notes file when absent so old/hotfix tags can't fail.

## Checklist
- [ ] `.agent-brains/skills/release/release.md` — procedure: preflight → analyze
      commits/PRs since last tag → recommend+confirm bump → align version.json →
      write RELEASE-NOTES.md → release/vX.Y.Z PR → confirm merge → watch
      tag.yml/publish.yml → verify tag, release, MSI asset
- [ ] `publish.yml`: `body_path: RELEASE-NOTES.md` + "ensure notes file" fallback step
- [ ] PR + review (workflow file is load-bearing)

## Risks
- publish.yml change is unverifiable until the next real tag — flag prominently in the
  PR and verify during the v12.1.0 release (first consumer).

## Progress Log
_2026-06-05 — Plan created from release-flow discussion; decisions captured._
