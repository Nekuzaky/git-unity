using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GitTools.EditorTools
{
    /// <summary>
    /// Git client embedded in the editor: stage, commit, push, pull, branch and merge
    /// without leaving Unity.
    /// </summary>
    public class GitWindow : EditorWindow
    {
        const string PrefKeyAutoRefresh = "GitTools.AutoRefresh";
        const double AutoRefreshInterval = 5.0;
        const int MaxDiffChars = 60000;
        const int MaxLogLines = 200;

        GitStatus m_Status = new GitStatus();
        readonly List<string> m_Branches = new List<string>();
        readonly List<string> m_Log = new List<string>();

        string m_CommitMessage = "";
        string m_NewBranchName = "";
        string m_SelectedPath;
        bool m_SelectedIsStaged;
        string m_Diff = "";
        bool m_Amend;
        bool m_AutoRefresh = true;
        bool m_ShowLog = true;

        Vector2 m_ChangesScroll;
        Vector2 m_DiffScroll;
        Vector2 m_LogScroll;
        double m_NextAutoRefresh;
        bool m_Initialized;

        [MenuItem("Tools/Git %#g")]
        public static void Open()
        {
            var window = GetWindow<GitWindow>("Git");
            window.minSize = new Vector2(420f, 480f);
            window.Show();
        }

        void OnEnable()
        {
            m_AutoRefresh = EditorPrefs.GetBool(PrefKeyAutoRefresh, true);
            EditorApplication.update += OnEditorUpdate;
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        void OnFocus()
        {
            Refresh();
        }

        void OnEditorUpdate()
        {
            if (!m_AutoRefresh || GitRunner.IsBusy) return;
            if (EditorApplication.timeSinceStartup < m_NextAutoRefresh) return;

            m_NextAutoRefresh = EditorApplication.timeSinceStartup + AutoRefreshInterval;
            Refresh(silent: true);
        }

        // ------------------------------------------------------------------ data

        void Refresh(bool silent = false)
        {
            if (!GitRunner.IsRepository) return;

            GitRunner.RunAsync("status --porcelain=v1 -b", result =>
            {
                if (!result.Ok)
                {
                    if (!silent) LogResult(result);
                    return;
                }

                m_Status = GitStatus.Parse(result.Output);
                RefreshDiff();
                Repaint();
            });

            GitRunner.RunAsync("for-each-ref --format=%(refname:short) refs/heads", result =>
            {
                if (!result.Ok) return;

                m_Branches.Clear();
                foreach (var line in result.Output.Replace("\r\n", "\n").Split('\n'))
                {
                    var name = line.Trim();
                    if (name.Length > 0) m_Branches.Add(name);
                }
                Repaint();
            });
        }

        void RefreshDiff()
        {
            if (string.IsNullOrEmpty(m_SelectedPath))
            {
                m_Diff = "";
                return;
            }

            var change = m_Status.Changes.Find(c => c.Path == m_SelectedPath);
            if (change != null && change.IsUntracked)
            {
                m_Diff = "Fichier non suivi - pas encore d'historique.\n\n" + m_SelectedPath;
                return;
            }

            string command = (m_SelectedIsStaged ? "diff --cached -- " : "diff -- ") + GitRunner.Quote(m_SelectedPath);
            GitRunner.RunAsync(command, result =>
            {
                var text = result.Ok ? result.Output : result.Combined;
                if (string.IsNullOrEmpty(text)) text = "(aucune difference textuelle - fichier binaire ou gere par LFS)";
                if (text.Length > MaxDiffChars) text = text.Substring(0, MaxDiffChars) + "\n\n[...diff tronque]";
                m_Diff = text;
                Repaint();
            });
        }

        void Select(GitChange change, bool staged)
        {
            m_SelectedPath = change.Path;
            m_SelectedIsStaged = staged;
            RefreshDiff();

            // Mirror the selection into the Project window when the path is an asset.
            if (change.Path.StartsWith("Assets/"))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(change.Path);
                if (asset != null) EditorGUIUtility.PingObject(asset);
            }
        }

        // ------------------------------------------------------------- commands

        void Execute(string command, bool refreshAssets = false)
        {
            Execute(new[] { command }, refreshAssets);
        }

        void Execute(IList<string> commands, bool refreshAssets = false)
        {
            GitRunner.RunSequenceAsync(commands, LogResult, success =>
            {
                if (refreshAssets) AssetDatabase.Refresh();
                Refresh();
                Repaint();
            });
        }

        void Commit(bool thenPush)
        {
            var message = m_CommitMessage.Trim();
            if (message.Length == 0 && !m_Amend)
            {
                EditorUtility.DisplayDialog("Git", "Le message de commit est vide.", "OK");
                return;
            }

            // Write the message to a temp file: avoids every quoting and newline pitfall.
            var messageFile = Path.Combine(Path.GetTempPath(), "unity-git-commit-" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(messageFile, message, new System.Text.UTF8Encoding(false));

            var commands = new List<string>();
            commands.Add("commit " + (m_Amend ? "--amend " : "") + "--file=" + GitRunner.Quote(messageFile));
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
                Refresh();
                Repaint();
            });
        }

        string BuildPushCommand()
        {
            if (string.IsNullOrEmpty(m_Status.Upstream))
                return "push -u origin " + GitRunner.Quote(m_Status.Branch);
            return "push";
        }

        void DiscardAll()
        {
            if (!EditorUtility.DisplayDialog(
                    "Annuler toutes les modifications ?",
                    "Toutes les modifications non indexees seront perdues et les fichiers non suivis supprimes. Cette action est irreversible.",
                    "Tout annuler", "Garder"))
                return;

            Execute(new[] { "restore --staged --worktree -- .", "clean -fd" }, refreshAssets: true);
        }

        void Discard(GitChange change)
        {
            if (!EditorUtility.DisplayDialog(
                    "Annuler les modifications ?",
                    change.DisplayPath + "\n\nLes modifications de ce fichier seront perdues.",
                    "Annuler les modifications", "Garder"))
                return;

            var target = GitRunner.Quote(change.Path);
            Execute(change.IsUntracked ? "clean -fd -- " + target : "restore -- " + target, refreshAssets: true);
        }

        void Checkout(string branch)
        {
            if (branch == m_Status.Branch) return;

            if (m_Status.HasUnstaged || m_Status.HasStaged)
            {
                if (!EditorUtility.DisplayDialog(
                        "Changer de branche",
                        "Tu as des modifications en cours. Git refusera de basculer si elles entrent en conflit avec la branche " + branch + ".\n\nContinuer ?",
                        "Basculer", "Annuler"))
                    return;
            }

            Execute("checkout " + GitRunner.Quote(branch), refreshAssets: true);
        }

        void CreateBranch()
        {
            var name = m_NewBranchName.Trim();
            if (name.Length == 0) return;

            Execute("checkout -b " + GitRunner.Quote(name), refreshAssets: true);
            m_NewBranchName = "";
            GUI.FocusControl(null);
        }

        void Merge(string branch)
        {
            if (!EditorUtility.DisplayDialog(
                    "Fusionner",
                    "Fusionner " + branch + " dans " + m_Status.Branch + " ?\n\n" +
                    "En cas de conflit sur des scenes ou prefabs, utilise le bouton Resoudre (UnityYAMLMerge).",
                    "Fusionner", "Annuler"))
                return;

            Execute("merge --no-ff " + GitRunner.Quote(branch), refreshAssets: true);
        }

        void RunMergeTool()
        {
            Execute("mergetool --no-prompt", refreshAssets: true);
        }

        // ------------------------------------------------------------------- UI

        void OnGUI()
        {
            if (!m_Initialized)
            {
                m_Initialized = true;
                Refresh();
            }

            if (!GitRunner.IsRepository)
            {
                DrawNoRepository();
                return;
            }

            DrawToolbar();
            DrawBranchBar();

            if (m_Status.HasConflicts) DrawConflictBanner();

            using (new EditorGUI.DisabledScope(GitRunner.IsBusy))
            {
                DrawChanges();
                DrawDiff();
                DrawCommitBox();
                DrawBranchTools();
            }

            DrawLog();
        }

        void DrawNoRepository()
        {
            EditorGUILayout.HelpBox("Aucun depot Git trouve dans " + GitRunner.RepositoryRoot, MessageType.Warning);
            if (GUILayout.Button("git init"))
                Execute("init");
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(GitRunner.IsBusy))
                {
                    if (GUILayout.Button("Actualiser", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                        Refresh();

                    if (GUILayout.Button("Fetch", EditorStyles.toolbarButton, GUILayout.Width(50f)))
                        Execute("fetch --all --prune");

                    if (GUILayout.Button("Pull", EditorStyles.toolbarButton, GUILayout.Width(50f)))
                        Execute("pull --rebase=false", refreshAssets: true);

                    if (GUILayout.Button("Push", EditorStyles.toolbarButton, GUILayout.Width(50f)))
                        Execute(BuildPushCommand());
                }

                GUILayout.FlexibleSpace();

                if (GitRunner.IsBusy)
                {
                    GUILayout.Label("git...", EditorStyles.toolbarButton, GUILayout.Width(40f));
                    Repaint();
                }

                EditorGUI.BeginChangeCheck();
                m_AutoRefresh = GUILayout.Toggle(m_AutoRefresh, "Auto", EditorStyles.toolbarButton, GUILayout.Width(45f));
                if (EditorGUI.EndChangeCheck())
                    EditorPrefs.SetBool(PrefKeyAutoRefresh, m_AutoRefresh);
            }
        }

        void DrawBranchBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var label = m_Status.Branch;
                if (!string.IsNullOrEmpty(m_Status.Upstream)) label += "  ->  " + m_Status.Upstream;
                GUILayout.Label(label, EditorStyles.boldLabel);

                GUILayout.FlexibleSpace();

                if (m_Status.Ahead > 0) GUILayout.Label("^ " + m_Status.Ahead + " a pousser", EditorStyles.miniLabel);
                if (m_Status.Behind > 0) GUILayout.Label("v " + m_Status.Behind + " a tirer", EditorStyles.miniLabel);
                if (m_Status.IsClean) GUILayout.Label("propre", EditorStyles.miniLabel);
            }
        }

        void DrawConflictBanner()
        {
            EditorGUILayout.HelpBox("Fusion en conflit. Resous les fichiers puis indexe-les avant de committer.", MessageType.Error);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Resoudre (UnityYAMLMerge)")) RunMergeTool();
                if (GUILayout.Button("Abandonner la fusion")) Execute("merge --abort", refreshAssets: true);
            }
        }

        void DrawChanges()
        {
            m_ChangesScroll = EditorGUILayout.BeginScrollView(m_ChangesScroll, GUILayout.MinHeight(140f));

            var staged = m_Status.Changes.FindAll(c => c.IsStaged && !c.IsConflicted);
            var unstaged = m_Status.Changes.FindAll(c => (c.IsUnstaged || c.IsUntracked) && !c.IsConflicted);
            var conflicts = m_Status.Changes.FindAll(c => c.IsConflicted);

            if (conflicts.Count > 0)
            {
                DrawSectionHeader("Conflits (" + conflicts.Count + ")", null, null);
                foreach (var change in conflicts) DrawChangeRow(change, staged: false, conflicted: true);
            }

            DrawSectionHeader(
                "Indexe (" + staged.Count + ")",
                staged.Count > 0 ? "Tout desindexer" : null,
                () => Execute("reset"));
            foreach (var change in staged) DrawChangeRow(change, staged: true, conflicted: false);
            if (staged.Count == 0) DrawEmptyRow("Rien d'indexe.");

            DrawSectionHeader(
                "Modifications (" + unstaged.Count + ")",
                unstaged.Count > 0 ? "Tout indexer" : null,
                () => Execute("add -A"));
            foreach (var change in unstaged) DrawChangeRow(change, staged: false, conflicted: false);
            if (unstaged.Count == 0) DrawEmptyRow("Aucune modification.");

            if (unstaged.Count > 0)
            {
                GUILayout.Space(2f);
                if (GUILayout.Button("Tout annuler...", EditorStyles.miniButton)) DiscardAll();
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawSectionHeader(string title, string actionLabel, Action action)
        {
            GUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(title, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (actionLabel != null && GUILayout.Button(actionLabel, EditorStyles.miniButton, GUILayout.Width(110f)))
                    action();
            }
        }

        void DrawEmptyRow(string message)
        {
            using (new EditorGUI.DisabledScope(true))
                GUILayout.Label("    " + message, EditorStyles.miniLabel);
        }

        void DrawChangeRow(GitChange change, bool staged, bool conflicted)
        {
            var isSelected = change.Path == m_SelectedPath && m_SelectedIsStaged == staged;

            using (new EditorGUILayout.HorizontalScope(isSelected ? EditorStyles.helpBox : GUIStyle.none))
            {
                var content = new GUIContent("  " + change.DisplayPath, change.Path);
                if (GUILayout.Button(content, EditorStyles.label))
                    Select(change, staged);

                GUILayout.FlexibleSpace();

                var color = GUI.color;
                if (conflicted) GUI.color = new Color(1f, 0.5f, 0.5f);
                GUILayout.Label(change.Label(staged), EditorStyles.miniLabel, GUILayout.Width(70f));
                GUI.color = color;

                if (conflicted)
                {
                    if (GUILayout.Button("Resolu", EditorStyles.miniButton, GUILayout.Width(60f)))
                        Execute("add -- " + GitRunner.Quote(change.Path));
                }
                else if (staged)
                {
                    if (GUILayout.Button("-", EditorStyles.miniButton, GUILayout.Width(22f)))
                        Execute("restore --staged -- " + GitRunner.Quote(change.Path));
                }
                else
                {
                    if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(22f)))
                        Execute("add -- " + GitRunner.Quote(change.Path));
                    if (GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(22f)))
                        Discard(change);
                }
            }
        }

        void DrawDiff()
        {
            if (string.IsNullOrEmpty(m_Diff)) return;

            GUILayout.Label("Diff - " + m_SelectedPath, EditorStyles.boldLabel);
            m_DiffScroll = EditorGUILayout.BeginScrollView(m_DiffScroll, GUILayout.MinHeight(100f), GUILayout.MaxHeight(220f));
            EditorGUILayout.TextArea(m_Diff, GetMonoStyle(), GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        void DrawCommitBox()
        {
            GUILayout.Space(4f);
            GUILayout.Label("Message de commit", EditorStyles.boldLabel);
            m_CommitMessage = EditorGUILayout.TextArea(m_CommitMessage, GUILayout.MinHeight(48f));

            using (new EditorGUILayout.HorizontalScope())
            {
                m_Amend = GUILayout.Toggle(m_Amend, new GUIContent("Amend", "Modifie le dernier commit au lieu d'en creer un nouveau."), EditorStyles.miniButton, GUILayout.Width(60f));

                using (new EditorGUI.DisabledScope(!m_Status.HasStaged && !m_Amend))
                {
                    if (GUILayout.Button("Commit")) Commit(thenPush: false);
                    if (GUILayout.Button("Commit & Push")) Commit(thenPush: true);
                }
            }
        }

        void DrawBranchTools()
        {
            GUILayout.Space(6f);
            GUILayout.Label("Branches", EditorStyles.boldLabel);

            if (m_Branches.Count > 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var index = Mathf.Max(0, m_Branches.IndexOf(m_Status.Branch));
                    var newIndex = EditorGUILayout.Popup(index, m_Branches.ToArray());
                    if (newIndex != index) Checkout(m_Branches[newIndex]);

                    using (new EditorGUI.DisabledScope(m_Branches.Count < 2))
                    {
                        if (GUILayout.Button("Fusionner...", GUILayout.Width(90f)))
                            ShowMergeMenu();
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                m_NewBranchName = EditorGUILayout.TextField(m_NewBranchName);
                using (new EditorGUI.DisabledScope(m_NewBranchName.Trim().Length == 0))
                {
                    if (GUILayout.Button("Nouvelle branche", GUILayout.Width(120f))) CreateBranch();
                }
            }
        }

        void ShowMergeMenu()
        {
            var menu = new GenericMenu();
            foreach (var branch in m_Branches)
            {
                if (branch == m_Status.Branch) continue;
                var captured = branch;
                menu.AddItem(new GUIContent(branch), false, () => Merge(captured));
            }
            menu.ShowAsContext();
        }

        void DrawLog()
        {
            GUILayout.Space(4f);
            m_ShowLog = EditorGUILayout.Foldout(m_ShowLog, "Console git (" + m_Log.Count + ")", true);
            if (!m_ShowLog) return;

            m_LogScroll = EditorGUILayout.BeginScrollView(m_LogScroll, GUILayout.MinHeight(70f), GUILayout.MaxHeight(160f));
            EditorGUILayout.TextArea(string.Join("\n", m_Log.ToArray()), GetMonoStyle(), GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Vider la console", EditorStyles.miniButton))
                m_Log.Clear();
        }

        void LogResult(GitResult result)
        {
            m_Log.Add("$ " + result.Command);
            var body = result.Combined;
            if (!string.IsNullOrEmpty(body)) m_Log.Add(body);
            if (!result.Ok) m_Log.Add("-> echec (code " + result.ExitCode + ")");

            while (m_Log.Count > MaxLogLines) m_Log.RemoveAt(0);
            m_LogScroll.y = float.MaxValue;
            Repaint();
        }

        static GUIStyle s_MonoStyle;

        static GUIStyle GetMonoStyle()
        {
            if (s_MonoStyle == null)
            {
                s_MonoStyle = new GUIStyle(EditorStyles.textArea)
                {
                    font = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Menlo", "Courier New" }, 11),
                    wordWrap = false,
                    richText = false,
                };
            }
            return s_MonoStyle;
        }
    }
}
