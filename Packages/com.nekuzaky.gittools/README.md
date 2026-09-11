# Git Tools

A Git client embedded in the Unity editor. Stage, commit, push, pull, branch and merge
without ever leaving the engine.

The package drives the `git` executable installed on the machine: no native DLL is
bundled, and Git LFS and the system credential manager keep working as they already do.

## Installation

### From a git URL (Package Manager)

`Window > Package Manager > + > Install package from git URL…` then:

```
https://github.com/Nekuzaky/git-unity.git?path=/Packages/com.nekuzaky.gittools
```

To pin a version, append a revision:

```
https://github.com/Nekuzaky/git-unity.git?path=/Packages/com.nekuzaky.gittools#v0.6.1
```

### From `manifest.json`

```json
{
  "dependencies": {
    "com.nekuzaky.gittools": "https://github.com/Nekuzaky/git-unity.git?path=/Packages/com.nekuzaky.gittools"
  }
}
```

### Locally

Copy the folder into the project's `Packages/`, or add it through
`Package Manager > + > Install package from disk…` pointing at `package.json`.

## Requirements

- Unity 2021.3 or newer.
- `git` 2.23+ on the `PATH` (the `git restore` command is used).
- A Git repository initialised at the project root — the folder containing `Assets/`.

## Usage

`Tools > Git`, or `Ctrl+Shift+G` (`Cmd+Shift+G` on macOS).

The window is split into resizable panes; their sizes are remembered between sessions.

| Pane | Contents |
| --- | --- |
| Toolbar | Sidebar toggle, Refresh, Fetch, Pull, Push, branch creation, Stash menu, dark-theme toggle, search filter, history depth, and an overflow menu holding whatever the width forced out |
| Sidebar | Current branch and its tracking state, pending changes, local branches (with ahead/behind counters), remote branches, tags, stashes |
| Graph | History with coloured lanes, filled nodes for commits and hollow ones for merges, branch and tag badges, author, relative date, SHA |
| Detail | Files of the selected commit, or the staging area when the "Uncommitted changes" row is selected |
| Diff | Colourised diff of the selected file, with virtualised scrolling |
| Git console | Every command that ran, with its raw output |
| Footer | Current branch, commit and change counts, repository path |

Double-clicking a branch checks it out. Clicking one scrolls the graph to its tip.
Right-clicking opens a context menu:

- **local branch** — switch, merge into the current branch, rename, delete (offering to force
  when the branch is not fully merged);
- **remote branch** — check out as a local tracking branch, merge, delete on the remote;
- **commit** — copy the SHA or message, create a branch or tag here, check out, cherry-pick,
  revert, `reset --mixed` or `reset --hard`;
- **tag** — delete, push;
- **stash** — apply, apply and drop (`pop`), delete.

Selecting a file also highlights it in the Project window when it lives under `Assets/`.

Anything that touches the disk (pull, checkout, merge, discard) triggers an
`AssetDatabase.Refresh()`.

## Scene and prefab conflicts

The conflict banner offers a **Resolve (UnityYAMLMerge)** button that calls `git mergetool`.
The tool has to be declared once in the repository:

```bash
git config merge.tool unityyamlmerge
git config mergetool.unityyamlmerge.trustExitCode false
git config mergetool.unityyamlmerge.keepBackup false
git config mergetool.unityyamlmerge.cmd '"<Unity>/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"'
```

Replace `<Unity>` with your installation path, for example
`C:/Program Files/Unity/Hub/Editor/6000.3.13f1`.

Also check in `Project Settings > Editor` that **Asset Serialization** is set to
**Force Text**: without it scenes are binary and no tool can merge them.

## Responsive layout

The window adapts to its width, so it stays usable whether it is maximised or docked in a
narrow column next to the Inspector:

- below 900 px, toolbar buttons drop their labels and keep their glyph;
- below 780 px, the search field moves into an overflow menu (the `...` button);
- below 660 px, the sidebar folds away, and the branch filter and history depth join the
  overflow menu;
- below 560 px in the detail pane, the file list stacks above the diff instead of sitting
  beside it.

History columns are dropped from the right as the pane narrows, SHA first, then the date,
then the author, so the commit message always keeps a readable width. The sidebar can also
be toggled by hand from the toolbar, and the minimum window size is 360x300.

The footer shows the current branch, the number of commits and pending changes, and the
repository path.

## Theme and icons

The window paints its own dark palette by default rather than following the editor skin, so
it reads like a dedicated Git client. The toggle in the toolbar switches to a light palette;
the choice is stored per user.

Icons are [Bootstrap Icons](https://github.com/twbs/icons), embedded as SVG path data and
rasterised through the built-in Vector Graphics module. They are rendered white and tinted
at draw time, so one texture serves both themes.

Rasterising happens on demand and off the GUI callback: drawing a mesh into a render texture
in the middle of an IMGUI repaint fights its render state. An icon that is not ready yet
falls back to its glyph for that frame, which is invisible in practice.

That module only exists from Unity 6 onwards. On 2021.3 and 2022 the asmdef leaves
`GITTOOLS_VECTOR_GRAPHICS` undefined and the UI falls back to the Unicode glyphs declared
in `GitIcons`, which all sit in the Basic Multilingual Plane so IMGUI can look them up per
UTF-16 code unit. Nothing else changes.

The artwork is MIT licensed; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Design notes

- Commands run on a background thread and their callback is marshalled back onto the editor
  main thread, so the editor never freezes. Each command times out after 120 s.
- `GIT_TERMINAL_PROMPT=0` is forced: a command that would ask for credentials fails cleanly
  in the console instead of hanging on an invisible prompt.
- The commit message is passed through a temporary file (`git commit --file=`), which removes
  every quoting, accent and newline problem.
- The history and the diff are both virtualised: only the rows inside the viewport are drawn,
  so a thousand commits still scroll smoothly.
- Destructive actions (discarding a file, resetting, deleting a branch, merging) ask for
  confirmation first.

## Known limitations

- No per-line or per-hunk staging: files are staged whole.
- No interactive rebase, no submodule handling.
- The graph reads the first N commits of `git log` (100 to 1000, selectable in the toolbar)
  rather than streaming the whole history.

## Licence

MIT — see [LICENSE.md](LICENSE.md).
