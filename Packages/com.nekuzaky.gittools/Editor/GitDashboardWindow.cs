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
                    case 'A': return GitStyles.LocalBranchBadge;
                    case 'D': return new Color(0.85f, 0.35f, 0.35f);
                    case 'R': return GitStyles.RemoteBranchBadge;
                    case 'U': return new Color(0.92f, 0.45f, 0.20f);
                    default: return GitStyles.HeadBadge;
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
        const float Toolbar = 21f;
        const float SplitterSize = 4f;
        const float RowHeight = 21f;
        const float SidebarRow = 18f;
        const float LaneWidth = 14f;
        const float MaxGraphWidth = 180f;

        const string PrefSidebar = "GitTools.Dashboard.Sidebar";
        const string PrefHistory = "GitTools.Dashboard.History";
        const string PrefFiles = "GitTools.Dashboard.Files";
        const string PrefAllBranches = "GitTools.Dashboard.AllBranches";
        const string PrefLimit = "GitTools.Dashboard.Limit";

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
        bool m_AllBranches = true;
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
            window.minSize = new Vector2(760f, 460f);
            window.Show();
        }

        void OnEnable()
        {
            m_SidebarWidth = EditorPrefs.GetFloat(PrefSidebar, 220f);
            m_HistoryHeight = EditorPrefs.GetFloat(PrefHistory, 300f);
            m_FilesWidth = EditorPrefs.GetFloat(PrefFiles, 300f);
            m_AllBranches = EditorPrefs.GetBool(PrefAllBranches, true);
            m_LimitIndex = Mathf.Clamp(EditorPrefs.GetInt(PrefLimit, 1), 0, Limits.Length - 1);

            m_Diff.Clear("Select a file to see its changes.");
            EditorApplication.update += OnEditorUpdate;
        }

        void OnDisable()
        {
            EditorPrefs.SetFloat(PrefSidebar, m_SidebarWidth);
            EditorPrefs.SetFloat(PrefHistory, m_HistoryHeight);
            EditorPrefs.SetFloat(PrefFiles, m_FilesWidth);
            EditorPrefs.SetBool(PrefAllBranches, m_AllBranches);
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

            var toolbar = new Rect(0f, 0f, position.width, Toolbar);
            DrawToolbar(toolbar);

            var bannerHeight = m_Status.HasConflicts ? 24f : 0f;
            if (bannerHeight > 0f)
                DrawConflictBanner(new Rect(0f, Toolbar, position.width, bannerHeight));

            var logHeight = m_ShowLog ? 130f : 18f;
            var body = new Rect(0f, Toolbar + bannerHeight, position.width,
                position.height - Toolbar - bannerHeight - logHeight);

            m_SidebarWidth = Mathf.Clamp(m_SidebarWidth, 160f, Mathf.Max(160f, position.width - 420f));
            var sidebar = new Rect(body.x, body.y, m_SidebarWidth, body.height);
            DrawSidebar(sidebar);

            var vSplit = new Rect(sidebar.xMax, body.y, SplitterSize, body.height);
            HandleSplitter(vSplit, ref m_SidebarWidth, true, 1f);

            var main = new Rect(vSplit.xMax, body.y, body.width - vSplit.xMax, body.height);
            m_HistoryHeight = Mathf.Clamp(m_HistoryHeight, 120f, Mathf.Max(120f, main.height - 160f));

            var history = new Rect(main.x, main.y, main.width, m_HistoryHeight);
            DrawHistory(history);

            var hSplit = new Rect(main.x, history.yMax, main.width, SplitterSize);
            HandleSplitter(hSplit, ref m_HistoryHeight, false, 1f);

            var detail = new Rect(main.x, hSplit.yMax, main.width, main.yMax - hSplit.yMax);
            DrawDetail(detail);

            DrawLog(new Rect(0f, body.yMax, position.width, logHeight));
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

        void DrawToolbar(Rect rect)
        {
            GUILayout.BeginArea(rect, EditorStyles.toolbar);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(GitRunner.IsBusy))
                {
                    if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(76f))) ReloadAll();
                    if (GUILayout.Button("Fetch", EditorStyles.toolbarButton, GUILayout.Width(48f))) Execute("fetch --all --prune");
                    if (GUILayout.Button("Pull", EditorStyles.toolbarButton, GUILayout.Width(44f))) Execute("pull --rebase=false", true);
                    if (GUILayout.Button("Push", EditorStyles.toolbarButton, GUILayout.Width(44f))) Execute(BuildPushCommand());

                    GUILayout.Space(8f);

                    if (GUILayout.Button("Branch", EditorStyles.toolbarButton, GUILayout.Width(62f)))
                    {
                        GitPromptWindow.Show("New branch", "Name", "", "Create and switch",
                            name => Execute("checkout -b " + GitRunner.Quote(name), true));
                    }

                    if (GUILayout.Button("Stash", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                        ShowStashMenu();
                }

                GUILayout.FlexibleSpace();

                if (GitRunner.IsBusy)
                {
                    GUILayout.Label("git...", EditorStyles.toolbarButton, GUILayout.Width(40f));
                    Repaint();
                }

                EditorGUI.BeginChangeCheck();
                m_AllBranches = GUILayout.Toggle(m_AllBranches, "All branches", EditorStyles.toolbarButton, GUILayout.Width(122f));
                m_LimitIndex = EditorGUILayout.Popup(m_LimitIndex, LimitLabels, EditorStyles.toolbarPopup, GUILayout.Width(96f));
                if (EditorGUI.EndChangeCheck()) ReloadHistory();

                EditorGUI.BeginChangeCheck();
                m_Search = GUILayout.TextField(m_Search, EditorStyles.toolbarSearchField, GUILayout.Width(170f));
                if (EditorGUI.EndChangeCheck()) ApplySearch();
            }
            GUILayout.EndArea();
        }

        // --------------------------------------------------------------- sidebar

        void DrawSidebar(Rect rect)
        {
            EditorGUI.DrawRect(rect, GitStyles.PanelBackground);

            var header = new Rect(rect.x, rect.y, rect.width, 34f);
            EditorGUI.DrawRect(header, GitStyles.HeaderBackground);

            var branchLabel = new Rect(header.x + 6f, header.y + 2f, header.width - 12f, 16f);
            GUI.Label(branchLabel, m_Status.Branch, GitStyles.Title);

            var trackLabel = new Rect(header.x + 6f, header.y + 17f, header.width - 12f, 14f);
            var summary = m_Status.Upstream ?? "no upstream";
            if (m_Status.Ahead > 0) summary += "   ^" + m_Status.Ahead;
            if (m_Status.Behind > 0) summary += "   v" + m_Status.Behind;
            GUI.Label(trackLabel, summary, GitStyles.MonoSmall);

            var content = new Rect(rect.x, header.yMax, rect.width, rect.height - header.height);
            var viewHeight = MeasureSidebar();
            var view = new Rect(0f, 0f, rect.width - 16f, viewHeight);

            m_SidebarScroll = GUI.BeginScrollView(content, m_SidebarScroll, view);
            float y = 4f;

            DrawSidebarSection("Changes", view.width, ref y, () =>
            {
                var count = m_Status.Changes.Count;
                var label = count == 0 ? "No changes" : count + " changed file(s)";
                var row = NextRow(view.width, ref y);
                if (DrawSidebarRow(row, label, 1, m_WorkingTreeSelected, GitStyles.HeadBadge))
                    SelectWorkingTree();
            });

            DrawSidebarSection("Local branches (" + m_Repo.LocalBranches.Count + ")", view.width, ref y, () =>
            {
                foreach (var branch in m_Repo.LocalBranches)
                {
                    var row = NextRow(view.width, ref y);
                    var label = branch.Name;
                    if (branch.Ahead > 0 || branch.Behind > 0)
                        label += "   " + (branch.Ahead > 0 ? "^" + branch.Ahead + " " : "") + (branch.Behind > 0 ? "v" + branch.Behind : "");

                    var color = branch.IsCurrent ? GitStyles.HeadBadge : GitStyles.LocalBranchBadge;
                    if (DrawSidebarRow(row, label, 1, false, color, branch.IsCurrent))
                    {
                        if (Event.current.clickCount == 2 && !branch.IsCurrent) Checkout(branch.Name);
                        else FocusBranch(branch.Name);
                    }

                    HandleContext(row, () => ShowBranchMenu(branch));
                }
            });

            DrawSidebarSection("Remote branches (" + m_Repo.RemoteBranches.Count + ")", view.width, ref y, () =>
            {
                foreach (var remote in m_Repo.RemoteBranches)
                {
                    var row = NextRow(view.width, ref y);
                    if (DrawSidebarRow(row, remote, 1, false, GitStyles.RemoteBranchBadge))
                    {
                        if (Event.current.clickCount == 2) CheckoutRemote(remote);
                        else FocusBranch(remote);
                    }

                    var captured = remote;
                    HandleContext(row, () => ShowRemoteBranchMenu(captured));
                }
            });

            DrawSidebarSection("Tags (" + m_Repo.Tags.Count + ")", view.width, ref y, () =>
            {
                foreach (var tag in m_Repo.Tags)
                {
                    var row = NextRow(view.width, ref y);
                    if (DrawSidebarRow(row, tag, 1, false, GitStyles.TagBadge)) FocusBranch(tag);

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

            DrawSidebarSection("Stashes (" + m_Repo.Stashes.Count + ")", view.width, ref y, () =>
            {
                foreach (var stash in m_Repo.Stashes)
                {
                    var row = NextRow(view.width, ref y);
                    DrawSidebarRow(row, stash.Selector + "  " + stash.Description, 1, false, GitStyles.StashBadge);

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
            if (GUI.Button(header, (collapsed ? "▸  " : "▾  ") + title, GitStyles.SectionHeader))
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

        bool DrawSidebarRow(Rect rect, string label, int indent, bool selected, Color dot, bool bold = false)
        {
            if (selected) EditorGUI.DrawRect(rect, GitStyles.RowSelected);
            else if (rect.Contains(Event.current.mousePosition)) EditorGUI.DrawRect(rect, GitStyles.RowHover);

            var marker = new Rect(rect.x + 6f + indent * 6f, rect.y + rect.height * 0.5f - 3f, 6f, 6f);
            EditorGUI.DrawRect(marker, dot);

            var text = new Rect(marker.xMax + 6f, rect.y, rect.width - marker.xMax - 10f, rect.height);
            GUI.Label(text, label, bold ? EditorStyles.boldLabel : GitStyles.Row);

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                e.Use();
                return true;
            }
            return false;
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

            var header = new Rect(rect.x, rect.y, rect.width, 18f);
            EditorGUI.DrawRect(header, GitStyles.HeaderBackground);

            int graphLanes = GitLog.MaxLane(m_Visible) + 1;
            float graphWidth = Mathf.Min(MaxGraphWidth, Mathf.Max(2, graphLanes) * LaneWidth + 8f);

            DrawHistoryHeader(header, graphWidth);

            var body = new Rect(rect.x, header.yMax, rect.width, rect.height - header.height);

            bool showWorkingRow = !m_Status.IsClean;
            int rowCount = m_Visible.Count + (showWorkingRow ? 1 : 0);

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
            GUI.Label(columns[0], "Graph", GitStyles.MonoSmall);
            GUI.Label(columns[1], "Message", GitStyles.MonoSmall);
            GUI.Label(columns[2], "Author", GitStyles.MonoSmall);
            GUI.Label(columns[3], "Date", GitStyles.MonoSmall);
            GUI.Label(columns[4], "SHA", GitStyles.MonoSmall);
        }

        Rect[] ColumnLayout(Rect row, float graphWidth)
        {
            const float authorWidth = 130f;
            const float dateWidth = 74f;
            const float shaWidth = 76f;

            float messageWidth = Mathf.Max(80f, row.width - graphWidth - authorWidth - dateWidth - shaWidth - 12f);

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
            if (m_WorkingTreeSelected) EditorGUI.DrawRect(row, GitStyles.RowSelected);
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

            if (selected) EditorGUI.DrawRect(row, GitStyles.RowSelected);
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
            GUI.Label(columns[2], commit.Author, GitStyles.RowMuted);
            GUI.Label(columns[3], commit.RelativeDate, GitStyles.RowRight);
            GUI.Label(columns[4], commit.ShortSha, GitStyles.MonoSmall);

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
            var header = new Rect(rect.x, rect.y, rect.width, 42f + bodyLines.Length * 14f);
            DrawDetailHeader(header, bodyLines);

            var body = new Rect(rect.x, header.yMax, rect.width, rect.height - header.height);
            m_FilesWidth = Mathf.Clamp(m_FilesWidth, 180f, Mathf.Max(180f, body.width - 240f));

            var files = new Rect(body.x, body.y, m_FilesWidth, body.height);
            DrawFiles(files);

            var split = new Rect(files.xMax, body.y, SplitterSize, body.height);
            HandleSplitter(split, ref m_FilesWidth, true, 1f);

            m_Diff.Draw(new Rect(split.xMax, body.y, body.xMax - split.xMax, body.height));
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

            var line1 = new Rect(rect.x + 8f, rect.y + 3f, rect.width - 16f, 17f);
            var line2 = new Rect(rect.x + 8f, rect.y + 21f, rect.width - 16f, 15f);

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
                var line = new Rect(rect.x + 8f, rect.y + 38f + i * 14f, rect.width - 16f, 14f);
                GUI.Label(line, bodyLines[i], GitStyles.MonoSmall);
            }
        }

        void DrawFiles(Rect rect)
        {
            var commitBoxHeight = m_WorkingTreeSelected ? 116f : 0f;
            var listRect = new Rect(rect.x, rect.y, rect.width, rect.height - commitBoxHeight);

            var view = new Rect(0f, 0f, listRect.width - 16f, Mathf.Max(m_Files.Count * SidebarRow, listRect.height));
            m_FilesScroll = GUI.BeginScrollView(listRect, m_FilesScroll, view);

            for (int i = 0; i < m_Files.Count; i++)
            {
                var entry = m_Files[i];
                var row = new Rect(0f, i * SidebarRow, view.width, SidebarRow);

                bool selected = entry.Path == m_SelectedFile;
                if (selected) EditorGUI.DrawRect(row, GitStyles.RowSelected);
                else if (row.Contains(Event.current.mousePosition)) EditorGUI.DrawRect(row, GitStyles.RowHover);

                var marker = new Rect(row.x + 6f, row.y + 6f, 6f, 6f);
                EditorGUI.DrawRect(marker, entry.StatusColor);

                float buttonsWidth = m_WorkingTreeSelected ? 46f : 0f;
                var label = new Rect(marker.xMax + 5f, row.y, row.width - marker.xMax - buttonsWidth - 10f, row.height);
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

            if (m_WorkingTreeSelected)
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

            GUILayout.BeginArea(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, rect.height - 8f));

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

            m_CommitMessage = EditorGUILayout.TextArea(m_CommitMessage, GUILayout.Height(42f));

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
            var header = new Rect(rect.x, rect.y, rect.width, 18f);
            EditorGUI.DrawRect(header, GitStyles.HeaderBackground);

            var toggle = new Rect(header.x + 4f, header.y, 220f, header.height);
            if (GUI.Button(toggle, (m_ShowLog ? "▾  " : "▸  ") + "Git console (" + m_Log.Count + ")", GitStyles.SectionHeader))
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
