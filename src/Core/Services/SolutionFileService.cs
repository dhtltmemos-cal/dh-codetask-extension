using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DhCodetaskExtension.Core.Models;
using DhCodetaskExtension.Core.Services;
using Newtonsoft.Json;

namespace DhCodetaskExtension.Core.Services
{
    /// <summary>
    /// Scans a root directory for .sln and .csproj files, caches results with
    /// a configurable TTL, and provides filtering by name.
    /// Cache is stored at {storageRoot}\solution-file-cache.json.
    ///
    /// v3.5 optimizations:
    /// - EnumerateFiles (lazy) instead of GetFiles (eager)
    /// - Parallel.ForEach with ConcurrentBag for multi-core scan
    /// - Persisted cache read/written atomically
    /// </summary>
    public sealed class SolutionFileService
    {
        private readonly Func<AppSettings> _settings;
        private readonly Func<string>      _storageRoot;

        private List<SolutionFileEntry>    _cache;
        private DateTime                   _cacheTime = DateTime.MinValue;

        public SolutionFileService(Func<AppSettings> settings, Func<string> storageRoot)
        {
            _settings    = settings    ?? throw new ArgumentNullException(nameof(settings));
            _storageRoot = storageRoot ?? throw new ArgumentNullException(nameof(storageRoot));
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Returns cached list (reloading if expired or first call).
        /// keyword filter is applied in-memory — empty returns all.
        /// </summary>
        public async Task<List<SolutionFileEntry>> GetFilesAsync(string keyword = null)
        {
            await EnsureCacheAsync();

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

        /// <summary>Force re-scan regardless of TTL.</summary>
        public async Task RefreshAsync()
        {
            _cache     = null;
            _cacheTime = DateTime.MinValue;
            await EnsureCacheAsync();
        }

        public bool IsCacheExpired()
        {
            if (_cache == null) return true;
            var ttl = _settings().SolutionFileCacheMinutes;
            if (ttl <= 0) ttl = 20;
            return (DateTime.UtcNow - _cacheTime).TotalMinutes >= ttl;
        }

        /// <summary>Returns cache age in human-readable format, or null if no cache.</summary>
        public string GetCacheAgeDisplay()
        {
            if (_cache == null) return null;
            var age = DateTime.UtcNow - _cacheTime;
            if (age.TotalSeconds < 60) return string.Format("{0}s", (int)age.TotalSeconds);
            if (age.TotalMinutes < 60) return string.Format("{0}m {1}s", (int)age.TotalMinutes, age.Seconds);
            return string.Format("{0}h {1}m", (int)age.TotalHours, age.Minutes);
        }

        // ── Private ───────────────────────────────────────────────────────

        private async Task EnsureCacheAsync()
        {
            if (!IsCacheExpired()) return;

            var cachePath = Path.Combine(_storageRoot(), "solution-file-cache.json");

            // 1. Try reading persisted cache (still valid by timestamp inside file)
            if (_cache == null && File.Exists(cachePath))
            {
                try
                {
                    var persisted = await Task.Run(() =>
                        JsonConvert.DeserializeObject<PersistedCache>(
                            File.ReadAllText(cachePath, Encoding.UTF8)));

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

            // 2. Re-scan with parallel processing
            var root = _settings().DirectoryRootDhHosCodePath;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                _cache     = new List<SolutionFileEntry>();
                _cacheTime = DateTime.UtcNow;
                return;
            }

            var found = await Task.Run(() => ScanDirectoryParallel(root));
            _cache     = found.OrderBy(f => f.FileName, StringComparer.OrdinalIgnoreCase).ToList();
            _cacheTime = DateTime.UtcNow;

            // 3. Persist to disk atomically
            try
            {
                var persisted = new PersistedCache { SavedAt = _cacheTime, Entries = _cache };
                var json = JsonConvert.SerializeObject(persisted, Formatting.Indented);
                var dir  = _storageRoot();
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                await AtomicFile.WriteAllTextAsync(cachePath, json);
            }
            catch { /* non-critical */ }
        }

        /// <summary>
        /// Parallel scan using ConcurrentBag + Parallel.ForEach.
        /// Uses Directory.EnumerateFiles (lazy) to start processing early.
        /// </summary>
        private static List<SolutionFileEntry> ScanDirectoryParallel(string root)
        {
            var bag = new ConcurrentBag<SolutionFileEntry>();
            try
            {
                // EnumerateFiles is lazy — starts yielding immediately
                var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                    .Where(f =>
                    {
                        var ext = Path.GetExtension(f);
                        return string.Equals(ext, ".sln",    StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(ext, ".csproj", StringComparison.OrdinalIgnoreCase);
                    });

                // Parallel.ForEach uses thread pool — much faster on large trees
                System.Threading.Tasks.Parallel.ForEach(files, f =>
                {
                    try
                    {
                        var rel = f.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                            ? f.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar,
                                                                   Path.AltDirectorySeparatorChar)
                            : f;

                        bag.Add(new SolutionFileEntry
                        {
                            FileName     = Path.GetFileName(f),
                            FullPath     = f,
                            RelativePath = rel,
                            Extension    = Path.GetExtension(f).ToLower(),
                            LastModified = File.GetLastWriteTime(f)
                        });
                    }
                    catch { /* skip inaccessible */ }
                });
            }
            catch { /* non-critical */ }
            return bag.ToList();
        }

        private sealed class PersistedCache
        {
            public DateTime              SavedAt { get; set; }
            public List<SolutionFileEntry> Entries { get; set; }
        }
    }
}
