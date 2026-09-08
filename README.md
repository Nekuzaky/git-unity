# Git Tools for Unity

[![Unity](https://img.shields.io/badge/Unity-2021.3%2B-000000?logo=unity&logoColor=white)](https://unity.com)
[![UPM](https://img.shields.io/badge/UPM-com.nekuzaky.gittools-2C7BE5)](https://docs.unity3d.com/Manual/upm-ui-giturl.html)
[![Version](https://img.shields.io/badge/version-0.3.0-2C7BE5)](Packages/com.nekuzaky.gittools/CHANGELOG.md)
[![License](https://img.shields.io/badge/license-MIT-3DA639)](Packages/com.nekuzaky.gittools/LICENSE.md)
[![Last commit](https://img.shields.io/github/last-commit/Nekuzaky/git-unity)](https://github.com/Nekuzaky/git-unity/commits/main)
[![Stars](https://img.shields.io/github/stars/Nekuzaky/git-unity?style=flat)](https://github.com/Nekuzaky/git-unity/stargazers)

A Git client that lives inside the Unity editor. Commit, push, pull, branch and merge
without ever alt-tabbing out of the engine.

It drives the `git` executable already installed on the machine rather than bundling a
native library, so Git LFS, SSH keys and the system credential manager all keep working
exactly as they do in your terminal.

---

## What it does

**Commit graph.** The full history with coloured lanes, filled nodes for commits and hollow
ones for merges, branch and tag badges rendered inline, author, relative date and short SHA.
Rendering is virtualised, so a thousand commits scroll without a hitch.

**Ref sidebar.** Current branch and its tracking state, pending changes, local branches with
their own ahead/behind counters, remote branches, tags and stashes. Sections fold away;
double-clicking a branch checks it out; clicking one scrolls the graph to its tip.

**Staging and committing.** Stage or unstage per file or in bulk, discard a file, write a
multi-line message, amend, and commit-and-push in a single action.

**Diffs.** Colourised and virtualised, for the working tree (index side or worktree side)
and for any file inside any commit.

**Right-click actions.** Switch, merge, rename and delete branches; check out a remote branch
as a local tracking branch; delete a branch on the remote; copy a SHA or message; create a
branch or tag at a commit; cherry-pick, revert, `reset --mixed`, `reset --hard`; apply, pop
or drop a stash.

**Merge conflicts.** A banner appears while a merge is unresolved, with a one-click call to
UnityYAMLMerge, per-file "Resolved" buttons and an abort button.

**Transparency.** Every command that runs and its raw output land in a console pane at the
bottom of the window. Nothing is a black box.

---

## Install

In Unity: `Window > Package Manager > + > Install package from git URL...`

```
https://github.com/Nekuzaky/git-unity.git?path=/Packages/com.nekuzaky.gittools
```

Or add it straight to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.nekuzaky.gittools": "https://github.com/Nekuzaky/git-unity.git?path=/Packages/com.nekuzaky.gittools"
  }
}
```

Pin a release by appending a tag, for example `#v0.3.0`.

### Requirements

- Unity 2021.3 or newer
- `git` 2.23 or newer on the `PATH`
- A Git repository at the project root, next to `Assets/`

---

## Use it

Open `Tools > Git`, or press `Ctrl+Shift+G` (`Cmd+Shift+G` on macOS).

Three resizable panes: refs on the left, the graph on top, the detail view below.
Their sizes are remembered between sessions.

Full documentation lives in the
[package README](Packages/com.nekuzaky.gittools/README.md), including the UnityYAMLMerge
setup you want before your first scene conflict.

---

## Repository layout

```
Packages/com.nekuzaky.gittools/   the package itself, the only thing you need
Assets/                            a bare Unity 6 project used to develop and test it
```

The package is embedded here so it can be iterated on inside a live project. Installing it
by URL pulls in only the `Packages/com.nekuzaky.gittools` subfolder.

---

## How it is built

Roughly 2 000 lines of editor C#, no third-party dependency.

| File | Role |
| --- | --- |
| `GitRunner.cs` | Process wrapper. Runs on a background thread, marshals callbacks back to the main thread, 120 s timeout, `GIT_TERMINAL_PROMPT=0` so a credential prompt can never hang the editor |
| `GitStatus.cs` | Parses `git status --porcelain -b`: index and worktree states, renames, conflicts, ahead/behind |
| `GitCommit.cs` | Parses `git log` and lays commits out on graph lanes |
| `GitRepository.cs` | Branches, remotes, tags and stashes, loaded as one batch |
| `GitDashboardWindow.cs` | The window: layout, graph rendering, all commands |
| `GitDiffView.cs` | Colourised, virtualised diff renderer |
| `GitStyles.cs` | Colours and styles, light and dark skin |
| `GitPromptWindow.cs` | Small modal text prompt for branch, tag and stash names |

A few deliberate choices worth knowing about:

- Commit messages are passed through a temporary file (`git commit --file=`), which removes
  every quoting, accent and newline problem in one go.
- Anything touching the disk (pull, checkout, merge, discard) triggers an
  `AssetDatabase.Refresh()`.
- Destructive actions ask for confirmation before running.

---

## Limitations

- No per-line or per-hunk staging: files are staged whole
- No interactive rebase, no submodule support
- The graph reads the first 100 to 1000 commits of `git log`, selectable in the toolbar,
  rather than streaming the entire history

---

## Contributing

Issues and pull requests are welcome. Since the repository is a working Unity project,
open it in Unity 2021.3 or newer and edit the package in place under `Packages/`.

Remember to commit the `.meta` files Unity generates: without them, asset GUIDs are
regenerated for everyone who installs the package.

---

## License

MIT. See [LICENSE.md](Packages/com.nekuzaky.gittools/LICENSE.md).
