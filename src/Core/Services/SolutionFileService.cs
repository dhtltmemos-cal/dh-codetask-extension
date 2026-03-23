using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DhCodetaskExtension.Core.Models;
using Newtonsoft.Json;

namespace DhCodetaskExtension.Core.Services
{
    /// <summary>
    /// Scans a root directory for .sln and .csproj files.
    ///
    /// v3.11: Ưu tiên dùng ripgrep (nhanh, tự ignore .git/node_modules/bin/obj...).
    /// Fallback về Directory.EnumerateFiles nếu RipgrepPath chưa cấu hình hoặc lỗi.
    /// Hỗ trợ ExcludePatterns cấu hình được, progress reporting, CancellationToken.
    /// Cache persist JSON với TTL cấu hình được.
    /// </summary>
    public sealed class SolutionFileService
    {
        private readonly Func<AppSettings> _settings;
        private readonly Func<string>      _storageRoot;

        private List<SolutionFileEntry>    _cache;
        private DateTime                   _cacheTime = DateTime.MinValue;

        // Default patterns to exclude when using filesystem scan
        private static readonly string[] DefaultExcludeDirs =
        {
            "node_modules", ".git", "bin", "obj", "packages",
            ".vs", ".idea", "__pycache__", "dist", "build",
            ".nuget", "TestResults", "Artifacts"
        };

        public SolutionFileService(Func<AppSettings> settings, Func<string> storageRoot)
        {
            _settings    = settings    ?? throw new ArgumentNullException(nameof(settings));
            _storageRoot = storageRoot ?? throw new ArgumentNullException(nameof(storageRoot));
        }

        // ── Public API ────────────────────────────────────────────────────

        public async Task<List<SolutionFileEntry>> GetFilesAsync(string keyword = null)
        {
            await EnsureCacheAsync(CancellationToken.None, null);
            var list = _cache ?? new List<SolutionFileEntry>();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.ToLower();
                list = list.Where(f =>
                    f.FileName.ToLower().Contains(kw) ||
                    f.RelativePath.ToLower().Contains(kw)).ToList();
            }
            return list;
        }

        /// <summary>
        /// Force re-scan with progress reporting and cancellation support.
        /// onProgress(message) called periodically during scan.
        /// </summary>
        public async Task RefreshAsync(
            Action<string> onProgress = null,
            CancellationToken ct = default(CancellationToken))
        {
            _cache     = null;
            _cacheTime = DateTime.MinValue;
            await EnsureCacheAsync(ct, onProgress);
        }

        public bool IsCacheExpired()
        {
            if (_cache == null) return true;
            var ttl = _settings().SolutionFileCacheMinutes;
            if (ttl <= 0) ttl = 20;
            return (DateTime.UtcNow - _cacheTime).TotalMinutes >= ttl;
        }

        public string GetCacheAgeDisplay()
        {
            if (_cache == null) return null;
            var age = DateTime.UtcNow - _cacheTime;
            if (age.TotalSeconds < 60) return string.Format("{0}s", (int)age.TotalSeconds);
            if (age.TotalMinutes < 60) return string.Format("{0}m {1}s", (int)age.TotalMinutes, age.Seconds);
            return string.Format("{0}h {1}m", (int)age.TotalHours, age.Minutes);
        }

        /// <summary>Returns which scan mode will be used given current settings.</summary>
        public string GetScanModeDisplay()
        {
            var s = _settings();
            return !string.IsNullOrEmpty(s.RipgrepPath) && File.Exists(s.RipgrepPath)
                ? "ripgrep"
                : "filesystem";
        }

        // ── Private ───────────────────────────────────────────────────────

        private async Task EnsureCacheAsync(CancellationToken ct, Action<string> onProgress)
        {
            if (!IsCacheExpired()) return;

            var cachePath = Path.Combine(_storageRoot(), "solution-file-cache.json");

            // 1. Try reading persisted cache
            if (_cache == null && File.Exists(cachePath))
            {
                try
                {
                    var persisted = await Task.Run(() =>
                        JsonConvert.DeserializeObject<PersistedCache>(
                            File.ReadAllText(cachePath, Encoding.UTF8)), ct);
                    if (persisted != null)
                    {
                        var ttl = _settings().SolutionFileCacheMinutes;
                        if (ttl <= 0) ttl = 20;
                        if ((DateTime.UtcNow - persisted.SavedAt).TotalMinutes < ttl)
                        {
                            _cache     = persisted.Entries ?? new List<SolutionFileEntry>();
                            _cacheTime = persisted.SavedAt;
                            return;
                        }
                    }
                }
                catch { /* corrupt cache — re-scan */ }
            }

            var s = _settings();
            var root = s.DirectoryRootDhHosCodePath;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                _cache     = new List<SolutionFileEntry>();
                _cacheTime = DateTime.UtcNow;
                return;
            }

            // 2. Choose scan strategy
            List<SolutionFileEntry> found;
            bool useRipgrep = !string.IsNullOrEmpty(s.RipgrepPath) && File.Exists(s.RipgrepPath);

            if (useRipgrep)
            {
                onProgress?.Invoke(string.Format("⚡ Đang quét bằng ripgrep: {0}", root));
                try
                {
                    found = await ScanWithRipgrepAsync(s.RipgrepPath, root, s, ct, onProgress);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    // Fallback to filesystem on any ripgrep error
                    onProgress?.Invoke(string.Format("⚠ ripgrep lỗi ({0}), chuyển sang quét thường...", ex.Message));
                    found = await ScanWithFilesystemAsync(root, s, ct, onProgress);
                }
            }
            else
            {
                onProgress?.Invoke(string.Format("📂 Đang quét: {0}", root));
                onProgress?.Invoke("💡 Tip: Cấu hình RipgrepPath để quét nhanh hơn.");
                found = await ScanWithFilesystemAsync(root, s, ct, onProgress);
            }

            ct.ThrowIfCancellationRequested();

            _cache     = found.OrderBy(f => f.FileName, StringComparer.OrdinalIgnoreCase).ToList();
            _cacheTime = DateTime.UtcNow;

            onProgress?.Invoke(string.Format("✅ Tìm thấy {0} file — đang lưu cache...", _cache.Count));

            // 3. Persist
            try
            {
                var dir = _storageRoot();
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var persisted = new PersistedCache { SavedAt = _cacheTime, Entries = _cache };
                var json = JsonConvert.SerializeObject(persisted, Formatting.Indented);
                await AtomicFile.WriteAllTextAsync(cachePath, json);
            }
            catch { /* non-critical */ }
        }

        // ── Ripgrep scan ──────────────────────────────────────────────────

        private static async Task<List<SolutionFileEntry>> ScanWithRipgrepAsync(
            string rgPath, string root, AppSettings settings,
            CancellationToken ct, Action<string> onProgress)
        {
            var sw = Stopwatch.StartNew();

            // Build exclude globs: defaults + user config
            var excludes = new List<string>(DefaultExcludeDirs);
            if (settings.ScanExcludePatterns != null)
                excludes.AddRange(settings.ScanExcludePatterns);

            var sbArgs = new StringBuilder();
            sbArgs.Append("--files");
            sbArgs.Append(" --glob \"*.sln\"");
            sbArgs.Append(" --glob \"*.csproj\"");
            // ripgrep uses ! prefix for negation in --glob
            foreach (var ex in excludes.Distinct(StringComparer.OrdinalIgnoreCase))
                sbArgs.AppendFormat(" --glob \"!{0}\"", ex.Trim('/','\\'));
            // Don't follow symlinks to avoid loops
            sbArgs.Append(" --no-follow");
            sbArgs.AppendFormat(" -- \"{0}\"", root.TrimEnd('\\', '/'));

            var args = sbArgs.ToString();
            onProgress?.Invoke(string.Format("⚡ ripgrep: {0}", args));

            var results = new List<SolutionFileEntry>();

            await Task.Run(() =>
            {
                ProcessStartInfo psi;
                try
                {
                    psi = new ProcessStartInfo(rgPath, args)
                    {
                        UseShellExecute        = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        CreateNoWindow         = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding  = Encoding.UTF8
                    };
                }
                catch
                {
                    // If direct fails, try via cmd.exe (32-bit VS2017 host / 64-bit rg.exe)
                    psi = new ProcessStartInfo("cmd.exe",
                        string.Format("/c \"{0}\" {1}", rgPath, args))
                    {
                        UseShellExecute        = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        CreateNoWindow         = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding  = Encoding.UTF8
                    };
                }

                using (var proc = Process.Start(psi))
                {
                    if (proc == null) return;

                    ct.Register(() =>
                    {
                        try { if (!proc.HasExited) proc.Kill(); }
                        catch { }
                    });

                    // Read stderr async to avoid deadlock
                    var stderrTask = System.Threading.Tasks.Task.Run(
                        () => proc.StandardError.ReadToEnd());

                    string line;
                    int count = 0;
                    while ((line = proc.StandardOutput.ReadLine()) != null)
                    {
                        if (ct.IsCancellationRequested) break;
                        line = line.Trim();
                        if (string.IsNullOrEmpty(line)) continue;

                        try
                        {
                            var entry = BuildEntry(line, root);
                            if (entry != null)
                            {
                                results.Add(entry);
                                count++;
                                if (count % 50 == 0)
                                    onProgress?.Invoke(string.Format("⚡ Đã tìm thấy {0} file...", count));
                            }
                        }
                        catch { /* skip bad line */ }
                    }

                    proc.WaitForExit(15000);
                    stderrTask.Wait(2000);
                }
            }, ct);

            sw.Stop();
            onProgress?.Invoke(string.Format("⚡ ripgrep hoàn thành: {0} file trong {1}ms",
                results.Count, sw.ElapsedMilliseconds));

            return results;
        }

        // ── Filesystem scan (fallback) ─────────────────────────────────────

        private static async Task<List<SolutionFileEntry>> ScanWithFilesystemAsync(
            string root, AppSettings settings,
            CancellationToken ct, Action<string> onProgress)
        {
            var sw = Stopwatch.StartNew();
            var bag = new ConcurrentBag<SolutionFileEntry>();

            var excludes = new HashSet<string>(DefaultExcludeDirs,
                StringComparer.OrdinalIgnoreCase);
            if (settings.ScanExcludePatterns != null)
                foreach (var p in settings.ScanExcludePatterns) excludes.Add(p);

            await Task.Run(() =>
            {
                ScanDirectoryRecursive(root, root, excludes, bag, ct, onProgress, 0,
                    settings.ScanMaxDepth > 0 ? settings.ScanMaxDepth : int.MaxValue);
            }, ct);

            sw.Stop();
            var list = bag.ToList();
            onProgress?.Invoke(string.Format("📂 Filesystem scan: {0} file trong {1}ms",
                list.Count, sw.ElapsedMilliseconds));
            return list;
        }

        private static void ScanDirectoryRecursive(
            string dir, string root,
            HashSet<string> excludes,
            ConcurrentBag<SolutionFileEntry> bag,
            CancellationToken ct,
            Action<string> onProgress,
            int depth, int maxDepth)
        {
            if (ct.IsCancellationRequested) return;
            if (depth > maxDepth) return;

            try
            {
                // Add matching files in this directory
                foreach (var f in Directory.EnumerateFiles(dir))
                {
                    if (ct.IsCancellationRequested) return;
                    var ext = Path.GetExtension(f);
                    if (string.Equals(ext, ".sln",    StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(ext, ".csproj", StringComparison.OrdinalIgnoreCase))
                    {
                        var entry = BuildEntry(f, root);
                        if (entry != null) bag.Add(entry);
                    }
                }

                // Recurse into subdirectories, skip excluded ones
                var subdirs = Directory.EnumerateDirectories(dir).ToList();
                Parallel.ForEach(subdirs, new ParallelOptions { CancellationToken = ct }, subdir =>
                {
                    var name = Path.GetFileName(subdir);
                    if (excludes.Contains(name)) return;
                    // Skip hidden dirs starting with . (e.g. .git, .vs)
                    if (name.StartsWith(".", StringComparison.Ordinal)) return;
                    ScanDirectoryRecursive(subdir, root, excludes, bag, ct,
                        onProgress, depth + 1, maxDepth);
                });
            }
            catch (UnauthorizedAccessException) { /* skip inaccessible */ }
            catch (PathTooLongException)        { /* skip long paths */ }
            catch (OperationCanceledException) { throw; }
            catch { /* skip other errors */ }
        }

        // ── Shared helpers ────────────────────────────────────────────────

        private static SolutionFileEntry BuildEntry(string fullPath, string root)
        {
            if (string.IsNullOrEmpty(fullPath)) return null;
            try
            {
                string rel = fullPath;
                if (!string.IsNullOrEmpty(root) &&
                    fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    rel = fullPath.Substring(root.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                return new SolutionFileEntry
                {
                    FileName     = Path.GetFileName(fullPath),
                    FullPath     = fullPath,
                    RelativePath = rel,
                    Extension    = Path.GetExtension(fullPath).ToLower(),
                    LastModified = File.GetLastWriteTime(fullPath)
                };
            }
            catch { return null; }
        }

        private sealed class PersistedCache
        {
            public DateTime              SavedAt { get; set; }
            public List<SolutionFileEntry> Entries { get; set; }
        }
    }
}
