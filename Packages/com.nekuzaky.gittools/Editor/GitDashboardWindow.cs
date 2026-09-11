using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GitTools.EditorTools
{
    /// <summary>One file touched by a commit, from `git show --name-status`.</summary>
    public class GitFileEntry
    {
        public char Status;
        public string Path = "";
        public string OldPath;

        public string Display { get { return OldPath != null ? OldPath + " -> " + Path : Path; } }

        public Color StatusColor
        {
            get
            {
                switch (Status)
                {
                    case 'A': return GitStyles.StatusAdded;
                    case 'D': return GitStyles.StatusDeleted;
                    case 'R': return GitStyles.StatusRenamed;
                    case 'U': return GitStyles.StatusConflict;
                    default: return GitStyles.StatusModified;
                }
            }
        }
    }

    /// <summary>
    /// Full Git dashboard: ref sidebar, commit graph, and a detail pane that shows either
    /// the staging area or the contents of the selected commit.
    /// </summary>
    public class GitDashboardWindow : EditorWindow
    {
        const float Toolbar = 25f;
        const float SplitterSize = 5f;
        const float RowHeight = 25f;
        const float SidebarRow = 22f;
        const float LaneWidth = 16f;
        const float MaxGraphWidth = 180f;
        const float FooterHeight = 26f;

        // Layout breakpoints, in window width.
        const float SidebarBreakpoint = 660f;   // below this the sidebar folds away
        const float LabelBreakpoint = 900f;     // below this toolbar buttons lose their labels
        const float SearchBreakpoint = 780f;    // below this the search field is dropped
        const float StackBreakpoint = 560f;     // below this the detail pane stacks vertically

        const string PrefSidebar = "GitTools.Dashboard.Sidebar";
        const string PrefHistory = "GitTools.Dashboard.History";
        const string PrefFiles = "GitTools.Dashboard.Files";
        const string PrefFilesHeight = "GitTools.Dashboard.FilesHeight";
        const string PrefAllBranches = "GitTools.Dashboard.AllBranches";
        const string PrefLimit = "GitTools.Dashboard.Limit";
        const string PrefSidebarShown = "GitTools.Dashboard.SidebarShown";

        static readonly int[] Limits = { 100, 250, 500, 1000 };
        static readonly string[] LimitLabels = { "100 commits", "250 commits", "500 commits", "1000 commits" };

        // ------------------------------------------------------------------ state

        GitStatus m_Status = new GitStatus();
        GitRepository m_Repo = new GitRepository();
        List<GitCommit> m_Commits = new List<GitCommit>();
        List<GitCommit> m_Visible = new List<GitCommit>();
        readonly List<GitFileEntry> m_Files = new List<GitFileEntry>();
        readonly List<string> m_Log = new List<string>();
        readonly GitDiffView m_Diff = new GitDiffView();

        string m_SelectedSha;
        bool m_WorkingTreeSelected = true;
        string m_SelectedFile;
        bool m_SelectedFileStaged;
        GitCommit m_SelectedCommit;
        string m_CommitBody = "";
        string m_CommitMessage = "";
        string m_Search = "";
        bool m_Amend;

        float m_SidebarWidth = 220f;
        float m_HistoryHeight = 300f;
        float m_FilesWidth = 300f;
        float m_FilesHeight = 150f;
        bool m_AllBranches = true;
        bool m_ShowSidebar = true;
        int m_LimitIndex = 1;

        Vector2 m_SidebarScroll;
        Vector2 m_HistoryScroll;
        Vector2 m_FilesScroll;
        Vector2 m_LogScroll;
        bool m_ShowLog;

        readonly HashSet<string> m_CollapsedSections = new HashSet<string>();
        double m_NextAutoRefresh;
        bool m_Loaded;

        // ------------------------------------------------------------- lifecycle

        [MenuItem("Tools/Git %#g")]
        public static void Open()
        {
            var window = GetWindow<GitDashboardWindow>("Git");
            // Low enough that the window stays usable docked in a narrow column.
            window.minSize = new Vector2(360f, 300f);
            window.Show();
        }

        void OnEnable()
        {
            m_SidebarWidth = EditorPrefs.GetFloat(PrefSidebar, 220f);
            m_HistoryHeight = EditorPrefs.GetFloat(PrefHistory, 300f);
            m_FilesWidth = EditorPrefs.GetFloat(PrefFiles, 300f);
            m_FilesHeight = EditorPrefs.GetFloat(PrefFilesHeight, 150f);
            m_AllBranches = EditorPrefs.GetBool(PrefAllBranches, true);
            m_ShowSidebar = EditorPrefs.GetBool(PrefSidebarShown, true);
            m_LimitIndex = Mathf.Clamp(EditorPrefs.GetInt(PrefLimit, 1), 0, Limits.Length - 1);

            m_Diff.Clear("Select a file to see its changes.");
            EditorApplication.update += OnEditorUpdate;
        }

        void OnDisable()
        {
            EditorPrefs.SetFloat(PrefSidebar, m_SidebarWidth);
            EditorPrefs.SetFloat(PrefHistory, m_HistoryHeight);
            EditorPrefs.SetFloat(PrefFiles, m_FilesWidth);
            EditorPrefs.SetFloat(PrefFilesHeight, m_FilesHeight);
            EditorPrefs.SetBool(PrefAllBranches, m_AllBranches);
            EditorPrefs.SetBool(PrefSidebarShown, m_ShowSidebar);
            EditorPrefs.SetInt(PrefLimit, m_LimitIndex);

            EditorApplication.update -= OnEditorUpdate;
        }

        void OnFocus()
        {
            if (m_Loaded) ReloadStatus();
        }

        void OnEditorUpdate()
        {
            if (GitRunner.IsBusy) return;
            if (EditorApplication.timeSinceStartup < m_NextAutoRefresh) return;

            m_NextAutoRefresh = EditorApplication.timeSinceStartup + 5.0;
            if (m_Loaded) ReloadStatus();
        }

        // ------------------------------------------------------------------ load

        void ReloadAll()
        {
            m_Loaded = true;
            ReloadStatus();
            ReloadRefs();
            ReloadHistory();
        }

        void ReloadStatus()
        {
            GitRunner.RunAsync("status --porcelain=v1 -b", result =>
            {
                if (!result.Ok) return;

                m_Status = GitStatus.Parse(result.Output);
                if (m_WorkingTreeSelected) RefreshWorkingTreeFiles();
                Repaint();
            });
        }

        void ReloadRefs()
        {
            GitRepository.LoadAsync(repo =>
            {
                m_Repo = repo;
                Repaint();
            });
        }

        void ReloadHistory()
        {
            GitRunner.RunAsync(GitLog.BuildCommand(Limits[m_LimitIndex], m_AllBranches), result =>
            {
                if (!result.Ok)
                {
                    LogResult(result);
                    return;
                }

                m_Commits = GitLog.Parse(result.Output);
                ApplySearch();

                if (!m_WorkingTreeSelected && m_SelectedSha != null)
                {
                    // Keep the selection alive across reloads when the commit still exists.
                    m_SelectedCommit = m_Commits.Find(c => c.Sha == m_SelectedSha);
                }

                Repaint();
            });
        }

        void ApplySearch()
        {
            var needle = m_Search.Trim();
            if (needle.Length == 0)
            {
                m_Visible = m_Commits;
                return;
            }

            m_Visible = m_Commits.FindAll(c =>
                c.Subject.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 ||
                c.Author.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 ||
                c.Sha.StartsWith(needle, StringComparison.OrdinalIgnoreCase));
        }

        void SelectWorkingTree()
        {
            m_WorkingTreeSelected = true;
            m_SelectedSha = null;
            m_SelectedCommit = null;
            m_SelectedFile = null;
            m_CommitBody = "";
            m_Diff.Clear("Select a file to see its changes.");
            RefreshWorkingTreeFiles();
        }

        void RefreshWorkingTreeFiles()
        {
            m_Files.Clear();
            foreach (var change in m_Status.Changes)
            {
                m_Files.Add(new GitFileEntry
                {
                    Status = change.IsUntracked ? 'A' : (change.IsStaged ? change.IndexStatus : change.WorkTreeStatus),
                    Path = change.Path,
                    OldPath = change.OriginalPath,
                });
            }
        }

        void SelectCommit(GitCommit commit)
        {
            m_WorkingTreeSelected = false;
            m_SelectedCommit = commit;
            m_SelectedSha = commit.Sha;
            m_SelectedFile = null;
            m_Files.Clear();
            m_CommitBody = "";
            m_Diff.Clear("Select a file to see its changes.");

            GitRunner.RunAsync("show -s --format=%B " + commit.Sha, result =>
            {
                if (result.Ok) m_CommitBody = result.Output.Trim();
                Repaint();
            });

            GitRunner.RunAsync("show --format= --name-status -M " + commit.Sha, result =>
            {
                if (!result.Ok) return;

                m_Files.Clear();
                foreach (var raw in result.Output.Replace("\r\n", "\n").Split('\n'))
                {
                    var line = raw.Trim();
                    if (line.Length == 0) continue;

                    var parts = line.Split('\t');
                    if (parts.Length < 2) continue;

                    var entry = new GitFileEntry { Status = parts[0][0] };
                    if (entry.Status == 'R' && parts.Length >= 3)
                    {
                        entry.OldPath = parts[1];
                        entry.Path = parts[2];
                    }
                    else
                    {
                        entry.Path = parts[1];
                    }

                    m_Files.Add(entry);
                }

                Repaint();
            });
        }

        void SelectFile(GitFileEntry entry)
        {
            m_SelectedFile = entry.Path;

            if (m_WorkingTreeSelected)
            {
                var change = m_Status.Changes.Find(c => c.Path == entry.Path);
                m_SelectedFileStaged = change != null && change.IsStaged && !change.IsUnstaged;

                if (change != null && change.IsUntracked)
                {
                    m_Diff.SetText("Untracked file: no history to compare against.\n\n" + entry.Path);
                    return;
                }

                var command = (m_SelectedFileStaged ? "diff --cached -- " : "diff -- ") + GitRunner.Quote(entry.Path);
                GitRunner.RunAsync(command, r => { m_Diff.SetText(r.Ok ? r.Output : r.Combined); Repaint(); });
                return;
            }

            GitRunner.RunAsync("show --format= -M " + m_SelectedSha + " -- " + GitRunner.Quote(entry.Path),
                r => { m_Diff.SetText(r.Ok ? r.Output : r.Combined); Repaint(); });
        }

        // -------------------------------------------------------------- commands

        void Execute(string command, bool refreshAssets = false)
        {
            Execute(new[] { command }, refreshAssets);
        }

        void Execute(IList<string> commands, bool refreshAssets = false)
        {
            GitRunner.RunSequenceAsync(commands, LogResult, success =>
            {
                if (refreshAssets) AssetDatabase.Refresh();
                ReloadAll();
                Repaint();
            });
        }

        void LogResult(GitResult result)
        {
            m_Log.Add("$ " + result.Command);
            var body = result.Combined;
            if (!string.IsNullOrEmpty(body)) m_Log.Add(body);
            if (!result.Ok)
            {
                m_Log.Add("-> echec (code " + result.ExitCode + ")");
                m_ShowLog = true;
            }

            while (m_Log.Count > 200) m_Log.RemoveAt(0);
            m_LogScroll.y = float.MaxValue;
            Repaint();
        }

        string BuildPushCommand()
        {
            if (string.IsNullOrEmpty(m_Status.Upstream))
                return "push -u origin " + GitRunner.Quote(m_Status.Branch);
            return "push";
        }

        void Commit(bool thenPush)
        {
            var message = m_CommitMessage.Trim();
            if (message.Length == 0 && !m_Amend)
            {
                EditorUtility.DisplayDialog("Git", "The commit message is empty.", "OK");
                return;
            }

            var messageFile = Path.Combine(Path.GetTempPath(), "unity-git-commit-" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(messageFile, message, new System.Text.UTF8Encoding(false));

            var commands = new List<string>
            {
                "commit " + (m_Amend ? "--amend " : "") + "--file=" + GitRunner.Quote(messageFile)
            };
            if (thenPush) commands.Add(BuildPushCommand());

            GitRunner.RunSequenceAsync(commands, LogResult, success =>
            {
                try { File.Delete(messageFile); } catch { }
                if (success)
                {
                    m_CommitMessage = "";
                    m_Amend = false;
                    GUI.FocusControl(null);
                }
                ReloadAll();
                Repaint();
            });
        }

        void Checkout(string branch)
        {
            Execute("checkout " + GitRunner.Quote(branch), refreshAssets: true);
        }

        void CheckoutRemote(string remoteBranch)
        {
            int slash = remoteBranch.IndexOf('/');
            var local = slash >= 0 ? remoteBranch.Substring(slash + 1) : remoteBranch;

            if (m_Repo.LocalBranches.Exists(b => b.Name == local))
            {
                Checkout(local);
                return;
            }

            Execute("checkout -b " + GitRunner.Quote(local) + " --track " + GitRunner.Quote(remoteBranch), refreshAssets: true);
        }

        void DeleteBranch(string branch)
        {
            if (!EditorUtility.DisplayDialog("Delete branch",
                    "Delete the local branch " + branch + "?", "Delete", "Cancel"))
                return;

            GitRunner.RunAsync("branch -d " + GitRunner.Quote(branch), result =>
            {
                LogResult(result);

                if (!result.Ok && result.Combined.Contains("not fully merged"))
                {
                    if (EditorUtility.DisplayDialog("Branch not merged",
                            branch + " has commits that no other branch contains. Force the deletion?",
                            "Force", "Cancel"))
                    {
                        Execute("branch -D " + GitRunner.Quote(branch));
                        return;
                    }
                }

                ReloadAll();
            });
        }

        void Merge(string branch)
        {
            if (!EditorUtility.DisplayDialog("Merge",
                    "Merge " + branch + " into " + m_Status.Branch + "?", "Merge", "Cancel"))
                return;

            Execute("merge --no-ff " + GitRunner.Quote(branch), refreshAssets: true);
        }

        void ResetTo(string sha, string mode)
        {
            var warning = mode == "--hard"
                ? "All local changes will be PERMANENTLY lost."
                : "Commits after this one are undone, but the files stay changed on disk.";

            if (!EditorUtility.DisplayDialog("Reset to " + sha.Substring(0, 8),
                    warning, "Reset", "Cancel"))
                return;

            Execute("reset " + mode + " " + sha, refreshAssets: true);
        }

        // ------------------------------------------------------------------- GUI

        void OnGUI()
        {
            if (!m_Loaded)
            {
                if (!GitRunner.IsRepository)
                {
                    DrawNoRepository();
                    return;
                }
                ReloadAll();
            }

            EditorGUI.DrawRect(new Rect(0f, 0f, position.width, position.height), GitStyles.WindowBackground);

            var toolbar = new Rect(0f, 0f, position.width, Toolbar);
            DrawToolbar(toolbar);

            var bannerHeight = m_Status.HasConflicts ? 28f : 0f;
            if (bannerHeight > 0f)
                DrawConflictBanner(new Rect(0f, Toolbar, position.width, bannerHeight));

            // Chrome first, then the console takes what is left over once the body has kept
            // a usable 40 px. Clamping this way means the panes can never be laid out past
            // the footer, however short the window is docked.
            var chrome = Toolbar + bannerHeight + FooterHeight;
            var available = Mathf.Max(0f, position.height - chrome);
            var logHeight = Mathf.Min(m_ShowLog ? 150f : 22f, Mathf.Max(0f, available - 40f));

            var body = new Rect(0f, Toolbar + bannerHeight, position.width, available - logHeight);

            if (body.height < 60f)
            {
                // Too short for the panes; the console and the footer still tell the story.
                DrawLog(new Rect(0f, body.yMax, position.width, logHeight));
                DrawFooter(new Rect(0f, position.height - FooterHeight, position.width, FooterHeight));
                return;
            }

            // The sidebar folds away on its own once the window gets too narrow to carry
            // both it and a usable history, and the toolbar toggle can force it either way.
            var showSidebar = m_ShowSidebar && position.width >= SidebarBreakpoint;

            var main = body;
            if (showSidebar)
            {
                m_SidebarWidth = Mathf.Clamp(m_SidebarWidth, 150f, Mathf.Max(150f, position.width - 360f));

                var sidebar = new Rect(body.x, body.y, m_SidebarWidth, body.height);
                DrawSidebar(sidebar);

                var vSplit = new Rect(sidebar.xMax, body.y, SplitterSize, body.height);
                HandleSplitter(vSplit, ref m_SidebarWidth, true, 1f);

                main = new Rect(vSplit.xMax, body.y, body.width - vSplit.xMax, body.height);
            }

            m_HistoryHeight = Mathf.Clamp(m_HistoryHeight, 80f, Mathf.Max(80f, main.height - 120f));

            var history = new Rect(main.x, main.y, main.width, m_HistoryHeight);
            DrawHistory(history);

            var hSplit = new Rect(main.x, history.yMax, main.width, SplitterSize);
            HandleSplitter(hSplit, ref m_HistoryHeight, false, 1f);

            var detail = new Rect(main.x, hSplit.yMax, main.width, main.yMax - hSplit.yMax);
            DrawDetail(detail);

            DrawLog(new Rect(0f, body.yMax, position.width, logHeight));
            DrawFooter(new Rect(0f, position.height - FooterHeight, position.width, FooterHeight));
        }

        /// <summary>
        /// Status line plus a discreet way to say thanks. The link only ever opens on a
        /// click the user made themselves.
        /// </summary>
        void DrawFooter(Rect rect)
        {
            EditorGUI.DrawRect(rect, GitStyles.FooterBackground);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), GitStyles.Border);

            var summary = m_Status.Branch;
            if (m_Commits.Count > 0) summary += "   " + GitIcons.Separator + "   " + m_Commits.Count + " commits";
            if (!m_Status.IsClean) summary += "   " + GitIcons.Separator + "   " + m_Status.Changes.Count + " changed";
            if (position.width >= 620f) summary += "   " + GitIcons.Separator + "   " + GitRunner.RepositoryRoot;

            var coffee = GitStyles.IconLabel("Coffee", GitIcons.Coffee,
                position.width < 420f ? null : "Buy me a coffee",
                "Support the development of Git Tools on buymeacoffee.com/nekuzaky");

            var linkWidth = Mathf.Min(GitStyles.Link.CalcSize(coffee).x + 20f, rect.width * 0.6f);
            var linkRect = new Rect(rect.xMax - linkWidth - 6f, rect.y + 3f, linkWidth, rect.height - 6f);

            GUI.Label(new Rect(rect.x, rect.y, rect.width - linkRect.width - 12f, rect.height), summary, GitStyles.Footer);

            // A quiet pill that only lights up under the cursor, so it never nags.
            var hovered = linkRect.Contains(Event.current.mousePosition);
            EditorGUI.DrawRect(linkRect, hovered ? GitStyles.HeaderBackground : GitStyles.PanelBackground);

            EditorGUIUtility.AddCursorRect(linkRect, MouseCursor.Link);
            if (GUI.Button(linkRect, coffee, GitStyles.Link))
                Application.OpenURL("https://buymeacoffee.com/nekuzaky");

            if (hovered) Repaint();
        }

        void DrawNoRepository()
        {
            GUILayout.Space(20f);
            EditorGUILayout.HelpBox("No Git repository found in " + GitRunner.RepositoryRoot, MessageType.Warning);
            if (GUILayout.Button("git init"))
            {
                GitRunner.RunAsync("init", r => { LogResult(r); ReloadAll(); });
            }
        }

        /// <summary>
        /// Shown while a merge is in progress: UnityYAMLMerge is the only sane way to
        /// resolve a conflicted scene or prefab, so it gets a button of its own.
        /// </summary>
        void DrawConflictBanner(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.45f, 0.16f, 0.16f));

            var conflicts = m_Status.Changes.FindAll(c => c.IsConflicted).Count;
            var label = new Rect(rect.x + 8f, rect.y, rect.width - 300f, rect.height);
            GUI.Label(label, "Merge conflict: " + conflicts + " file(s) need resolving, then staging.", EditorStyles.boldLabel);

            var resolve = new Rect(rect.xMax - 290f, rect.y + 2f, 180f, rect.height - 4f);
            if (GUI.Button(resolve, "Resolve (UnityYAMLMerge)", EditorStyles.miniButton))
                Execute("mergetool --no-prompt", refreshAssets: true);

            var abort = new Rect(rect.xMax - 104f, rect.y + 2f, 96f, rect.height - 4f);
            if (GUI.Button(abort, "Abort merge", EditorStyles.miniButton))
                Execute("merge --abort", refreshAssets: true);
        }

        /// <summary>
        /// Toolbar contents shrink with the window: labels drop first, then the search
        /// field, then the history-depth popup, and what is left moves into an overflow
        /// menu so no action ever becomes unreachable.
        /// </summary>
        void DrawToolbar(Rect rect)
        {
            var labels = position.width >= LabelBreakpoint;

            GUILayout.BeginArea(rect, EditorStyles.toolbar);
            using (new EditorGUILayout.HorizontalScope())
            {
                // Below the breakpoint the sidebar cannot be shown at all, so the toggle
                // reports that state instead of claiming to be on while nothing appears.
                var canShowSidebar = position.width >= SidebarBreakpoint;
                using (new EditorGUI.DisabledScope(!canShowSidebar))
                {
                    var sidebar = GUILayout.Toggle(m_ShowSidebar && canShowSidebar,
                        GitStyles.IconLabel("Sidebar", GitIcons.Sidebar, null, canShowSidebar
                            ? "Show or hide the sidebar."
                            : "The window is too narrow for the sidebar."),
                        GitStyles.ToolbarButton, GUILayout.Width(26f));

                    if (canShowSidebar && sidebar != m_ShowSidebar) m_ShowSidebar = sidebar;
                }

                GUILayout.Space(4f);

                using (new EditorGUI.DisabledScope(GitRunner.IsBusy))
                {
                    if (ToolbarButton("Refresh", GitIcons.Refresh, "Refresh", labels, 82f)) ReloadAll();
                    if (ToolbarButton("Fetch", GitIcons.Fetch, "Fetch", labels, 62f)) Execute("fetch --all --prune");
                    if (ToolbarButton("Pull", GitIcons.Pull, "Pull", labels, 56f)) Execute("pull --rebase=false", true);
                    if (ToolbarButton("Push", GitIcons.Push, "Push", labels, 56f)) Execute(BuildPushCommand());

                    GUILayout.Space(6f);

                    if (ToolbarButton("Branch", GitIcons.Branch, "Branch", labels, 74f))
                    {
                        GitPromptWindow.Show("New branch", "Name", "", "Create and switch",
                            name => Execute("checkout -b " + GitRunner.Quote(name), true));
                    }

                    if (ToolbarButton("Stash", GitIcons.Stash, "Stash", labels, 64f)) ShowStashMenu();
                }

                GUILayout.FlexibleSpace();

                if (GitRunner.IsBusy)
                {
                    GUILayout.Label(GitIcons.Refresh, GitStyles.ToolbarButton, GUILayout.Width(24f));
                    Repaint();
                }

                var dark = GUILayout.Toggle(GitStyles.Dark,
                    GitStyles.IconLabel("Theme", GitIcons.Theme, null, "Dark theme: draw the window with its own palette instead of the editor skin."),
                    GitStyles.ToolbarButton, GUILayout.Width(26f));
                if (dark != GitStyles.Dark) GitStyles.Dark = dark;

                if (position.width >= SidebarBreakpoint)
                {
                    EditorGUI.BeginChangeCheck();
                    m_AllBranches = GUILayout.Toggle(m_AllBranches,
                        GitStyles.IconLabel("Remote", GitIcons.Remote, labels ? "All branches" : null, "Include every branch in the graph, not just HEAD."),
                        GitStyles.ToolbarButton, GUILayout.Width(labels ? 96f : 26f));
                    m_LimitIndex = EditorGUILayout.Popup(m_LimitIndex, LimitLabels, EditorStyles.toolbarPopup, GUILayout.Width(92f));
                    if (EditorGUI.EndChangeCheck()) ReloadHistory();
                }

                if (position.width >= SearchBreakpoint)
                {
                    EditorGUI.BeginChangeCheck();
                    m_Search = GUILayout.TextField(m_Search, EditorStyles.toolbarSearchField,
                        GUILayout.MinWidth(90f), GUILayout.MaxWidth(180f));
                    if (EditorGUI.EndChangeCheck()) ApplySearch();
                }
                else if (GUILayout.Button(GitStyles.IconLabel("More", GitIcons.More, null, "More options"), GitStyles.ToolbarButton, GUILayout.Width(26f)))
                {
                    ShowOverflowMenu();
                }
            }
            GUILayout.EndArea();
        }

        bool ToolbarButton(string slot, string glyph, string label, bool showLabel, float wideWidth)
        {
            var content = GitStyles.IconLabel(slot, glyph, showLabel ? label : null, label);
            return GUILayout.Button(content, GitStyles.ToolbarButton, GUILayout.Width(showLabel ? wideWidth : 26f));
        }

        /// <summary>Holds whatever the toolbar had to drop at narrow widths.</summary>
        void ShowOverflowMenu()
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("All branches"), m_AllBranches, () =>
            {
                m_AllBranches = !m_AllBranches;
                ReloadHistory();
            });

            for (int i = 0; i < Limits.Length; i++)
            {
                var index = i;
                menu.AddItem(new GUIContent("History depth/" + LimitLabels[i]), m_LimitIndex == i, () =>
                {
                    m_LimitIndex = index;
                    ReloadHistory();
                });
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Search..."), false, () =>
                GitPromptWindow.Show("Search history", "Message, author or SHA", m_Search, "Search",
                    needle => { m_Search = needle; ApplySearch(); Repaint(); }));

            if (m_Search.Length > 0)
                menu.AddItem(new GUIContent("Clear search (" + m_Search + ")"), false, () =>
                {
                    m_Search = "";
                    ApplySearch();
                    Repaint();
                });

            menu.ShowAsContext();
        }

        // --------------------------------------------------------------- sidebar

        void DrawSidebar(Rect rect)
        {
            EditorGUI.DrawRect(rect, GitStyles.PanelBackground);

            var header = new Rect(rect.x, rect.y, rect.width, 42f);
            EditorGUI.DrawRect(header, GitStyles.HeaderBackground);
            GitStyles.DrawBottomBorder(header);

            var branchLabel = new Rect(header.x + 8f, header.y + 4f, header.width - 16f, 19f);
            GUI.Label(branchLabel, m_Status.Branch, GitStyles.Title);

            var trackLabel = new Rect(header.x + 8f, header.y + 23f, header.width - 16f, 16f);
            var summary = m_Status.Upstream ?? "no upstream";
            if (m_Status.Ahead > 0) summary += "   " + GitIcons.Ahead + m_Status.Ahead;
            if (m_Status.Behind > 0) summary += "   " + GitIcons.Behind + m_Status.Behind;
            GUI.Label(trackLabel, summary, GitStyles.MonoSmall);

            var content = new Rect(rect.x, header.yMax, rect.width, rect.height - header.height);
            var viewHeight = MeasureSidebar();
            var view = new Rect(0f, 0f, rect.width - 16f, viewHeight);

            m_SidebarScroll = GUI.BeginScrollView(content, m_SidebarScroll, view);
            float y = 4f;

            DrawSidebarSection(GitIcons.Changes + "  Changes", view.width, ref y, () =>
            {
                var count = m_Status.Changes.Count;
                var label = count == 0 ? "No changes" : count + " changed file(s)";
                var row = NextRow(view.width, ref y);
                if (DrawSidebarRow(row, label, 1, m_WorkingTreeSelected, GitStyles.HeadBadge, false, GitIcons.Changes, "Changes"))
                    SelectWorkingTree();
            });

            DrawSidebarSection(GitIcons.Branch + "  Local branches (" + m_Repo.LocalBranches.Count + ")", view.width, ref y, () =>
            {
                foreach (var branch in m_Repo.LocalBranches)
                {
                    var row = NextRow(view.width, ref y);
                    var label = branch.Name;
                    if (branch.Ahead > 0 || branch.Behind > 0)
                        label += "   " + (branch.Ahead > 0 ? GitIcons.Ahead + branch.Ahead.ToString() + " " : "") + (branch.Behind > 0 ? GitIcons.Behind + branch.Behind.ToString() : "");

                    var color = branch.IsCurrent ? GitStyles.HeadBadge : GitStyles.LocalBranchBadge;
                    if (DrawSidebarRow(row, label, 1, false, color, branch.IsCurrent, branch.IsCurrent ? GitIcons.Current : GitIcons.Other, branch.IsCurrent ? "Current" : "Branch"))
                    {
                        if (Event.current.clickCount == 2 && !branch.IsCurrent) Checkout(branch.Name);
                        else FocusBranch(branch.Name);
                    }

                    HandleContext(row, () => ShowBranchMenu(branch));
                }
            });

            DrawSidebarSection(GitIcons.Remote + "  Remote branches (" + m_Repo.RemoteBranches.Count + ")", view.width, ref y, () =>
            {
                foreach (var remote in m_Repo.RemoteBranches)
                {
                    var row = NextRow(view.width, ref y);
                    if (DrawSidebarRow(row, remote, 1, false, GitStyles.RemoteBranchBadge, false, GitIcons.Remote, "Remote"))
                    {
                        if (Event.current.clickCount == 2) CheckoutRemote(remote);
                        else FocusBranch(remote);
                    }

                    var captured = remote;
                    HandleContext(row, () => ShowRemoteBranchMenu(captured));
                }
            });

            DrawSidebarSection(GitIcons.Tag + "  Tags (" + m_Repo.Tags.Count + ")", view.width, ref y, () =>
            {
                foreach (var tag in m_Repo.Tags)
                {
                    var row = NextRow(view.width, ref y);
                    if (DrawSidebarRow(row, tag, 1, false, GitStyles.TagBadge, false, GitIcons.Tag, "Tag")) FocusBranch(tag);

                    var captured = tag;
                    HandleContext(row, () =>
                    {
                        var menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Delete tag"), false, () => Execute("tag -d " + GitRunner.Quote(captured)));
                        menu.AddItem(new GUIContent("Push tag"), false, () => Execute("push origin " + GitRunner.Quote(captured)));
                        menu.ShowAsContext();
                    });
                }
            });

            DrawSidebarSection(GitIcons.Stash + "  Stashes (" + m_Repo.Stashes.Count + ")", view.width, ref y, () =>
            {
                foreach (var stash in m_Repo.Stashes)
                {
                    var row = NextRow(view.width, ref y);
                    DrawSidebarRow(row, stash.Selector + "  " + stash.Description, 1, false, GitStyles.StashBadge, false, GitIcons.Stash, "Stash");

                    var captured = stash;
                    HandleContext(row, () =>
                    {
                        var menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Apply"), false, () => Execute("stash apply " + captured.Selector, true));
                        menu.AddItem(new GUIContent("Apply and drop (pop)"), false, () => Execute("stash pop " + captured.Selector, true));
                        menu.AddItem(new GUIContent("Delete"), false, () => Execute("stash drop " + captured.Selector));
                        menu.ShowAsContext();
                    });
                }
            });

            GUI.EndScrollView();
        }

        float MeasureSidebar()
        {
            float height = 20f;
            height += SectionHeight("Changes", 1);
            height += SectionHeight("Local branches", m_Repo.LocalBranches.Count);
            height += SectionHeight("Remote branches", m_Repo.RemoteBranches.Count);
            height += SectionHeight("Tags", m_Repo.Tags.Count);
            height += SectionHeight("Stashes", m_Repo.Stashes.Count);
            return height;
        }

        float SectionHeight(string key, int rows)
        {
            return SidebarRow + 4f + (m_CollapsedSections.Contains(key) ? 0f : rows * SidebarRow);
        }

        void DrawSidebarSection(string title, float width, ref float cursor, Action drawRows)
        {
            var key = title.Contains(" (") ? title.Substring(0, title.IndexOf(" (", StringComparison.Ordinal)) : title;
            var header = new Rect(0f, cursor, width, SidebarRow);

            var collapsed = m_CollapsedSections.Contains(key);
            if (GUI.Button(header, GitStyles.IconLabel(collapsed ? "Collapsed" : "Expanded", collapsed ? GitIcons.Collapsed : GitIcons.Expanded, title), GitStyles.SectionHeader))
            {
                if (collapsed) m_CollapsedSections.Remove(key);
                else m_CollapsedSections.Add(key);
            }

            cursor += SidebarRow;
            if (!collapsed) drawRows();
            cursor += 4f;
        }

        Rect NextRow(float width, ref float cursor)
        {
            var row = new Rect(0f, cursor, width, SidebarRow);
            cursor += SidebarRow;
            return row;
        }

        bool DrawSidebarRow(Rect rect, string label, int indent, bool selected, Color dot, bool bold = false, string glyph = null, string slot = null)
        {
            if (selected) GitStyles.DrawSelectedRow(rect);
            else if (rect.Contains(Event.current.mousePosition)) EditorGUI.DrawRect(rect, GitStyles.RowHover);

            var marker = new Rect(rect.x + 5f + indent * 6f, rect.y, 17f, rect.height);
            GitStyles.DrawIcon(marker, glyph ?? GitIcons.Other, dot, slot ?? "Other");

            var text = new Rect(marker.xMax + 4f, rect.y, rect.width - marker.xMax - 8f, rect.height);
            GUI.Label(text, label, bold ? GitStyles.RowBold : GitStyles.Row);

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                e.Use();
                return true;
            }
            return false;
        }

        /// <summary>Centred placeholder shown in place of an empty list.</summary>
        void DrawEmptyState(Rect rect, string message)
        {
            var box = new Rect(rect.x, rect.y + rect.height * 0.5f - 22f, rect.width, 44f);

            var icon = new Rect(box.x + box.width * 0.5f - 8f, box.y, 16f, 16f);
            GitStyles.DrawIcon(icon, GitIcons.Empty, GitStyles.Muted, "Empty");

            GUI.Label(new Rect(box.x, box.y + 20f, box.width, 20f), message, GitStyles.EmptyState);
        }

        void FocusBranch(string refName)
        {
            // Scroll the history to the tip of the clicked ref.
            var index = m_Visible.FindIndex(c => Array.Exists(c.Refs, r => r.Name == refName));
            if (index < 0) return;

            m_HistoryScroll.y = Mathf.Max(0f, index * RowHeight - 60f);
            SelectCommit(m_Visible[index]);
        }

        void HandleContext(Rect rect, Action buildMenu)
        {
            var e = Event.current;
            if (e.type == EventType.ContextClick && rect.Contains(e.mousePosition))
            {
                buildMenu();
                e.Use();
            }
        }

        void ShowBranchMenu(GitBranch branch)
        {
            var menu = new GenericMenu();
            if (!branch.IsCurrent)
            {
                menu.AddItem(new GUIContent("Switch to this branch"), false, () => Checkout(branch.Name));
                menu.AddItem(new GUIContent("Merge into " + m_Status.Branch), false, () => Merge(branch.Name));
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Delete"), false, () => DeleteBranch(branch.Name));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Current branch"));
                menu.AddItem(new GUIContent("Push"), false, () => Execute(BuildPushCommand()));
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Rename"), false, () =>
                GitPromptWindow.Show("Rename branch", "New name", branch.Name, "Rename",
                    name => Execute("branch -m " + GitRunner.Quote(branch.Name) + " " + GitRunner.Quote(name))));
            menu.ShowAsContext();
        }

        void ShowRemoteBranchMenu(string remoteBranch)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Check out as a local tracking branch"), false, () => CheckoutRemote(remoteBranch));
            menu.AddItem(new GUIContent("Merge into " + m_Status.Branch), false, () => Merge(remoteBranch));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Delete on the remote"), false, () =>
            {
                int slash = remoteBranch.IndexOf('/');
                if (slash < 0) return;

                var remote = remoteBranch.Substring(0, slash);
                var name = remoteBranch.Substring(slash + 1);

                if (EditorUtility.DisplayDialog("Delete remote branch",
                        "Delete " + remoteBranch + " on " + remote + "? This affects everyone on the remote.",
                        "Delete", "Cancel"))
                    Execute("push " + GitRunner.Quote(remote) + " --delete " + GitRunner.Quote(name));
            });
            menu.ShowAsContext();
        }

        void ShowStashMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Stash changes"), false, () =>
                GitPromptWindow.Show("Stash", "Message", "", "Stash",
                    message => Execute("stash push -m " + GitRunner.Quote(message), true)));
            menu.AddItem(new GUIContent("Stash including untracked files"), false, () =>
                GitPromptWindow.Show("Stash", "Message", "", "Stash",
                    message => Execute("stash push -u -m " + GitRunner.Quote(message), true)));

            if (m_Repo.Stashes.Count > 0)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Apply the latest (pop)"), false, () => Execute("stash pop", true));
            }
            menu.ShowAsContext();
        }

        // --------------------------------------------------------------- history

        void DrawHistory(Rect rect)
        {
            EditorGUI.DrawRect(rect, GitStyles.PanelBackground);

            var header = new Rect(rect.x, rect.y, rect.width, 22f);
            EditorGUI.DrawRect(header, GitStyles.HeaderBackground);
            GitStyles.DrawBottomBorder(header);

            int graphLanes = GitLog.MaxLane(m_Visible) + 1;
            float graphWidth = Mathf.Min(MaxGraphWidth, Mathf.Max(2, graphLanes) * LaneWidth + 8f);

            DrawHistoryHeader(header, graphWidth);

            var body = new Rect(rect.x, header.yMax, rect.width, rect.height - header.height);

            bool showWorkingRow = !m_Status.IsClean;
            int rowCount = m_Visible.Count + (showWorkingRow ? 1 : 0);

            if (rowCount == 0)
            {
                DrawEmptyState(body, m_Search.Length > 0
                    ? "No commit matches \"" + m_Search + "\""
                    : "No commit yet");
                return;
            }

            var view = new Rect(0f, 0f, body.width - 16f, rowCount * RowHeight);
            m_HistoryScroll = GUI.BeginScrollView(body, m_HistoryScroll, view);

            float offset = 0f;
            if (showWorkingRow)
            {
                DrawWorkingTreeRow(new Rect(0f, 0f, view.width, RowHeight), graphWidth);
                offset = RowHeight;
            }

            int first = Mathf.Max(0, Mathf.FloorToInt((m_HistoryScroll.y - offset) / RowHeight) - 1);
            int last = Mathf.Min(m_Visible.Count, first + Mathf.CeilToInt(body.height / RowHeight) + 2);

            for (int i = first; i < last; i++)
                DrawCommitRow(new Rect(0f, offset + i * RowHeight, view.width, RowHeight), m_Visible[i], i, graphWidth);

            GUI.EndScrollView();
        }

        void DrawHistoryHeader(Rect rect, float graphWidth)
        {
            var columns = ColumnLayout(rect, graphWidth);
            if (columns[0].width > 24f) GUI.Label(columns[0], "Graph", GitStyles.MonoSmall);
            GUI.Label(columns[1], "Message", GitStyles.MonoSmall);
            if (columns[2].width > 1f) GUI.Label(columns[2], "Author", GitStyles.MonoSmall);
            if (columns[3].width > 1f) GUI.Label(columns[3], "Date", GitStyles.MonoSmall);
            if (columns[4].width > 1f) GUI.Label(columns[4], "SHA", GitStyles.MonoSmall);
        }

        /// <summary>
        /// Columns are dropped from the right as the pane narrows - SHA first, then the
        /// date, then the author - so the message always keeps a readable width.
        /// </summary>
        Rect[] ColumnLayout(Rect row, float graphWidth)
        {
            float available = row.width - graphWidth;

            // Highest threshold on the rightmost column, so they really do fall right to left.
            float shaWidth = available >= 460f ? 76f : 0f;
            float dateWidth = available >= 380f ? 74f : 0f;
            float authorWidth = available >= 300f ? 130f : 0f;

            float messageWidth = Mathf.Max(60f, row.width - graphWidth - authorWidth - dateWidth - shaWidth - 12f);

            float x = row.x;
            var graph = new Rect(x, row.y, graphWidth, row.height); x += graphWidth;
            var message = new Rect(x, row.y, messageWidth, row.height); x += messageWidth;
            var author = new Rect(x, row.y, authorWidth, row.height); x += authorWidth;
            var date = new Rect(x, row.y, dateWidth, row.height); x += dateWidth;
            var sha = new Rect(x, row.y, shaWidth, row.height);

            return new[] { graph, message, author, date, sha };
        }

        void DrawWorkingTreeRow(Rect row, float graphWidth)
        {
            if (m_WorkingTreeSelected) GitStyles.DrawSelectedRow(row);
            else if (row.Contains(Event.current.mousePosition)) EditorGUI.DrawRect(row, GitStyles.RowHover);

            var node = new Rect(graphWidth * 0.5f - 4f, row.y + RowHeight * 0.5f - 4f, 8f, 8f);
            EditorGUI.DrawRect(node, GitStyles.HeadBadge);

            var columns = ColumnLayout(row, graphWidth);
            var staged = m_Status.Changes.FindAll(c => c.IsStaged).Count;
            var label = "Uncommitted changes  (" + m_Status.Changes.Count + " file(s)"
                        + (staged > 0 ? ", " + staged + " staged" : "") + ")";

            GUI.Label(columns[1], label, EditorStyles.boldLabel);

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && row.Contains(e.mousePosition))
            {
                SelectWorkingTree();
                e.Use();
            }
        }

        void DrawCommitRow(Rect row, GitCommit commit, int index, float graphWidth)
        {
            bool selected = !m_WorkingTreeSelected && commit.Sha == m_SelectedSha;

            if (selected) GitStyles.DrawSelectedRow(row);
            else if (index % 2 == 1) EditorGUI.DrawRect(row, GitStyles.RowAlternate);
            else if (row.Contains(Event.current.mousePosition)) EditorGUI.DrawRect(row, GitStyles.RowHover);

            DrawGraphCell(new Rect(row.x, row.y, graphWidth, row.height), commit, index);

            var columns = ColumnLayout(row, graphWidth);

            var messageRect = columns[1];
            foreach (var reference in commit.Refs)
            {
                if (messageRect.width < 60f) break;

                var badge = GitStyles.DrawBadge(messageRect, reference.Name, reference.Color);
                messageRect.x = badge.xMax + 4f;
                messageRect.width -= badge.width + 4f;
            }

            GUI.Label(messageRect, commit.Subject, GitStyles.Row);
            if (columns[2].width > 1f) GUI.Label(columns[2], commit.Author, GitStyles.RowMuted);
            if (columns[3].width > 1f) GUI.Label(columns[3], commit.RelativeDate, GitStyles.RowRight);
            if (columns[4].width > 1f) GUI.Label(columns[4], commit.ShortSha, GitStyles.MonoSmall);

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && row.Contains(e.mousePosition))
            {
                SelectCommit(commit);
                e.Use();
            }

            HandleContext(row, () => ShowCommitMenu(commit));
        }

        /// <summary>
        /// Draws the lane segments for one row. Lines are elbows made of axis-aligned
        /// rects, which stay crisp and respect the scroll view's clipping.
        /// </summary>
        void DrawGraphCell(Rect cell, GitCommit commit, int index)
        {
            if (Event.current.type != EventType.Repaint) return;

            float midY = cell.y + cell.height * 0.5f;
            Func<int, float> laneX = lane => cell.x + 8f + lane * LaneWidth;

            // Lanes entering this row from above.
            var above = index > 0 ? m_Visible[index - 1].LanesBelow : new string[0];
            for (int lane = 0; lane < above.Length; lane++)
            {
                if (above[lane] == null) continue;

                float x = laneX(lane);
                if (x > cell.xMax - 4f) continue;

                var color = GitStyles.LaneColor(lane);
                if (above[lane] == commit.Sha)
                {
                    DrawSegment(x, cell.y, x, midY, color);
                    DrawSegment(x, midY, laneX(commit.Lane), midY, color);
                }
                else
                {
                    DrawSegment(x, cell.y, x, midY, color);
                }
            }

            // Lanes leaving this row downwards.
            for (int lane = 0; lane < commit.LanesBelow.Length; lane++)
            {
                if (commit.LanesBelow[lane] == null) continue;

                float x = laneX(lane);
                if (x > cell.xMax - 4f) continue;

                var color = GitStyles.LaneColor(lane);
                DrawSegment(x, midY, x, cell.yMax, color);

                // A parent that starts a new lane hangs off this commit's node.
                if (lane != commit.Lane && Array.IndexOf(above, commit.LanesBelow[lane]) < 0)
                    DrawSegment(laneX(commit.Lane), midY, x, midY, color);
            }

            // The node itself: filled for a normal commit, hollow for a merge.
            float nodeX = laneX(commit.Lane);
            if (nodeX <= cell.xMax - 4f)
            {
                var nodeColor = GitStyles.LaneColor(commit.Lane);
                var node = new Rect(nodeX - 4f, midY - 4f, 8f, 8f);
                EditorGUI.DrawRect(node, nodeColor);

                if (commit.IsMerge)
                    EditorGUI.DrawRect(new Rect(node.x + 2f, node.y + 2f, 4f, 4f), GitStyles.PanelBackground);
            }
        }

        static void DrawSegment(float x0, float y0, float x1, float y1, Color color)
        {
            const float thickness = 2f;

            if (Mathf.Approximately(x0, x1))
            {
                EditorGUI.DrawRect(new Rect(x0 - thickness * 0.5f, Mathf.Min(y0, y1), thickness, Mathf.Abs(y1 - y0)), color);
                return;
            }

            EditorGUI.DrawRect(new Rect(Mathf.Min(x0, x1), y0 - thickness * 0.5f, Mathf.Abs(x1 - x0), thickness), color);
        }

        void ShowCommitMenu(GitCommit commit)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Copier le SHA"), false, () => EditorGUIUtility.systemCopyBuffer = commit.Sha);
            menu.AddItem(new GUIContent("Copier le message"), false, () => EditorGUIUtility.systemCopyBuffer = commit.Subject);
            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Create a branch here"), false, () =>
                GitPromptWindow.Show("New branch", "Name", "", "Create",
                    name => Execute("checkout -b " + GitRunner.Quote(name) + " " + commit.Sha, true)));

            menu.AddItem(new GUIContent("Create a tag here"), false, () =>
                GitPromptWindow.Show("New tag", "Name", "", "Create",
                    name => Execute("tag " + GitRunner.Quote(name) + " " + commit.Sha)));

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Check out this commit (detached HEAD)"), false, () => Checkout(commit.Sha));
            menu.AddItem(new GUIContent("Cherry-pick this commit"), false, () => Execute("cherry-pick " + commit.Sha, true));
            menu.AddItem(new GUIContent("Revert this commit"), false, () => Execute("revert --no-edit " + commit.Sha, true));

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Reset here/Keep the files (mixed)"), false, () => ResetTo(commit.Sha, "--mixed"));
            menu.AddItem(new GUIContent("Reset here/Discard everything (hard)"), false, () => ResetTo(commit.Sha, "--hard"));

            menu.ShowAsContext();
        }

        // ---------------------------------------------------------------- detail

        void DrawDetail(Rect rect)
        {
            EditorGUI.DrawRect(rect, GitStyles.PanelBackground);

            var bodyLines = CommitBodyLines();
            var headerHeight = Mathf.Min(50f + bodyLines.Length * 16f, Mathf.Max(24f, rect.height - 44f));
            var header = new Rect(rect.x, rect.y, rect.width, headerHeight);
            DrawDetailHeader(header, bodyLines);

            // Nothing useful fits below the header; stop before handing out negative rects.
            if (rect.height - headerHeight < 30f) return;

            var body = new Rect(rect.x, header.yMax, rect.width, rect.height - header.height);

            // Side by side while there is room; stacked once the pane gets too narrow for
            // a file list and a diff to coexist.
            if (body.width >= StackBreakpoint)
            {
                m_FilesWidth = Mathf.Clamp(m_FilesWidth, 160f, Mathf.Max(160f, body.width - 220f));

                var files = new Rect(body.x, body.y, m_FilesWidth, body.height);
                DrawFiles(files);

                var split = new Rect(files.xMax, body.y, SplitterSize, body.height);
                HandleSplitter(split, ref m_FilesWidth, true, 1f);

                m_Diff.Draw(new Rect(split.xMax, body.y, body.xMax - split.xMax, body.height));
            }
            else
            {
                m_FilesHeight = Mathf.Clamp(m_FilesHeight, 70f, Mathf.Max(70f, body.height - 80f));

                var files = new Rect(body.x, body.y, body.width, m_FilesHeight);
                DrawFiles(files);

                var split = new Rect(body.x, files.yMax, body.width, SplitterSize);
                HandleSplitter(split, ref m_FilesHeight, false, 1f);

                m_Diff.Draw(new Rect(body.x, split.yMax, body.width, body.yMax - split.yMax));
            }
        }

        /// <summary>
        /// The commit message minus its subject line, capped at three lines so a long
        /// body never eats the file list.
        /// </summary>
        string[] CommitBodyLines()
        {
            if (m_WorkingTreeSelected || m_SelectedCommit == null || m_CommitBody.Length == 0)
                return new string[0];

            var lines = new List<string>();
            var all = m_CommitBody.Replace("\r\n", "\n").Split('\n');

            for (int i = 1; i < all.Length && lines.Count < 3; i++)
            {
                var line = all[i].Trim();
                if (line.Length == 0 && lines.Count == 0) continue;
                lines.Add(line);
            }

            while (lines.Count > 0 && lines[lines.Count - 1].Length == 0)
                lines.RemoveAt(lines.Count - 1);

            return lines.ToArray();
        }

        void DrawDetailHeader(Rect rect, string[] bodyLines)
        {
            EditorGUI.DrawRect(rect, GitStyles.HeaderBackground);
            GitStyles.DrawBottomBorder(rect);

            var line1 = new Rect(rect.x + 10f, rect.y + 5f, rect.width - 20f, 19f);
            var line2 = new Rect(rect.x + 10f, rect.y + 26f, rect.width - 20f, 17f);

            if (m_WorkingTreeSelected)
            {
                GUI.Label(line1, "Uncommitted changes", GitStyles.Title);
                GUI.Label(line2, m_Status.Changes.Count + " file(s) - branch " + m_Status.Branch, GitStyles.MonoSmall);
                return;
            }

            if (m_SelectedCommit == null)
            {
                GUI.Label(line1, "No commit selected", GitStyles.Title);
                return;
            }

            GUI.Label(line1, m_SelectedCommit.Subject, GitStyles.Title);
            GUI.Label(line2,
                m_SelectedCommit.ShortSha + "   " + m_SelectedCommit.Author + " <" + m_SelectedCommit.AuthorEmail + ">   " +
                m_SelectedCommit.Date.ToString("yyyy-MM-dd HH:mm"),
                GitStyles.MonoSmall);

            for (int i = 0; i < bodyLines.Length; i++)
            {
                var line = new Rect(rect.x + 10f, rect.y + 45f + i * 16f, rect.width - 20f, 16f);
                GUI.Label(line, bodyLines[i], GitStyles.MonoSmall);
            }
        }

        void DrawFiles(Rect rect)
        {
            // The commit box is the only way to commit from the window, so it keeps its
            // minimum even in a cramped pane: the file list gives up the space instead of
            // the control disappearing without explanation.
            var commitBoxHeight = m_WorkingTreeSelected
                ? Mathf.Min(Mathf.Max(58f, rect.height * 0.45f), Mathf.Min(116f, rect.height))
                : 0f;

            var listRect = new Rect(rect.x, rect.y, rect.width, Mathf.Max(0f, rect.height - commitBoxHeight));

            if (m_Files.Count == 0)
            {
                DrawEmptyState(listRect, m_WorkingTreeSelected
                    ? "Working tree clean"
                    : "This commit touches no file");

                if (commitBoxHeight > 0f)
                    DrawCommitBox(new Rect(rect.x, listRect.yMax, rect.width, commitBoxHeight));
                return;
            }

            var view = new Rect(0f, 0f, listRect.width - 16f, Mathf.Max(m_Files.Count * SidebarRow, listRect.height));
            m_FilesScroll = GUI.BeginScrollView(listRect, m_FilesScroll, view);

            for (int i = 0; i < m_Files.Count; i++)
            {
                var entry = m_Files[i];
                var row = new Rect(0f, i * SidebarRow, view.width, SidebarRow);

                bool selected = entry.Path == m_SelectedFile;
                if (selected) GitStyles.DrawSelectedRow(row);
                else if (row.Contains(Event.current.mousePosition)) EditorGUI.DrawRect(row, GitStyles.RowHover);

                // A letter badge (A/M/D/R/U) says far more at a glance than a coloured dot.
                var marker = new Rect(row.x + 6f, row.y + 3f, 17f, SidebarRow - 6f);
                GitStyles.DrawStatusBadge(marker, entry.Status, entry.StatusColor);

                float buttonsWidth = m_WorkingTreeSelected ? 70f : 0f;
                var label = new Rect(marker.xMax + 6f, row.y, row.width - marker.xMax - buttonsWidth - 12f, row.height);
                GUI.Label(label, entry.Display, GitStyles.Row);

                if (m_WorkingTreeSelected)
                {
                    var change = m_Status.Changes.Find(c => c.Path == entry.Path);
                    if (change != null && change.IsConflicted)
                    {
                        var resolved = new Rect(row.xMax - 66f, row.y + 1f, 62f, SidebarRow - 3f);
                        if (GUI.Button(resolved, "Resolved", EditorStyles.miniButton))
                            Execute("add -- " + GitRunner.Quote(entry.Path));
                    }
                    else if (change != null)
                    {
                        var stageRect = new Rect(row.xMax - 44f, row.y + 1f, 20f, SidebarRow - 3f);
                        var discardRect = new Rect(row.xMax - 22f, row.y + 1f, 20f, SidebarRow - 3f);

                        if (change.IsStaged && !change.IsUnstaged)
                        {
                            if (GUI.Button(stageRect, "-", EditorStyles.miniButton))
                                Execute("restore --staged -- " + GitRunner.Quote(entry.Path));
                        }
                        else if (GUI.Button(stageRect, "+", EditorStyles.miniButton))
                        {
                            Execute("add -- " + GitRunner.Quote(entry.Path));
                        }

                        if (GUI.Button(discardRect, "x", EditorStyles.miniButton))
                            DiscardFile(change);
                    }
                }

                var e = Event.current;
                if (e.type == EventType.MouseDown && e.button == 0 && row.Contains(e.mousePosition))
                {
                    SelectFile(entry);
                    e.Use();
                }
            }

            GUI.EndScrollView();

            if (commitBoxHeight > 0f)
                DrawCommitBox(new Rect(rect.x, listRect.yMax, rect.width, commitBoxHeight));
        }

        void DiscardFile(GitChange change)
        {
            if (!EditorUtility.DisplayDialog("Discard changes?",
                    change.DisplayPath + "\n\nChanges to this file will be lost.",
                    "Discard changes", "Keep"))
                return;

            var target = GitRunner.Quote(change.Path);
            Execute(change.IsUntracked ? "clean -fd -- " + target : "restore -- " + target, refreshAssets: true);
        }

        void DrawCommitBox(Rect rect)
        {
            EditorGUI.DrawRect(rect, GitStyles.HeaderBackground);

            // In a cramped pane the bulk-staging row is the first thing to go: it has an
            // equivalent in the per-file buttons, while the message and Commit do not.
            var compact = rect.height < 92f;

            GUILayout.BeginArea(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, rect.height - 8f));

            if (!compact)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!m_Status.HasUnstaged))
                    {
                        if (GUILayout.Button("Stage all", EditorStyles.miniButtonLeft)) Execute("add -A");
                    }
                    using (new EditorGUI.DisabledScope(!m_Status.HasStaged))
                    {
                        if (GUILayout.Button("Unstage all", EditorStyles.miniButtonRight)) Execute("reset");
                    }
                }
            }

            var messageHeight = Mathf.Max(18f, rect.height - (compact ? 34f : 60f));
            m_CommitMessage = EditorGUILayout.TextArea(m_CommitMessage, GUILayout.Height(messageHeight));

            using (new EditorGUILayout.HorizontalScope())
            {
                m_Amend = GUILayout.Toggle(m_Amend, "Amend", EditorStyles.miniButton, GUILayout.Width(54f));

                using (new EditorGUI.DisabledScope(!m_Status.HasStaged && !m_Amend))
                {
                    if (GUILayout.Button("Commit", EditorStyles.miniButton)) Commit(false);
                    if (GUILayout.Button("Commit & Push", EditorStyles.miniButton)) Commit(true);
                }
            }

            GUILayout.EndArea();
        }

        // ------------------------------------------------------------------- log

        void DrawLog(Rect rect)
        {
            var header = new Rect(rect.x, rect.y, rect.width, 22f);
            EditorGUI.DrawRect(header, GitStyles.HeaderBackground);

            var toggle = new Rect(header.x + 4f, header.y, 240f, header.height);
            if (GUI.Button(toggle, GitStyles.IconLabel("Console", GitIcons.Console, "Git console (" + m_Log.Count + ")"), GitStyles.SectionHeader))
                m_ShowLog = !m_ShowLog;

            if (m_Log.Count > 0)
            {
                var clear = new Rect(header.xMax - 90f, header.y + 1f, 84f, header.height - 2f);
                if (GUI.Button(clear, "Clear", EditorStyles.miniButton)) m_Log.Clear();
            }

            if (!m_ShowLog) return;

            var body = new Rect(rect.x, header.yMax, rect.width, rect.height - header.height);
            EditorGUI.DrawRect(body, GitStyles.PanelBackground);

            var text = string.Join("\n", m_Log.ToArray());
            var height = Mathf.Max(body.height, GitStyles.Mono.CalcHeight(new GUIContent(text), body.width - 20f));
            var view = new Rect(0f, 0f, body.width - 16f, height);

            m_LogScroll = GUI.BeginScrollView(body, m_LogScroll, view);
            GUI.Label(view, text, GitStyles.Mono);
            GUI.EndScrollView();
        }

        // -------------------------------------------------------------- splitter

        void HandleSplitter(Rect rect, ref float value, bool horizontal, float sign)
        {
            EditorGUI.DrawRect(rect, GitStyles.Splitter);
            EditorGUIUtility.AddCursorRect(rect, horizontal ? MouseCursor.ResizeHorizontal : MouseCursor.ResizeVertical);

            var id = GUIUtility.GetControlID(FocusType.Passive);
            var e = Event.current;

            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (e.button == 0 && rect.Contains(e.mousePosition))
                    {
                        GUIUtility.hotControl = id;
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        value += (horizontal ? e.delta.x : e.delta.y) * sign;
                        e.Use();
                        Repaint();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;
            }
        }
    }
}
