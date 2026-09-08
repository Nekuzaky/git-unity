# Changelog

All notable changes to this package are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project adheres to [Semantic Versioning](https://semver.org/).

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
