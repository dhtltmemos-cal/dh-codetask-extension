using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using DhCodetaskExtension.Core.Models;
using DhCodetaskExtension.Core.Services;
using Newtonsoft.Json.Linq;

namespace DhCodetaskExtension.ViewModels
{
    public enum ProjectSortMode
    {
        NameAsc,
        NameDesc,
        DateDesc,
        DateAsc
    }

    public enum ProjectFileType
    {
        All,
        SlnOnly,
        CsprojOnly
    }

    public sealed class ProjectHelperViewModel : INotifyPropertyChanged
    {
        private readonly SolutionFileService _service;
        private readonly Func<AppSettings>   _getSettings;
        private readonly Action<string>      _log;

        private List<SolutionFileEntry> _allFiles = new List<SolutionFileEntry>();

        // ── File list state ───────────────────────────────────────────────

        private string          _filterKeyword  = string.Empty;
        private ProjectSortMode _sortMode        = ProjectSortMode.NameAsc;
        private ProjectFileType _fileType        = ProjectFileType.All;
        private bool            _isLoading;
        private string          _statusMessage   = "Nhấn 🔄 để quét thư mục.";

        // ── Panel mode ────────────────────────────────────────────────────

        private bool _isFileMode = true;

        public bool IsFileMode
        {
            get => _isFileMode;
            set { _isFileMode = value; OnProp(nameof(IsFileMode)); OnProp(nameof(IsSearchMode)); }
        }

        public bool IsSearchMode => !_isFileMode;

        // ── Properties ────────────────────────────────────────────────────

        public string FilterKeyword
        {
            get => _filterKeyword;
            set { _filterKeyword = value; OnProp(nameof(FilterKeyword)); }
        }

        public ProjectSortMode SortMode
        {
            get => _sortMode;
            set { _sortMode = value; OnProp(nameof(SortMode)); ApplyFilter(); RaiseSortFlags(); }
        }

        public ProjectFileType FileType
        {
            get => _fileType;
            set { _fileType = value; OnProp(nameof(FileType)); ApplyFilter(); RaiseTypeFlags(); }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnProp(nameof(IsLoading)); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnProp(nameof(StatusMessage)); }
        }

        public bool SortNameAsc  { get => SortMode == ProjectSortMode.NameAsc;  set { if (value) SortMode = ProjectSortMode.NameAsc; } }
        public bool SortNameDesc { get => SortMode == ProjectSortMode.NameDesc; set { if (value) SortMode = ProjectSortMode.NameDesc; } }
        public bool SortDateDesc { get => SortMode == ProjectSortMode.DateDesc; set { if (value) SortMode = ProjectSortMode.DateDesc; } }
        public bool SortDateAsc  { get => SortMode == ProjectSortMode.DateAsc;  set { if (value) SortMode = ProjectSortMode.DateAsc; } }

        public bool TypeAll     { get => FileType == ProjectFileType.All;        set { if (value) FileType = ProjectFileType.All; } }
        public bool TypeSln     { get => FileType == ProjectFileType.SlnOnly;    set { if (value) FileType = ProjectFileType.SlnOnly; } }
        public bool TypeCsproj  { get => FileType == ProjectFileType.CsprojOnly; set { if (value) FileType = ProjectFileType.CsprojOnly; } }

        public ObservableCollection<SolutionFileEntry> Files { get; }
            = new ObservableCollection<SolutionFileEntry>();

        // ── Ripgrep content search ─────────────────────────────────────────

        private string _contentQuery   = string.Empty;
        private bool   _isSearching;
        private string _searchStatus   = string.Empty;
        private CancellationTokenSource _searchCts;

        public string ContentQuery
        {
            get => _contentQuery;
            set { _contentQuery = value; OnProp(nameof(ContentQuery)); }
        }

        public bool IsSearching
        {
            get => _isSearching;
            set { _isSearching = value; OnProp(nameof(IsSearching)); }
        }

        public string SearchStatus
        {
            get => _searchStatus;
            set { _searchStatus = value; OnProp(nameof(SearchStatus)); }
        }

        public ObservableCollection<RipgrepSearchResult> SearchResults { get; }
            = new ObservableCollection<RipgrepSearchResult>();

        // ── Commands ──────────────────────────────────────────────────────
        public ICommand RefreshCommand       { get; }
        public ICommand FilterCommand        { get; }
        public ICommand ClearFilterCommand   { get; }
        public ICommand SearchContentCommand { get; }
        public ICommand CancelSearchCommand  { get; }
        public ICommand ClearSearchCommand   { get; }
        public RelayCommand<SolutionFileEntry>   OpenInVsCommand   { get; }
        public RelayCommand<SolutionFileEntry>   CopyPathCommand   { get; }
        public RelayCommand<SolutionFileEntry>   OpenFolderCommand { get; }
        public RelayCommand<RipgrepSearchResult> OpenSearchResultCommand { get; }

        // ── Injected actions ──────────────────────────────────────────────
        public Action<string> OpenFileAction   { get; set; }
        public Action<string> CopyToClipboard  { get; set; }
        public Action<string> OpenFolderAction { get; set; }

        public ProjectHelperViewModel(SolutionFileService service, Func<AppSettings> getSettings, Action<string> log)
        {
            _service     = service     ?? throw new ArgumentNullException(nameof(service));
            _getSettings = getSettings ?? throw new ArgumentNullException(nameof(getSettings));
            _log         = log         ?? (_ => { });

            RefreshCommand     = new RelayCommand(RefreshFireAndForget);
            FilterCommand      = new RelayCommand(ApplyFilter);
            ClearFilterCommand = new RelayCommand(() => { FilterKeyword = string.Empty; ApplyFilter(); });

            SearchContentCommand = new RelayCommand(
                () => { var _ = SearchContentAsync(); },
                () => !IsSearching && !string.IsNullOrWhiteSpace(ContentQuery));

            CancelSearchCommand = new RelayCommand(CancelSearch, () => IsSearching);

            ClearSearchCommand = new RelayCommand(() =>
            {
                ContentQuery = string.Empty;
                SearchResults.Clear();
                SearchStatus = string.Empty;
            });

            OpenInVsCommand = new RelayCommand<SolutionFileEntry>(entry =>
            {
                if (entry == null) return;
                _log(string.Format("[ProjectHelper] 📂 Open: {0}", entry.FullPath));
                OpenFileAction?.Invoke(entry.FullPath);
            });

            CopyPathCommand = new RelayCommand<SolutionFileEntry>(entry =>
            {
                if (entry == null) return;
                _log(string.Format("[ProjectHelper] 📋 Copy path: {0}", entry.FullPath));
                CopyToClipboard?.Invoke(entry.FullPath);
            });

            OpenFolderCommand = new RelayCommand<SolutionFileEntry>(entry =>
            {
                if (entry == null) return;
                var dir = System.IO.Path.GetDirectoryName(entry.FullPath) ?? string.Empty;
                _log(string.Format("[ProjectHelper] 📁 Open folder: {0}", dir));
                OpenFolderAction?.Invoke(dir);
            });

            OpenSearchResultCommand = new RelayCommand<RipgrepSearchResult>(r =>
            {
                if (r == null || string.IsNullOrEmpty(r.FilePath)) return;
                _log(string.Format("[ProjectHelper] 🔍 Open search result: {0}:{1}", r.FilePath, r.LineNumber));
                OpenFileAction?.Invoke(r.FilePath);
            });
        }

        // ── Refresh ───────────────────────────────────────────────────────

        private void RefreshFireAndForget() { var _ = RefreshAsync(); }

        public async Task RefreshAsync(bool forceRescan = false)
        {
            IsLoading     = true;
            StatusMessage = "⏳ Đang quét...";
            _log("[ProjectHelper] 🔍 Refreshing file list...");
            try
            {
                if (forceRescan || _service.IsCacheExpired())
                    await _service.RefreshAsync();

                _allFiles = await _service.GetFilesAsync();
                var age = _service.GetCacheAgeDisplay();
                _log(string.Format("[ProjectHelper] ✅ Found {0} file(s). Cache age: {1}",
                    _allFiles.Count, age ?? "fresh"));
                ApplyFilter();
            }
            catch (Exception ex)
            {
                StatusMessage = "❌ " + ex.Message;
                _log("[ProjectHelper] ❌ " + ex.Message);
            }
            finally { IsLoading = false; }
        }

        // ── Filter + Sort ─────────────────────────────────────────────────

        public void ApplyFilter()
        {
            var src = _allFiles.AsEnumerable();

            switch (FileType)
            {
                case ProjectFileType.SlnOnly:    src = src.Where(f => f.Extension == ".sln");    break;
                case ProjectFileType.CsprojOnly: src = src.Where(f => f.Extension == ".csproj"); break;
            }

            if (!string.IsNullOrWhiteSpace(FilterKeyword))
            {
                var kw = FilterKeyword.ToLower();
                src = src.Where(f =>
                    f.FileName.ToLower().Contains(kw) ||
                    f.RelativePath.ToLower().Contains(kw));
            }

            switch (SortMode)
            {
                case ProjectSortMode.NameAsc:  src = src.OrderBy(f => f.FileName, StringComparer.OrdinalIgnoreCase); break;
                case ProjectSortMode.NameDesc: src = src.OrderByDescending(f => f.FileName, StringComparer.OrdinalIgnoreCase); break;
                case ProjectSortMode.DateDesc: src = src.OrderByDescending(f => f.LastModified); break;
                case ProjectSortMode.DateAsc:  src = src.OrderBy(f => f.LastModified); break;
            }

            var result = src.ToList();
            Files.Clear();
            foreach (var f in result) Files.Add(f);

            var age = _service.GetCacheAgeDisplay();
            var ageInfo = age != null ? string.Format(" (cache: {0} trước)", age) : string.Empty;
            StatusMessage = result.Count == 0
                ? (_allFiles.Count == 0
                    ? "Chưa quét. Cấu hình DirectoryRootDhHosCodePath và nhấn 🔄."
                    : "Không tìm thấy file nào khớp.")
                : string.Format("{0} file{1} — {2:HH:mm:ss}", result.Count, ageInfo, DateTime.Now);
        }

        private void RaiseSortFlags()
        {
            OnProp(nameof(SortNameAsc)); OnProp(nameof(SortNameDesc));
            OnProp(nameof(SortDateDesc)); OnProp(nameof(SortDateAsc));
        }

        private void RaiseTypeFlags()
        {
            OnProp(nameof(TypeAll)); OnProp(nameof(TypeSln)); OnProp(nameof(TypeCsproj));
        }

        // ── Ripgrep content search ────────────────────────────────────────

        private void CancelSearch()
        {
            try { _searchCts?.Cancel(); }
            catch { }
            _log("[ProjectHelper] ⏸ Search cancelled by user.");
        }

        private async Task SearchContentAsync()
        {
            if (string.IsNullOrWhiteSpace(ContentQuery)) return;

            var settings = _getSettings();
            var rgPath   = settings.RipgrepPath?.Trim() ?? string.Empty;
            var root     = settings.DirectoryRootDhHosCodePath?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(rgPath) || !File.Exists(rgPath))
            {
                SearchStatus = "❌ ripgrep không tìm thấy. Cấu hình RipgrepPath trong Settings (JSON).";
                _log("[ProjectHelper] ❌ RipgrepPath không được cấu hình hoặc file không tồn tại: " + rgPath);
                return;
            }

            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                SearchStatus = "❌ DirectoryRootDhHosCodePath không hợp lệ hoặc không tồn tại.";
                _log("[ProjectHelper] ❌ DirectoryRootDhHosCodePath: " + root);
                return;
            }

            try { _searchCts?.Cancel(); _searchCts?.Dispose(); }
            catch { }
            _searchCts = new CancellationTokenSource();
            var ct = _searchCts.Token;

            IsSearching  = true;
            SearchStatus = string.Format("⏳ Đang tìm: \"{0}\"...", ContentQuery);
            SearchResults.Clear();
            (SearchContentCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (CancelSearchCommand  as RelayCommand)?.RaiseCanExecuteChanged();

            var sw = Stopwatch.StartNew();

            // v3.7: Log the exact command being run for debugging
            var safePattern = ContentQuery.Replace("\"", "\\\"");
            var safeRoot    = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var rgArgs      = string.Format("--json --line-number --max-count 500 -- \"{0}\" \"{1}\"",
                                safePattern, safeRoot);

            _log(string.Format("[ProjectHelper] 🔍 Bắt đầu tìm kiếm nội dung: \"{0}\"", ContentQuery));
            _log(string.Format("[ProjectHelper] 🔍 ripgrep path: {0}", rgPath));
            _log(string.Format("[ProjectHelper] 🔍 Thư mục tìm: {0}", root));
            _log(string.Format("[ProjectHelper] 🔍 Lệnh: \"{0}\" {1}", rgPath, rgArgs));

            try
            {
                var results = await Task.Run(
                    () => RunRipgrep(rgPath, ContentQuery, root, ct, _log), ct);

                sw.Stop();
                _log(string.Format("[ProjectHelper] ✅ Tìm kiếm hoàn thành trong {0}ms — {1} kết quả",
                    sw.ElapsedMilliseconds, results.Count));

                if (!ct.IsCancellationRequested)
                {
                    foreach (var r in results) SearchResults.Add(r);
                    SearchStatus = results.Count == 0
                        ? string.Format("🔍 Không tìm thấy kết quả cho \"{0}\" ({1}ms)",
                            ContentQuery, sw.ElapsedMilliseconds)
                        : string.Format("✅ {0} kết quả cho \"{1}\" ({2}ms)",
                            results.Count, ContentQuery, sw.ElapsedMilliseconds);
                }
                else
                {
                    SearchStatus = "⏸ Đã hủy tìm kiếm.";
                }
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                SearchStatus = "⏸ Đã hủy tìm kiếm.";
                _log("[ProjectHelper] ⏸ Search cancelled.");
            }
            catch (Exception ex)
            {
                sw.Stop();
                SearchStatus = "❌ Lỗi: " + ex.Message;
                _log("[ProjectHelper] ❌ Search error: " + ex.Message);
            }
            finally
            {
                IsSearching = false;
                (SearchContentCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (CancelSearchCommand  as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Runs ripgrep with --json output.
        /// v3.7: Logs the actual command, execution time, stderr output for debugging.
        /// Tries direct execution first; falls back to cmd.exe if Win32Exception occurs
        /// (e.g. 64-bit rg.exe launched from 32-bit VS2017 devenv.exe).
        /// </summary>
        private static List<RipgrepSearchResult> RunRipgrep(
            string rgPath, string pattern, string root, CancellationToken ct, Action<string> log)
        {
            var safePattern = pattern.Replace("\"", "\\\"");
            var safeRoot    = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Use -- to separate flags from pattern (handles patterns starting with -)
            var rgArgs = string.Format(
                "--json --line-number --max-count 500 -- \"{0}\" \"{1}\"",
                safePattern, safeRoot);

            // 1. Try direct execution
            try
            {
                log?.Invoke(string.Format("[ProjectHelper] 🔍 Thử chạy trực tiếp: {0} {1}", rgPath, rgArgs));
                var psi = new ProcessStartInfo(rgPath, rgArgs)
                {
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding  = Encoding.UTF8
                };
                return ParseRipgrepOutput(psi, root, ct, log);
            }
            catch (System.ComponentModel.Win32Exception ex)
                when (ex.NativeErrorCode == 193 || ex.NativeErrorCode == 216 || ex.NativeErrorCode == 14001)
            {
                log?.Invoke(string.Format(
                    "[ProjectHelper] ⚠ Direct launch failed (Win32 error {0} — likely 64-bit rg.exe on 32-bit host). Trying cmd.exe fallback...",
                    ex.NativeErrorCode));
            }

            // 2. Fallback: launch via cmd.exe (64-bit on x64 Windows)
            // cmd /c ""path\rg.exe" args" — outer quotes required by cmd for paths with spaces
            var cmdArgs = string.Format("/c \"{0}\" {1}", rgPath, rgArgs);
            log?.Invoke(string.Format("[ProjectHelper] 🔍 cmd.exe fallback: cmd.exe {0}", cmdArgs));

            var cmdPsi = new ProcessStartInfo("cmd.exe", cmdArgs)
            {
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding  = Encoding.UTF8
            };
            return ParseRipgrepOutput(cmdPsi, root, ct, log);
        }

        /// <summary>
        /// Starts the process, reads ripgrep --json output line by line, and returns results.
        /// v3.7: Logs stderr so users can see ripgrep error messages in Output Window.
        /// Also correctly computes RelativePath relative to the scan root.
        /// </summary>
        private static List<RipgrepSearchResult> ParseRipgrepOutput(
            ProcessStartInfo psi, string root, CancellationToken ct, Action<string> log)
        {
            var results    = new List<RipgrepSearchResult>();
            var stderrBuf  = new StringBuilder();

            using (var proc = Process.Start(psi))
            {
                if (proc == null)
                {
                    log?.Invoke("[ProjectHelper] ❌ Không thể khởi động ripgrep process.");
                    return results;
                }

                ct.Register(() =>
                {
                    try { if (!proc.HasExited) proc.Kill(); }
                    catch { }
                });

                // Read stderr on separate thread to avoid deadlock
                var stderrTask = Task.Run(() =>
                {
                    string errLine;
                    while ((errLine = proc.StandardError.ReadLine()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(errLine))
                            stderrBuf.AppendLine(errLine);
                    }
                });

                string line;
                while ((line = proc.StandardOutput.ReadLine()) != null)
                {
                    if (ct.IsCancellationRequested) break;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        var obj  = JObject.Parse(line);
                        var type = obj["type"]?.ToString();
                        if (type != "match") continue;

                        var data = obj["data"] as JObject;
                        if (data == null) continue;

                        var filePath = data["path"]?["text"]?.ToString() ?? string.Empty;
                        var lineNum  = data["line_number"]?.ToObject<int>() ?? 0;
                        var lineText = data["lines"]?["text"]?.ToString() ?? string.Empty;

                        lineText = lineText.TrimEnd('\r', '\n');
                        if (lineText.Length > 200) lineText = lineText.Substring(0, 197) + "...";

                        // Compute relative path properly
                        string relPath = filePath;
                        if (!string.IsNullOrEmpty(root) &&
                            filePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        {
                            relPath = filePath.Substring(root.Length)
                                .TrimStart(Path.DirectorySeparatorChar,
                                           Path.AltDirectorySeparatorChar);
                        }

                        results.Add(new RipgrepSearchResult
                        {
                            FilePath     = filePath,
                            RelativePath = relPath,
                            LineNumber   = lineNum,
                            LineText     = lineText
                        });
                    }
                    catch { /* skip malformed JSON line */ }
                }

                proc.WaitForExit(10000);
                stderrTask.Wait(2000);

                // v3.7: Log stderr so users can debug ripgrep issues from Output Window
                var stderr = stderrBuf.ToString().Trim();
                if (!string.IsNullOrEmpty(stderr))
                {
                    log?.Invoke("[ProjectHelper] ⚠ ripgrep stderr output:");
                    foreach (var errLine in stderr.Split('\n'))
                    {
                        var trimmed = errLine.Trim('\r', '\n', ' ');
                        if (!string.IsNullOrEmpty(trimmed))
                            log?.Invoke("    " + trimmed);
                    }
                }

                log?.Invoke(string.Format("[ProjectHelper] 🔍 ripgrep exit code: {0} — {1} matches parsed",
                    proc.ExitCode, results.Count));
            }

            return results;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnProp(string n) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
