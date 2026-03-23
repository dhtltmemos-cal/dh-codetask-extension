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
    public enum ProjectSortMode { NameAsc, NameDesc, DateDesc, DateAsc }
    public enum ProjectFileType  { All, SlnOnly, CsprojOnly }

    public sealed class ProjectHelperViewModel : INotifyPropertyChanged
    {
        private readonly SolutionFileService _service;
        private readonly Func<AppSettings>   _getSettings;
        private readonly Action<string>      _log;

        private List<SolutionFileEntry> _allFiles = new List<SolutionFileEntry>();

        // ── v3.13: Content Search defaults ───────────────────────────────
        /// <summary>Default file extensions to include in content search.</summary>
        private static readonly string[] DefaultSearchExtensions =
        {
            // C# / .NET
            "*.cs", "*.csx", "*.csproj", "*.sln", "*.props", "*.targets",
            // Web / config
            "*.xml", "*.config", "*.json", "*.yaml", "*.yml", "*.toml",
            "*.htm", "*.html", "*.css", "*.js", "*.ts", "*.jsx", "*.tsx",
            // Docs
            "*.md", "*.txt", "*.rst",
            // SQL / scripts
            "*.sql", "*.sh", "*.bat", "*.cmd", "*.ps1",
            // XAML / WPF
            "*.xaml", "*.axaml",
        };

        /// <summary>Default directories to exclude from content search.</summary>
        private static readonly string[] DefaultSearchExcludeDirs =
        {
            "bin", "obj", ".git", "node_modules", "packages", ".vs", ".idea",
            "TestResults", "Artifacts", "dist", "build", ".nuget",
            "wwwroot/lib", "wwwroot\\lib", ".gradle", "__pycache__",
            "vendor", "coverage", ".nyc_output",
        };


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

        // ── Ripgrep content search ────────────────────────────────────────
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
        public RelayCommand<SolutionFileEntry>   OpenInVsCommand       { get; }
        public RelayCommand<SolutionFileEntry>   CopyPathCommand       { get; }
        public RelayCommand<SolutionFileEntry>   OpenFolderCommand     { get; }
        public RelayCommand<RipgrepSearchResult> OpenSearchResultCommand { get; }

        // ── Injected actions ──────────────────────────────────────────────
        public Action<string> OpenFileAction   { get; set; }
        public Action<string> CopyToClipboard  { get; set; }
        public Action<string> OpenFolderAction { get; set; }

        public ProjectHelperViewModel(SolutionFileService service,
            Func<AppSettings> getSettings, Action<string> log)
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
                _log(string.Format("[ProjectHelper] \U0001F4C2 Open: {0}", entry.FullPath));
                OpenFileAction?.Invoke(entry.FullPath);
            });

            CopyPathCommand = new RelayCommand<SolutionFileEntry>(entry =>
            {
                if (entry == null) return;
                _log(string.Format("[ProjectHelper] \U0001F4CB Copy path: {0}", entry.FullPath));
                CopyToClipboard?.Invoke(entry.FullPath);
            });

            OpenFolderCommand = new RelayCommand<SolutionFileEntry>(entry =>
            {
                if (entry == null) return;
                var dir = Path.GetDirectoryName(entry.FullPath) ?? string.Empty;
                OpenFolderAction?.Invoke(dir);
            });

            OpenSearchResultCommand = new RelayCommand<RipgrepSearchResult>(r =>
            {
                if (r == null || string.IsNullOrEmpty(r.FilePath)) return;
                OpenFileAction?.Invoke(r.FilePath);
            });
        }

        // ── Refresh (v3.11: progress + cancellation) ──────────────────────

        private void RefreshFireAndForget() { var _ = RefreshAsync(); }

        // v3.11: separate CTS for file scan (not to be confused with _searchCts)
        private CancellationTokenSource _scanCts;

        public async Task RefreshAsync(bool forceRescan = false)
        {
            try { _scanCts?.Cancel(); _scanCts?.Dispose(); }
            catch { }
            _scanCts = new CancellationTokenSource();
            var ct = _scanCts.Token;

            IsLoading     = true;
            var mode      = _service.GetScanModeDisplay();
            StatusMessage = string.Format("\u23F3 Dang quet ({0})...", mode);
            _log(string.Format("[ProjectHelper] Refreshing file list via {0}...", mode));
            try
            {
                if (forceRescan || _service.IsCacheExpired())
                {
                    await _service.RefreshAsync(
                        onProgress: msg =>
                        {
                            StatusMessage = msg;
                            _log("[ProjectHelper] " + msg);
                        },
                        ct: ct);
                }

                ct.ThrowIfCancellationRequested();

                _allFiles = await _service.GetFilesAsync();
                var age = _service.GetCacheAgeDisplay();
                _log(string.Format("[ProjectHelper] Found {0} file(s) via {1}. Cache: {2}",
                    _allFiles.Count, mode, age ?? "fresh"));
                ApplyFilter();
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Dung quet.";
                _log("[ProjectHelper] Scan cancelled.");
            }
            catch (Exception ex)
            {
                StatusMessage = "Loi: " + ex.Message;
                _log("[ProjectHelper] ERROR: " + ex.Message);
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
            var ageInfo = age != null ? string.Format(" (cache: {0} truoc)", age) : string.Empty;
            var mode = _service.GetScanModeDisplay();
            StatusMessage = result.Count == 0
                ? (_allFiles.Count == 0
                    ? "Chua quet. Cau hinh DirectoryRootDhHosCodePath va nhan 🔄."
                    : "Khong tim thay file nao khop.")
                : string.Format("{0} file [{1}]{2} — {3:HH:mm:ss}", result.Count, mode, ageInfo, DateTime.Now);
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

        // ── Ripgrep content search (unchanged from v3.7) ──────────────────
        private void CancelSearch()
        {
            try { _searchCts?.Cancel(); }
            catch { }
            _log("[ProjectHelper] Search cancelled by user.");
        }

        private async Task SearchContentAsync()
        {
            if (string.IsNullOrWhiteSpace(ContentQuery)) return;

            var settings = _getSettings();
            var rgPath   = settings.RipgrepPath?.Trim() ?? string.Empty;
            var root     = settings.DirectoryRootDhHosCodePath?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(rgPath) || !File.Exists(rgPath))
            {
                SearchStatus = "ripgrep chua duoc cau hinh. Them RipgrepPath vao Settings.";
                return;
            }
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                SearchStatus = "DirectoryRootDhHosCodePath khong hop le.";
                return;
            }

            try { _searchCts?.Cancel(); _searchCts?.Dispose(); }
            catch { }

            // v3.13: hard timeout — prevent hanging on large repos
            int timeoutSec = settings.SearchTimeoutSeconds > 0 ? settings.SearchTimeoutSeconds : 45;
            _searchCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));
            var ct = _searchCts.Token;

            IsSearching  = true;
            SearchResults.Clear();
            (SearchContentCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (CancelSearchCommand  as RelayCommand)?.RaiseCanExecuteChanged();

            // Build rg args with extension filter + exclude dirs
            int maxResults = settings.SearchMaxResults > 0 ? settings.SearchMaxResults : 200;
            string rgArgs  = BuildRipgrepArgs(settings, ContentQuery, root, maxResults);

            SearchStatus = string.Format("Tim: {0} (timeout {1}s, max {2} ket qua)...",
                ContentQuery, timeoutSec, maxResults);
            _log(string.Format("[ProjectHelper] rg {0}", rgArgs));

            var sw = Stopwatch.StartNew();
            try
            {
                var results = await Task.Run(
                    () => RunRipgrep(rgPath, rgArgs, root, ct, _log), ct);
                sw.Stop();
                _log(string.Format("[ProjectHelper] Search done: {0}ms, {1} results",
                    sw.ElapsedMilliseconds, results.Count));

                if (!ct.IsCancellationRequested)
                {
                    foreach (var r in results) SearchResults.Add(r);
                    if (results.Count == 0)
                        SearchStatus = string.Format(
                            "Khong co ket qua cho [{0}] ({1}ms)", ContentQuery, sw.ElapsedMilliseconds);
                    else if (results.Count >= maxResults)
                        SearchStatus = string.Format(
                            "{0}+ ket qua (hit limit {0}) cho [{1}] ({2}ms) -- thu hep tu khoa",
                            maxResults, ContentQuery, sw.ElapsedMilliseconds);
                    else
                        SearchStatus = string.Format(
                            "{0} ket qua cho [{1}] ({2}ms)", results.Count, ContentQuery, sw.ElapsedMilliseconds);
                }
                else
                {
                    SearchStatus = string.Format(
                        "Da huy / timeout sau {0}s -- thu hep tu khoa hoac tang SearchTimeoutSeconds",
                        timeoutSec);
                }
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                SearchStatus = string.Format(
                    "Timeout sau {0}s -- tang SearchTimeoutSeconds trong Settings neu can.",
                    timeoutSec);
            }
            catch (Exception ex) { sw.Stop(); SearchStatus = "Loi: " + ex.Message; }
            finally
            {
                IsSearching = false;
                (SearchContentCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (CancelSearchCommand  as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// v3.13: Build ripgrep arguments with:
        ///   - Extension whitelist (only search relevant file types)
        ///   - Directory exclusions (skip bin/obj/.git/node_modules etc.)
        ///   - max-count limit
        ///   - --no-follow (skip symlinks to avoid loops)
        /// </summary>
        private static string BuildRipgrepArgs(
            AppSettings settings, string pattern, string root, int maxResults)
        {
            var sb = new System.Text.StringBuilder();

            sb.Append("--json ");
            sb.Append("--line-number ");
            sb.AppendFormat("--max-count {0} ", maxResults);
            sb.Append("--no-follow ");     // skip symlinks
            sb.Append("--no-binary ");     // skip binary files entirely

            // Extension whitelist
            var exts = (settings.SearchFileExtensions != null && settings.SearchFileExtensions.Count > 0)
                ? settings.SearchFileExtensions.ToArray()
                : DefaultSearchExtensions;
            foreach (var ext in exts)
            {
                var e = ext.Trim();
                if (!string.IsNullOrEmpty(e))
                    sb.AppendFormat("--glob \"{0}\" ", e.StartsWith("*") ? e : "*." + e);
            }

            // Directory exclusions
            var excludes = new System.Collections.Generic.List<string>(DefaultSearchExcludeDirs);
            if (settings.SearchExcludeDirs != null)
                excludes.AddRange(settings.SearchExcludeDirs);
            foreach (var dir in excludes)
            {
                var d = dir.Trim().Trim('/', '\\');
                if (!string.IsNullOrEmpty(d))
                    sb.AppendFormat("--glob \"!{0}\" ", d);
            }

            // Pattern and root (-- separator prevents pattern starting with - being parsed as flag)
            sb.AppendFormat("-- \"{0}\" \"{1}\"",
                pattern,
                root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// v3.13: Accepts pre-built rgArgs from BuildRipgrepArgs().
        /// Tries direct execution first; falls back to cmd.exe for 64-bit rg on 32-bit VS2017.
        /// </summary>
        private static List<RipgrepSearchResult> RunRipgrep(
            string rgPath, string rgArgs, string root, CancellationToken ct, Action<string> log)
        {
            // Extract root from rgArgs not needed — passed separately
            ProcessStartInfo psi;
            bool usedCmdFallback = false;
            try
            {
                psi = new ProcessStartInfo(rgPath, rgArgs)
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
                // 64-bit rg.exe on 32-bit VS2017 devenv.exe
                usedCmdFallback = true;
                psi = new ProcessStartInfo("cmd.exe",
                    string.Format("/c \"{0}\" {1}", rgPath, rgArgs))
                {
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding  = Encoding.UTF8
                };
            }
            if (usedCmdFallback)
                log?.Invoke("[ProjectHelper] Using cmd.exe fallback for 64-bit rg.exe");

            return ParseRipgrepOutput(psi, root, ct, log);
        }

        private static List<RipgrepSearchResult> ParseRipgrepOutput(
            ProcessStartInfo psi, string root, CancellationToken ct, Action<string> log)
        {
            var results   = new List<RipgrepSearchResult>();
            var stderrBuf = new StringBuilder();

            using (var proc = Process.Start(psi))
            {
                if (proc == null) return results;
                ct.Register(() => { try { if (!proc.HasExited) proc.Kill(); } catch { } });

                // Use a plain Thread (not Task) for stderr to avoid VSTHRD002.
                // We are inside Task.Run so there is no JoinableTask sync context.
                var stderrThread = new System.Threading.Thread(() =>
                {
                    try { stderrBuf.Append(proc.StandardError.ReadToEnd()); }
                    catch { }
                }) { IsBackground = true };
                stderrThread.Start();

                string line;
                while ((line = proc.StandardOutput.ReadLine()) != null)
                {
                    if (ct.IsCancellationRequested) break;
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var obj  = JObject.Parse(line);
                        if (obj["type"]?.ToString() != "match") continue;
                        var data = obj["data"] as JObject;
                        if (data == null) continue;
                        var filePath = data["path"]?["text"]?.ToString() ?? string.Empty;
                        var lineNum  = data["line_number"]?.ToObject<int>() ?? 0;
                        var lineText = (data["lines"]?["text"]?.ToString() ?? string.Empty)
                            .TrimEnd('\r', '\n');
                        if (lineText.Length > 200) lineText = lineText.Substring(0, 197) + "...";
                        string relPath = filePath;
                        if (!string.IsNullOrEmpty(root) &&
                            filePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                            relPath = filePath.Substring(root.Length)
                                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        results.Add(new RipgrepSearchResult
                        {
                            FilePath = filePath, RelativePath = relPath,
                            LineNumber = lineNum, LineText = lineText
                        });
                    }
                    catch { }
                }

                proc.WaitForExit(10000);
                stderrThread.Join(2000); // wait max 2s for stderr to finish

                var stderr = stderrBuf.ToString().Trim();
                if (!string.IsNullOrEmpty(stderr))
                {
                    log?.Invoke("[ProjectHelper] rg stderr:");
                    foreach (var l in stderr.Split('\n'))
                    {
                        var t = l.Trim('\r', '\n', ' ');
                        if (!string.IsNullOrEmpty(t)) log?.Invoke("  " + t);
                    }
                }
                log?.Invoke(string.Format("[ProjectHelper] rg exit: {0}, {1} matches", proc.ExitCode, results.Count));
            }
            return results;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnProp(string n) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}