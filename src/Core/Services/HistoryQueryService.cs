using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DhCodetaskExtension.Core.Interfaces;
using DhCodetaskExtension.Core.Models;
using DhCodetaskExtension.Core.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DhCodetaskExtension.Core.Services
{
    public sealed class HistoryQueryService : IHistoryRepository, IDisposable
    {
        private readonly Func<string> _historyDirProvider;
        private List<CompletionReport> _cache;
        private FileSystemWatcher      _watcher;
        private volatile bool          _cacheDirty = true;
        private readonly object        _lock       = new object();

        public HistoryQueryService(Func<string> historyDirProvider)
        {
            _historyDirProvider = historyDirProvider
                ?? throw new ArgumentNullException(nameof(historyDirProvider));
        }

        public void StartWatcher()
        {
            var dir = _historyDirProvider();
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            try
            {
                _watcher = new FileSystemWatcher(dir, "*.json")
                {
                    IncludeSubdirectories = true,
                    NotifyFilter          = NotifyFilters.FileName | NotifyFilters.LastWrite,
                    EnableRaisingEvents   = true
                };
                _watcher.Created += (s, e) => _cacheDirty = true;
                _watcher.Deleted += (s, e) => _cacheDirty = true;
            }
            catch { /* non-critical */ }
        }

        private async Task<List<CompletionReport>> GetCacheAsync()
        {
            if (!_cacheDirty && _cache != null) return _cache;

            var reports = new List<CompletionReport>();
            var dir     = _historyDirProvider();
            if (!Directory.Exists(dir)) return reports;

            var files = await Task.Run(() =>
                Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories));

            foreach (var f in files)
            {
                try
                {
                    var rawJson = await Task.Run(() => File.ReadAllText(f, Encoding.UTF8));
                    var r       = JsonConvert.DeserializeObject<CompletionReport>(rawJson);
                    if (r == null) continue;

                    // ── Verify checksum ───────────────────────────────────
                    VerifyReportChecksum(r, rawJson);

                    reports.Add(r);
                }
                catch { /* skip corrupt */ }
            }

            reports = reports.OrderByDescending(r => r.CompletedAt).ToList();
            lock (_lock) { _cache = reports; _cacheDirty = false; }
            return reports;
        }

        /// <summary>
        /// Re-serialize the report WITHOUT the Checksum field, compute hash, compare.
        /// Sets CompletionReport.ChecksumValid accordingly.
        /// </summary>
        private static void VerifyReportChecksum(CompletionReport report, string rawJson)
        {
            if (string.IsNullOrEmpty(report.Checksum)) return;
            try
            {
                // Parse raw JSON, remove the "Checksum" key, re-serialize
                var obj = JObject.Parse(rawJson);
                obj.Remove("Checksum");
                var withoutChecksum = obj.ToString(Formatting.Indented);
                report.VerifyChecksum(withoutChecksum);
            }
            catch { /* checksum verification non-critical */ }
        }

        public async Task<IEnumerable<CompletionReport>> GetAllAsync()
            => await GetCacheAsync();

        public async Task<IEnumerable<CompletionReport>> GetByDateRangeAsync(DateTime from, DateTime to)
        {
            var all = await GetCacheAsync();
            return all.Where(r => r.CompletedAt >= from && r.CompletedAt <= to);
        }

        public async Task<IEnumerable<CompletionReport>> GetTodayAsync()
            => await GetByDateRangeAsync(DateTime.Today, DateTime.Today.AddDays(1).AddTicks(-1));

        public async Task<IEnumerable<CompletionReport>> GetThisWeekAsync()
        {
            var today = DateTime.Today;
            int diff  = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            var monday = today.AddDays(-diff);
            var sunday = monday.AddDays(7).AddTicks(-1);
            return await GetByDateRangeAsync(monday, sunday);
        }

        public async Task<IEnumerable<CompletionReport>> GetThisMonthAsync()
        {
            var first = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var last  = first.AddMonths(1).AddTicks(-1);
            return await GetByDateRangeAsync(first, last);
        }

        public Task DeleteAsync(string reportId)
        {
            return Task.Run(() =>
            {
                lock (_lock)
                {
                    if (_cache == null) return;
                    var r = _cache.FirstOrDefault(x => x.ReportId == reportId);
                    if (r == null) return;
                    TryDelete(r.JsonFilePath);
                    TryDelete(r.MarkdownFilePath);
                    _cacheDirty = true;
                }
            });
        }

        private static void TryDelete(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                try { File.Delete(path); } catch { }
        }

        public void Dispose() { _watcher?.Dispose(); }
    }
}
