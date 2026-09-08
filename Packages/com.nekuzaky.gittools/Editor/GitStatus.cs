using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace GitTools.EditorTools
{
    /// <summary>One entry from `git status --porcelain`.</summary>
    public class GitChange
    {
        public char IndexStatus;    // staged side
        public char WorkTreeStatus; // unstaged side
        public string Path;
        public string OriginalPath; // set for renames/copies

        public bool IsStaged { get { return IndexStatus != ' ' && IndexStatus != '?'; } }
        public bool IsUnstaged { get { return WorkTreeStatus != ' ' && WorkTreeStatus != '?'; } }
        public bool IsUntracked { get { return IndexStatus == '?' && WorkTreeStatus == '?'; } }
        public bool IsConflicted
        {
            get
            {
                return IndexStatus == 'U' || WorkTreeStatus == 'U'
                       || (IndexStatus == 'A' && WorkTreeStatus == 'A')
                       || (IndexStatus == 'D' && WorkTreeStatus == 'D');
            }
        }

        public string DisplayPath
        {
            get { return OriginalPath != null ? OriginalPath + " → " + Path : Path; }
        }

        public string Label(bool staged)
        {
            if (IsConflicted) return "conflit";
            char code = staged ? IndexStatus : WorkTreeStatus;
            switch (code)
            {
                case 'M': return "modifié";
                case 'A': return "ajouté";
                case 'D': return "supprimé";
                case 'R': return "renommé";
                case 'C': return "copié";
                case 'T': return "type changé";
                case '?': return "non suivi";
                default: return code.ToString();
            }
        }
    }

    /// <summary>Snapshot of the working tree plus branch tracking info.</summary>
    public class GitStatus
    {
        public string Branch = "(inconnue)";
        public string Upstream;
        public int Ahead;
        public int Behind;
        public bool Detached;
        public readonly List<GitChange> Changes = new List<GitChange>();

        public bool HasStaged { get { return Changes.Exists(c => c.IsStaged && !c.IsConflicted); } }
        public bool HasUnstaged { get { return Changes.Exists(c => (c.IsUnstaged || c.IsUntracked) && !c.IsConflicted); } }
        public bool HasConflicts { get { return Changes.Exists(c => c.IsConflicted); } }
        public bool IsClean { get { return Changes.Count == 0; } }

        static readonly Regex s_BranchLine = new Regex(
            @"^## (?<branch>[^\.\s]+(?:\.[^\.\s]+)*?)(?:\.\.\.(?<upstream>\S+))?(?: \[(?<track>.*)\])?$");

        public static GitStatus Parse(string porcelainOutput)
        {
            var status = new GitStatus();
            if (string.IsNullOrEmpty(porcelainOutput)) return status;

            var lines = porcelainOutput.Replace("\r\n", "\n").Split('\n');
            foreach (var line in lines)
            {
                if (line.Length == 0) continue;

                if (line.StartsWith("## "))
                {
                    ParseBranchLine(line, status);
                    continue;
                }

                if (line.Length < 4) continue;

                var change = new GitChange
                {
                    IndexStatus = line[0],
                    WorkTreeStatus = line[1],
                };

                string path = line.Substring(3);
                int arrow = path.IndexOf(" -> ", StringComparison.Ordinal);
                if (arrow >= 0)
                {
                    change.OriginalPath = Unquote(path.Substring(0, arrow));
                    change.Path = Unquote(path.Substring(arrow + 4));
                }
                else
                {
                    change.Path = Unquote(path);
                }

                status.Changes.Add(change);
            }

            status.Changes.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase));
            return status;
        }

        static void ParseBranchLine(string line, GitStatus status)
        {
            if (line.StartsWith("## HEAD (no branch)"))
            {
                status.Detached = true;
                status.Branch = "HEAD détachée";
                return;
            }

            var match = s_BranchLine.Match(line);
            if (!match.Success)
            {
                status.Branch = line.Substring(3).Trim();
                return;
            }

            status.Branch = match.Groups["branch"].Value;
            if (match.Groups["upstream"].Success) status.Upstream = match.Groups["upstream"].Value;

            if (match.Groups["track"].Success)
            {
                foreach (var part in match.Groups["track"].Value.Split(','))
                {
                    var token = part.Trim();
                    int value;
                    if (token.StartsWith("ahead ") && int.TryParse(token.Substring(6), out value)) status.Ahead = value;
                    else if (token.StartsWith("behind ") && int.TryParse(token.Substring(7), out value)) status.Behind = value;
                }
            }
        }

        static string Unquote(string path)
        {
            path = path.Trim();
            if (path.Length >= 2 && path[0] == '"' && path[path.Length - 1] == '"')
                path = path.Substring(1, path.Length - 2).Replace("\\\"", "\"").Replace("\\\\", "\\");
            return path;
        }
    }
}
