using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace GitTools.EditorTools
{
    /// <summary>Result of a single git invocation.</summary>
    public struct GitResult
    {
        public string Command;
        public int ExitCode;
        public string Output;
        public string Error;

        public bool Ok { get { return ExitCode == 0; } }

        public string Combined
        {
            get
            {
                if (string.IsNullOrEmpty(Error)) return Output;
                if (string.IsNullOrEmpty(Output)) return Error;
                return Output + "\n" + Error;
            }
        }
    }

    /// <summary>
    /// Thin wrapper around the git executable. Commands run in the project root
    /// (the folder containing Assets/). Async calls marshal their callback back
    /// onto the editor main thread.
    /// </summary>
    public static class GitRunner
    {
        const int DefaultTimeoutMs = 120000;

        static readonly Queue<Action> s_MainThreadQueue = new Queue<Action>();
        static bool s_PumpHooked;
        static int s_RunningCount;

        public static bool IsBusy { get { return s_RunningCount > 0; } }

        public static string RepositoryRoot
        {
            get { return Path.GetDirectoryName(Application.dataPath).Replace('\\', '/'); }
        }

        public static bool IsRepository
        {
            get { return Directory.Exists(Path.Combine(RepositoryRoot, ".git")) || File.Exists(Path.Combine(RepositoryRoot, ".git")); }
        }

        /// <summary>Runs git and blocks until it exits. Use only for fast, read-only commands.</summary>
        public static GitResult Run(string arguments)
        {
            var result = new GitResult { Command = "git " + arguments };

            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "-c core.quotepath=false " + arguments,
                WorkingDirectory = RepositoryRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            // Never let git block on an interactive credential prompt inside the editor.
            startInfo.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
            startInfo.EnvironmentVariables["LC_ALL"] = "C";

            try
            {
                using (var process = new Process { StartInfo = startInfo })
                {
                    var stdout = new StringBuilder();
                    var stderr = new StringBuilder();

                    process.OutputDataReceived += (s, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
                    process.ErrorDataReceived += (s, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    if (!process.WaitForExit(DefaultTimeoutMs))
                    {
                        try { process.Kill(); } catch { }
                        result.ExitCode = -1;
                        result.Error = "Timed out after " + (DefaultTimeoutMs / 1000) + "s.";
                        return result;
                    }

                    process.WaitForExit(); // flush the async readers
                    result.ExitCode = process.ExitCode;
                    result.Output = stdout.ToString().TrimEnd();
                    result.Error = stderr.ToString().TrimEnd();
                }
            }
            catch (Exception e)
            {
                result.ExitCode = -1;
                result.Error = "Could not start git: " + e.Message +
                               "\nMake sure git is installed and available in PATH.";
            }

            return result;
        }

        /// <summary>Runs git on a worker thread; the callback fires on the main thread.</summary>
        public static void RunAsync(string arguments, Action<GitResult> onComplete)
        {
            HookPump();
            Interlocked.Increment(ref s_RunningCount);

            var thread = new Thread(() =>
            {
                GitResult result;
                try
                {
                    result = Run(arguments);
                }
                catch (Exception e)
                {
                    result = new GitResult { Command = "git " + arguments, ExitCode = -1, Error = e.Message };
                }

                lock (s_MainThreadQueue)
                {
                    s_MainThreadQueue.Enqueue(() =>
                    {
                        Interlocked.Decrement(ref s_RunningCount);
                        if (onComplete != null) onComplete(result);
                    });
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary>Runs several commands in order, stopping at the first failure.</summary>
        public static void RunSequenceAsync(IList<string> commands, Action<GitResult> onEachComplete, Action<bool> onFinished)
        {
            RunNext(commands, 0, onEachComplete, onFinished);
        }

        static void RunNext(IList<string> commands, int index, Action<GitResult> onEach, Action<bool> onFinished)
        {
            if (index >= commands.Count)
            {
                if (onFinished != null) onFinished(true);
                return;
            }

            RunAsync(commands[index], result =>
            {
                if (onEach != null) onEach(result);

                if (!result.Ok)
                {
                    if (onFinished != null) onFinished(false);
                    return;
                }

                RunNext(commands, index + 1, onEach, onFinished);
            });
        }

        /// <summary>Quotes a path for a git argument list.</summary>
        public static string Quote(string path)
        {
            return "\"" + path.Replace("\"", "\\\"") + "\"";
        }

        static void HookPump()
        {
            if (s_PumpHooked) return;
            s_PumpHooked = true;
            EditorApplication.update += Pump;
        }

        static void Pump()
        {
            while (true)
            {
                Action action;
                lock (s_MainThreadQueue)
                {
                    if (s_MainThreadQueue.Count == 0) return;
                    action = s_MainThreadQueue.Dequeue();
                }

                try { action(); }
                catch (Exception e) { UnityEngine.Debug.LogException(e); }
            }
        }
    }
}
