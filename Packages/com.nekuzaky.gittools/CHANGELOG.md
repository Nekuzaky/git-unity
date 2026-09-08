# Changelog

All notable changes to this package are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project adheres to [Semantic Versioning](https://semver.org/).

## [0.4.0] - 2026-09-08

### Added

- Dedicated dark theme, on by default: the window paints its own Fork-like palette instead
  of inheriting the editor skin. A toolbar toggle switches back to a light palette, and the
  choice is remembered per user.
- Unicode glyphs throughout: toolbar actions, sidebar sections, branch, remote, tag and
  stash markers, ahead/behind counters, fold arrows. All of them are declared in `GitIcons`
  and can be swapped in one place.
- Letter badges (A/M/D/R/U) on file rows, replacing the coloured dots.
- Accent bar on the selected row.

### Changed

- Glyph-bearing labels are rendered through a symbol-capable font chain rather than the
  editor font. On Windows the editor font resolves to Segoe UI, which covers only 7 of the
  20 glyphs used; the other 13 would have rendered as empty boxes.
- Colour palette reworked around a single set of named tokens, including per-status colours.

### Note

Colour emoji are deliberately not used: Unity's IMGUI draws text with a dynamic font that
has no COLR/CBDT support, so they render as blank boxes. The glyphs chosen are BMP symbols
verified as present in the fallback fonts.


## [0.3.0] - 2026-09-08

### Changed

- The whole interface is now in English.
- The dashboard moved back to `Tools > Git` and keeps the `Ctrl+Shift+G` shortcut.

### Removed

- The "quick panel" window. The dashboard covers everything it did.

## [0.2.0] - 2026-09-08

### Added

- Dashboard window: a full desktop-client style view of the repository.
- Commit graph with coloured lanes, hollow nodes for merges, branch and tag badges.
- Sidebar listing local branches (with ahead/behind counters), remote branches, tags and stashes.
- Context menus: switch, merge, rename, delete, cherry-pick, revert, reset, tag, stash.
- Detail pane showing the files of a commit, or the staging area of the working tree.
- Colourised, virtualised diff; history search; resizable panes remembered between sessions.

### Fixed

- Local branch parsing: `git for-each-ref` does not expand `%x1f` the way `git log` does,
  so the separator was printed literally and upstream and ahead/behind data were lost.
  The correct escape there is `%1f`.

## [0.1.0] - 2026-09-08

### Added

- Git window with the `Ctrl+Shift+G` shortcut.
- Staging and unstaging per file or in bulk, discarding changes.
- Commit with an `Amend` option, and `Commit & Push` in one action.
- Fetch, Pull, Push, creating the upstream automatically on the first push.
- Branch switching, branch creation, `--no-ff` merges.
- Conflict detection, `git mergetool` invocation and merge abort.
- Diff of the selected file, index side or working tree side.
- Console showing every git command and its raw output.
- Automatic status refresh every 5 seconds, toggleable.
