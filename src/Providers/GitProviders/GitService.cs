using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DhCodetaskExtension.Core.Interfaces;
using DhCodetaskExtension.Core.Models;

namespace DhCodetaskExtension.Providers.GitProviders
{
    // ── Private result type thay cho ValueTuple (không dùng được ở .NET 4.6) ──
    internal sealed class GitRunResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
    }

    public sealed class GitService : IGitService
    {
        private readonly Func<AppSettings> _settingsProvider;

        public GitService(Func<AppSettings> settingsProvider)
        {
            _settingsProvider = settingsProvider
                ?? throw new ArgumentNullException(nameof(settingsProvider));
        }

        public bool IsAvailable()
        {
            try
            {
                var r = RunGit(".", "--version");
                return r.ExitCode == 0;
            }
            catch { return false; }
        }

        public string FindRepoRoot(string startPath)
        {
            if (string.IsNullOrEmpty(startPath)) return null;
            var dir = Directory.Exists(startPath) ? startPath : Path.GetDirectoryName(startPath);
            while (!string.IsNullOrEmpty(dir))
            {
                if (Directory.Exists(Path.Combine(dir, ".git"))) return dir;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        public async Task<string> GetCurrentBranchAsync(string repoRoot)
        {
            if (string.IsNullOrEmpty(repoRoot)) return "unknown";
            var r = await Task.Run(() => RunGit(repoRoot, "rev-parse --abbrev-ref HEAD"));
            return r.ExitCode == 0 ? r.Output.Trim() : "unknown";
        }

        public async Task<GitResult> PushAndCompleteAsync(string repoRoot, string commitMessage, bool autoPush)
        {
            if (string.IsNullOrEmpty(repoRoot))
                return new GitResult { Error = "Repository root not found." };

            try
            {
                var settings = _settingsProvider();

                if (!string.IsNullOrEmpty(settings.GitUserName))
                    await Task.Run(() => RunGit(repoRoot,
                        string.Format("config user.name \"{0}\"", settings.GitUserName)));
                if (!string.IsNullOrEmpty(settings.GitUserEmail))
                    await Task.Run(() => RunGit(repoRoot,
                        string.Format("config user.email \"{0}\"", settings.GitUserEmail)));

                // git add -A
                var addResult = await Task.Run(() => RunGit(repoRoot, "add -A"));
                if (addResult.ExitCode != 0)
                    return new GitResult { Error = "git add failed: " + addResult.Error };

                // git commit
                var safeMsg = commitMessage.Replace("\"", "'");
                var commitResult = await Task.Run(() =>
                    RunGit(repoRoot, string.Format("commit -m \"{0}\"", safeMsg)));
                if (commitResult.ExitCode != 0)
                    return new GitResult { Error = "git commit failed: " + commitResult.Error };

                // git push (optional)
                if (autoPush)
                {
                    var pushResult = await Task.Run(() => RunGit(repoRoot, "push"));
                    if (pushResult.ExitCode != 0)
                        return new GitResult
                        {
                            Error = "git push failed: " + pushResult.Error,
                            Output = commitResult.Output
                        };
                }

                // get commit hash
                var hashResult = await Task.Run(() => RunGit(repoRoot, "rev-parse HEAD"));
                var hash = hashResult.ExitCode == 0 ? hashResult.Output.Trim() : string.Empty;
                if (hash.Length > 7) hash = hash.Substring(0, 7);

                return new GitResult
                {
                    Success = true,
                    Output = commitResult.Output,
                    CommitHash = hash
                };
            }
            catch (Exception ex)
            {
                return new GitResult { Error = ex.Message };
            }
        }

        // ── Private helper — returns plain class, NO ValueTuple ──────────────
        private static GitRunResult RunGit(string workDir, string args)
        {
            var psi = new ProcessStartInfo("git", args)
            {
                WorkingDirectory = workDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var p = Process.Start(psi))
            {
                var output = p.StandardOutput.ReadToEnd();
                var error = p.StandardError.ReadToEnd();
                p.WaitForExit(30000);
                return new GitRunResult
                {
                    ExitCode = p.ExitCode,
                    Output = output,
                    Error = error
                };
            }
        }
    }
}