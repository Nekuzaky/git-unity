using System;
using System.Collections.Generic;

namespace GitTools.EditorTools
{
    public class GitBranch
    {
        public string Name = "";
        public string Upstream;
        public int Ahead;
        public int Behind;
        public bool IsCurrent;

        public bool HasUpstream { get { return !string.IsNullOrEmpty(Upstream); } }
    }

    public class GitStash
    {
        public string Selector = ""; // stash@{0}
        public string Description = "";
    }

    /// <summary>
    /// Everything the sidebar needs: branches, remotes, tags and stashes.
    /// Loaded in one batch so the panel never shows a half-updated tree.
    /// </summary>
    public class GitRepository
    {
        const char Sep = (char)0x1f;

        public readonly List<GitBranch> LocalBranches = new List<GitBranch>();
        public readonly List<string> RemoteBranches = new List<string>();
        public readonly List<string> Remotes = new List<string>();
        public readonly List<string> Tags = new List<string>();
        public readonly List<GitStash> Stashes = new List<GitStash>();

        public string CurrentBranch = "";

        public GitBranch Current
        {
            get { return LocalBranches.Find(b => b.IsCurrent); }
        }

        /// <summary>Loads every ref list, then invokes <paramref name="onLoaded"/> once.</summary>
        public static void LoadAsync(Action<GitRepository> onLoaded)
        {
            var repo = new GitRepository();
            int pending = 5;

            Action done = () =>
            {
                pending--;
                if (pending == 0 && onLoaded != null) onLoaded(repo);
            };

            GitRunner.RunAsync("rev-parse --abbrev-ref HEAD", r =>
            {
                if (r.Ok) repo.CurrentBranch = r.Output.Trim();
                done();
            });

            GitRunner.RunAsync(
                "for-each-ref --sort=-committerdate --format=%(refname:short)%1f%(upstream:short)%1f%(upstream:track) refs/heads",
                r =>
                {
                    if (r.Ok) ParseLocalBranches(r.Output, repo);
                    done();
                });

            GitRunner.RunAsync("for-each-ref --sort=refname --format=%(refname:short) refs/remotes", r =>
            {
                if (r.Ok) FillList(r.Output, repo.RemoteBranches);
                done();
            });

            GitRunner.RunAsync("for-each-ref --sort=-creatordate --format=%(refname:short) refs/tags", r =>
            {
                if (r.Ok) FillList(r.Output, repo.Tags);
                done();
            });

            GitRunner.RunAsync("stash list --format=%gd%x1f%s", r =>
            {
                if (r.Ok) ParseStashes(r.Output, repo);
                done();
            });
        }

        static void ParseLocalBranches(string output, GitRepository repo)
        {
            foreach (var line in SplitLines(output))
            {
                var fields = line.Split(Sep);
                var branch = new GitBranch { Name = fields[0] };

                if (fields.Length > 1 && fields[1].Length > 0) branch.Upstream = fields[1];
                if (fields.Length > 2) ParseTracking(fields[2], branch);

                repo.LocalBranches.Add(branch);
            }

            foreach (var branch in repo.LocalBranches)
                branch.IsCurrent = branch.Name == repo.CurrentBranch;

            // Remotes are derived from the remote-tracking branches already fetched.
            foreach (var branch in repo.LocalBranches)
            {
                if (!branch.HasUpstream) continue;
                int slash = branch.Upstream.IndexOf('/');
                if (slash <= 0) continue;

                var remote = branch.Upstream.Substring(0, slash);
                if (!repo.Remotes.Contains(remote)) repo.Remotes.Add(remote);
            }
        }

        /// <summary>Reads git's `[ahead 1, behind 2]` tracking summary.</summary>
        static void ParseTracking(string track, GitBranch branch)
        {
            if (string.IsNullOrEmpty(track)) return;

            var inner = track.Trim();
            if (inner.StartsWith("[")) inner = inner.Substring(1);
            if (inner.EndsWith("]")) inner = inner.Substring(0, inner.Length - 1);

            foreach (var part in inner.Split(','))
            {
                var token = part.Trim();
                int value;
                if (token.StartsWith("ahead ") && int.TryParse(token.Substring(6), out value)) branch.Ahead = value;
                else if (token.StartsWith("behind ") && int.TryParse(token.Substring(7), out value)) branch.Behind = value;
            }
        }

        static void ParseStashes(string output, GitRepository repo)
        {
            foreach (var line in SplitLines(output))
            {
                var fields = line.Split(Sep);
                repo.Stashes.Add(new GitStash
                {
                    Selector = fields[0],
                    Description = fields.Length > 1 ? fields[1] : "",
                });
            }
        }

        static void FillList(string output, List<string> target)
        {
            foreach (var line in SplitLines(output))
            {
                // `origin/HEAD` is a symbolic pointer, not a branch worth listing.
                if (line.EndsWith("/HEAD")) continue;
                target.Add(line);
            }
        }

        static IEnumerable<string> SplitLines(string output)
        {
            if (string.IsNullOrEmpty(output)) yield break;

            foreach (var raw in output.Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length > 0) yield return line;
            }
        }
    }
}
