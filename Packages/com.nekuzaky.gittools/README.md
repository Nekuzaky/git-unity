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
https://github.com/Nekuzaky/git-unity.git?path=/Packages/com.nekuzaky.gittools#v0.4.0
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

The window is split into three resizable panes; their sizes are remembered between sessions.

| Pane | Contents |
| --- | --- |
| Toolbar | Refresh, Fetch, Pull, Push, branch creation, Stash menu, dark-theme toggle, search filter, history depth |
| Sidebar | Current branch and its tracking state, pending changes, local branches (with ahead/behind counters), remote branches, tags, stashes |
| Graph | History with coloured lanes, filled nodes for commits and hollow ones for merges, branch and tag badges, author, relative date, SHA |
| Detail | Files of the selected commit, or the staging area when the "Uncommitted changes" row is selected |
| Diff | Colourised diff of the selected file, with virtualised scrolling |
| Git console | Every command that ran, with its raw output |

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

## Theme and icons

The window paints its own dark palette by default rather than following the editor skin, so
it reads like a dedicated Git client. The toggle in the toolbar switches to a light palette;
the choice is stored per user.

Icons are Unicode symbols, not colour emoji. Unity's IMGUI draws text through a dynamic font
with no COLR/CBDT support, so emoji render as blank boxes. Every glyph is declared in
`GitIcons` and drawn with a symbol-capable font chain (Segoe UI Symbol first on Windows),
because the editor font alone covers only a third of them. Change a glyph there and the
whole UI follows.

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
