# Changelog

All notable changes to this package are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project adheres to [Semantic Versioning](https://semver.org/).

## [0.6.1] - 2026-09-11

### Fixed

- Icons did not render at all and logged "Not allowed to override geometry on sprite"
  once per icon per frame. VectorUtils.BuildSprite builds a Sprite and calls
  Sprite.OverrideGeometry, which Unity 6 refuses; the artwork is now tessellated straight
  into a Mesh with VectorUtils.FillMesh and drawn into a render texture, with no Sprite in
  the path.
- Rasterising moved off the GUI callback onto a delayCall, so mesh drawing no longer runs
  inside the IMGUI repaint. A slot that is not ready yet falls back to its glyph for that
  frame, and the window repaints once the texture lands.
- A failed rasterisation is remembered instead of retried on every repaint, which is what
  turned one broken icon into a console flood.
- Sidebar section headers drew the fold caret and a leftover Unicode glyph side by side.
- Collapsing a sidebar section did not shrink the scroll view: the collapse key carried
  the glyph prefix and never matched the key used to measure the section height.
- The console header lost its expand/collapse caret when it was wired to the icon set.

## [0.6.0] - 2026-09-11

### Added

- Bootstrap Icons artwork, embedded as SVG path data and rasterised through the built-in
  Vector Graphics module. Icons are rendered white and tinted at draw time, so one texture
  serves both themes. MIT licensed, see THIRD-PARTY-NOTICES.md.
- Automatic fallback to the Unicode glyphs when the Vector Graphics module is absent
  (Unity 2021.3 and 2022), driven by a versionDefine in the asmdef. The package keeps its
  2021.3 minimum.

### Fixed

- The console could be laid out over the footer in a short window. The console now takes
  only what is left after the body keeps 40 px, so the panes can never reach past the
  footer; verified across every window height from the chrome minimum to 1200 px.
- History columns dropped in the wrong order: the author column disappeared first even
  though the documentation promised right-to-left. SHA now goes first, then the date, then
  the author.
- The commit box vanished without explanation whenever the file pane fell below about
  102 px, leaving no way to commit. It now keeps a minimum and sheds its bulk-staging row
  instead, since the per-file buttons cover that.
- The sidebar toggle claimed to be on while the sidebar was auto-hidden below 660 px.
  It now reports the real state and is disabled, with a tooltip saying why.
- Dynamic fonts were recreated on every theme toggle and never destroyed. They are cached
  per size and survive style rebuilds.

## [0.5.0] - 2026-09-11

### Added

- Footer bar: current branch, commit and change counts, repository path, and a discreet
  Buy Me a Coffee link that opens only on a click.
- Sidebar toggle in the toolbar, remembered per user.
- Overflow menu holding whatever the toolbar drops at narrow widths, so no action ever
  becomes unreachable.
- Empty states for the history and the file list instead of blank panes.

### Changed

- Everything is one step larger: 25 px history rows, 22 px sidebar rows, 25 px toolbar,
  17 px diff lines, and body text at 12 px instead of 11.
- Footer enlarged to 26 px with 11 px text, and the Buy Me a Coffee link sits in a pill
  that lights up under the cursor.
- Real emoji where one exists in the Basic Multilingual Plane: pull and push arrows, the
  pencil on Changes, the warning sign on conflicts, and the coffee cup in the footer.
  Emoji outside the BMP stay excluded - IMGUI addresses glyphs per UTF-16 code unit, so a
  surrogate pair renders as two blanks. Emoji fonts were added to the fallback chain and
  render monochrome, since IMGUI ignores COLR/CPAL colour layers.
- The layout is now responsive. The sidebar folds away below 660 px, toolbar buttons lose
  their labels below 900 px, the search field moves into the overflow menu below 780 px,
  and the detail pane stacks the file list above the diff below 560 px. History columns
  drop from the right as the pane narrows: SHA first, then date, then author.
- Minimum window size lowered from 760x460 to 360x300, so the window can be docked in a
  narrow column.
- Pane headers carry a hairline separator, and the commit box shrinks rather than pushing
  the file list out of view.

### Fixed

- Vertical layout could hand negative heights to the scroll views when the window was
  shorter than its minimum, which happens with some docking arrangements. The console now
  collapses first, and the detail pane stops before drawing into a negative rect.

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
