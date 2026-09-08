using System;
using System.Collections.Generic;

namespace GitTools.EditorTools
{
    /// <summary>A single entry of the commit history, with its position in the graph.</summary>
    public class GitCommit
    {
        public string Sha = "";
        public string[] Parents = new string[0];
        public string Author = "";
        public string AuthorEmail = "";
        public DateTime Date;
        public string Subject = "";
        public GitRef[] Refs = new GitRef[0];

        /// <summary>Column this commit's node sits on.</summary>
        public int Lane;

        /// <summary>
        /// Lane occupancy immediately below this row: each slot holds the sha of the
        /// commit that lane is waiting for, or null when the lane is free.
        /// </summary>
        public string[] LanesBelow = new string[0];

        public string ShortSha { get { return Sha.Length >= 8 ? Sha.Substring(0, 8) : Sha; } }
        public bool IsMerge { get { return Parents.Length > 1; } }

        public string RelativeDate
        {
            get
            {
                var span = DateTime.Now - Date;
                if (span.TotalMinutes < 1) return "just now";
                if (span.TotalMinutes < 60) return (int)span.TotalMinutes + " min";
                if (span.TotalHours < 24) return (int)span.TotalHours + " h";
                if (span.TotalDays < 30) return (int)span.TotalDays + " d";
                if (span.TotalDays < 365) return (int)(span.TotalDays / 30) + " mo";
                return (int)(span.TotalDays / 365) + " y";
            }
        }
    }

    public enum GitRefKind { Head, LocalBranch, RemoteBranch, Tag, Stash }

    /// <summary>A decoration attached to a commit (branch tip, tag, HEAD…).</summary>
    public struct GitRef
    {
        public GitRefKind Kind;
        public string Name;

        public UnityEngine.Color Color
        {
            get
            {
                switch (Kind)
                {
                    case GitRefKind.Head: return GitStyles.HeadBadge;
                    case GitRefKind.RemoteBranch: return GitStyles.RemoteBranchBadge;
                    case GitRefKind.Tag: return GitStyles.TagBadge;
                    case GitRefKind.Stash: return GitStyles.StashBadge;
                    default: return GitStyles.LocalBranchBadge;
                }
            }
        }
    }

    /// <summary>Parses `git log` output and lays the commits out on graph lanes.</summary>
    public static class GitLog
    {
        // Unit and record separators: bytes git never emits inside a field value.
        const char FieldSeparator = (char)0x1f;
        const char RecordSeparator = (char)0x1e;

        /// <summary>Format string matching <see cref="Parse"/>. Keep the two in sync.</summary>
        public const string Format =
            "--format=%H%x1f%P%x1f%an%x1f%ae%x1f%at%x1f%D%x1f%s%x1e";

        public static string BuildCommand(int maxCount, bool allBranches)
        {
            return "log --date-order" + (allBranches ? " --all" : "") +
                   " --max-count=" + maxCount + " " + Format;
        }

        public static List<GitCommit> Parse(string raw)
        {
            var commits = new List<GitCommit>();
            if (string.IsNullOrEmpty(raw)) return commits;

            var records = raw.Split(RecordSeparator);
            foreach (var record in records)
            {
                var trimmed = record.Trim('\n', '\r');
                if (trimmed.Length == 0) continue;

                var fields = trimmed.Split(FieldSeparator);
                if (fields.Length < 7) continue;

                var commit = new GitCommit
                {
                    Sha = fields[0],
                    Parents = fields[1].Length == 0
                        ? new string[0]
                        : fields[1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries),
                    Author = fields[2],
                    AuthorEmail = fields[3],
                    Subject = fields[6],
                    Refs = ParseRefs(fields[5]),
                };

                long unixTime;
                if (long.TryParse(fields[4], out unixTime))
                    commit.Date = DateTimeOffset.FromUnixTimeSeconds(unixTime).ToLocalTime().DateTime;

                commits.Add(commit);
            }

            AssignLanes(commits);
            return commits;
        }

        static GitRef[] ParseRefs(string decoration)
        {
            if (string.IsNullOrEmpty(decoration)) return new GitRef[0];

            var refs = new List<GitRef>();
            foreach (var raw in decoration.Split(','))
            {
                var name = raw.Trim();
                if (name.Length == 0) continue;

                if (name.StartsWith("HEAD -> "))
                {
                    refs.Add(new GitRef { Kind = GitRefKind.Head, Name = name.Substring(8) });
                }
                else if (name == "HEAD")
                {
                    refs.Add(new GitRef { Kind = GitRefKind.Head, Name = "HEAD" });
                }
                else if (name.StartsWith("tag: "))
                {
                    refs.Add(new GitRef { Kind = GitRefKind.Tag, Name = name.Substring(5) });
                }
                else if (name.StartsWith("refs/stash"))
                {
                    refs.Add(new GitRef { Kind = GitRefKind.Stash, Name = "stash" });
                }
                else if (name.Contains("/"))
                {
                    refs.Add(new GitRef { Kind = GitRefKind.RemoteBranch, Name = name });
                }
                else
                {
                    refs.Add(new GitRef { Kind = GitRefKind.LocalBranch, Name = name });
                }
            }
            return refs.ToArray();
        }

        /// <summary>
        /// Walks the list top to bottom assigning each commit a lane, then records the
        /// lane occupancy below every row so the graph can be drawn without re-walking.
        /// </summary>
        public static void AssignLanes(List<GitCommit> commits)
        {
            var lanes = new List<string>(); // lane index -> sha awaited in that lane

            foreach (var commit in commits)
            {
                // The lane already reserved for this commit, if a child claimed one.
                int lane = lanes.IndexOf(commit.Sha);
                if (lane < 0)
                {
                    lane = lanes.IndexOf(null);
                    if (lane < 0)
                    {
                        lanes.Add(null);
                        lane = lanes.Count - 1;
                    }
                }
                commit.Lane = lane;

                // Other lanes waiting for this same commit converge here and are freed.
                for (int i = 0; i < lanes.Count; i++)
                    if (i != lane && lanes[i] == commit.Sha)
                        lanes[i] = null;

                // The first parent continues in this lane; extra parents branch off.
                lanes[lane] = commit.Parents.Length > 0 ? commit.Parents[0] : null;

                for (int p = 1; p < commit.Parents.Length; p++)
                {
                    var parent = commit.Parents[p];
                    if (lanes.Contains(parent)) continue;

                    int free = lanes.IndexOf(null);
                    if (free < 0)
                    {
                        lanes.Add(parent);
                    }
                    else
                    {
                        lanes[free] = parent;
                    }
                }

                TrimTrailingFreeLanes(lanes);
                commit.LanesBelow = lanes.ToArray();
            }
        }

        static void TrimTrailingFreeLanes(List<string> lanes)
        {
            while (lanes.Count > 0 && lanes[lanes.Count - 1] == null)
                lanes.RemoveAt(lanes.Count - 1);
        }

        /// <summary>Widest lane index used, so the graph column can be sized.</summary>
        public static int MaxLane(List<GitCommit> commits)
        {
            int max = 0;
            foreach (var commit in commits)
            {
                if (commit.Lane > max) max = commit.Lane;
                if (commit.LanesBelow.Length - 1 > max) max = commit.LanesBelow.Length - 1;
            }
            return max;
        }
    }
}
