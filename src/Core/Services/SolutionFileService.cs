using System;
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
            return (DateTime.UtcNow - _cacheTime).TotalMinutes >= ttl;
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

            // 2. Re-scan
            var root = _settings().DirectoryRootDhHosCodePath;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                _cache     = new List<SolutionFileEntry>();
                _cacheTime = DateTime.UtcNow;
                return;
            }

            var found = await Task.Run(() => ScanDirectory(root));
            _cache     = found.OrderBy(f => f.FileName, StringComparer.OrdinalIgnoreCase).ToList();
            _cacheTime = DateTime.UtcNow;

            // 3. Persist to disk
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

        private static List<SolutionFileEntry> ScanDirectory(string root)
        {
            var result = new List<SolutionFileEntry>();
            try
            {
                var files = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
                    .Where(f =>
                    {
                        var ext = Path.GetExtension(f).ToLower();
                        return ext == ".sln" || ext == ".csproj";
                    });

                foreach (var f in files)
                {
                    try
                    {
                        var rel = f.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                            ? f.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar,
                                                                   Path.AltDirectorySeparatorChar)
                            : f;

                        result.Add(new SolutionFileEntry
                        {
                            FileName     = Path.GetFileName(f),
                            FullPath     = f,
                            RelativePath = rel,
                            Extension    = Path.GetExtension(f).ToLower(),
                            LastModified = File.GetLastWriteTime(f)
                        });
                    }
                    catch { /* skip inaccessible */ }
                }
            }
            catch { /* non-critical */ }
            return result;
        }

        private sealed class PersistedCache
        {
            public DateTime              SavedAt { get; set; }
            public List<SolutionFileEntry> Entries { get; set; }
        }
    }
}
