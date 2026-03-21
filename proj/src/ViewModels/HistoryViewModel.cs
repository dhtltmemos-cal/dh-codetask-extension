using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DhCodetaskExtension.Core.Interfaces;
using DhCodetaskExtension.Core.Models;
using DhCodetaskExtension.Core.Services;

namespace DhCodetaskExtension.ViewModels
{
    public enum HistoryViewMode { Today, ThisWeek, ThisMonth, Custom }

    public class CompletionReportSummary
    {
        public string   ReportId         { get; set; }
        public string   TaskId           { get; set; }
        public string   TaskTitle        { get; set; }
        public DateTime StartedAt        { get; set; }
        public DateTime CompletedAt      { get; set; }
        public TimeSpan TotalElapsed     { get; set; }
        public string   ElapsedDisplay   { get; set; }
        public int      TodoDone         { get; set; }
        public int      TodoTotal        { get; set; }
        public bool     WasPushed        { get; set; }
        public string   JsonFilePath     { get; set; }
        public string   MarkdownFilePath { get; set; }
        public string   GroupKey         => CompletedAt.ToLocalTime().ToString("yyyy-MM-dd");
        public string   GroupLabel       => CompletedAt.ToLocalTime().ToString("dddd, dd/MM/yyyy");
        public CompletionReport FullReport { get; set; }
    }

    public class HistorySummary
    {
        public int      TotalTasks       { get; set; }
        public TimeSpan TotalElapsed     { get; set; }
        public TimeSpan AvgElapsed       { get; set; }
        public int      TotalTodos       { get; set; }
        public double   TodoDoneRate     { get; set; }
        public string   TotalElapsedDisplay => FormatSpan(TotalElapsed);
        public string   AvgElapsedDisplay   => FormatSpan(AvgElapsed);
        public string   TodoDoneRateDisplay => string.Format("{0:P0}", TodoDoneRate);
        private static string FormatSpan(TimeSpan ts) => string.Format("{0}h {1}m", (int)ts.TotalHours, ts.Minutes);
    }

    public sealed class HistoryViewModel : INotifyPropertyChanged
    {
        private readonly IHistoryRepository _repo;
        private readonly Action<string>     _log;
        private const int PageSize = 20;

        private HistoryViewMode _viewMode = HistoryViewMode.ThisWeek;
        private DateTime        _customFrom = DateTime.Today.AddDays(-7);
        private DateTime        _customTo   = DateTime.Today;
        private string          _searchKeyword = string.Empty;
        private bool            _isLoading;
        private int             _currentPage = 1;
        private List<CompletionReportSummary> _allItems = new List<CompletionReportSummary>();

        public HistoryViewMode ViewMode
        {
            get => _viewMode;
            set { _viewMode = value; OnProp(nameof(ViewMode)); RefreshFireAndForget(); }
        }
        public DateTime CustomFrom  { get => _customFrom; set { _customFrom = value; OnProp(nameof(CustomFrom)); } }
        public DateTime CustomTo    { get => _customTo;   set { _customTo   = value; OnProp(nameof(CustomTo)); } }
        public string SearchKeyword { get => _searchKeyword; set { _searchKeyword = value; OnProp(nameof(SearchKeyword)); ApplyFilter(); } }
        public bool IsLoading       { get => _isLoading;     set { _isLoading     = value; OnProp(nameof(IsLoading)); } }

        public ObservableCollection<CompletionReportSummary> Items { get; } = new ObservableCollection<CompletionReportSummary>();

        private HistorySummary _summary = new HistorySummary();
        public HistorySummary Summary { get => _summary; private set { _summary = value; OnProp(nameof(Summary)); } }

        public int    CurrentPage { get => _currentPage; set { _currentPage = value; OnProp(nameof(CurrentPage)); } }
        public int    TotalPages  => Math.Max(1, (int)Math.Ceiling(_allItems.Count / (double)PageSize));
        public string PageLabel   => string.Format("Trang {0}/{1}", CurrentPage, TotalPages);
        public bool   CanPrev     => CurrentPage > 1;
        public bool   CanNext     => CurrentPage < TotalPages;

        // ── Commands ──────────────────────────────────────────────────────
        public ICommand RefreshCommand      { get; }
        public ICommand PrevPageCommand     { get; }
        public ICommand NextPageCommand     { get; }
        public ICommand CustomFilterCommand { get; }
        public RelayCommand<CompletionReportSummary> DeleteCommand  { get; }
        public RelayCommand<CompletionReportSummary> ResumeCommand  { get; }
        public RelayCommand<CompletionReportSummary> OpenUrlCommand { get; }

        // ── Action delegates (wired by Package) ───────────────────────────
        public Action<CompletionReportSummary> OpenDetailAction { get; set; }
        public Action<string>                  OpenFileAction   { get; set; }

        /// <summary>
        /// Called when user clicks "Resume" on a history row.
        /// Package wires this to load the report into TrackerViewModel.
        /// </summary>
        public Action<CompletionReport> ResumeFromHistoryAction { get; set; }

        /// <summary>
        /// Called to open a URL in the default browser.
        /// Package wires this to Process.Start.
        /// </summary>
        public Action<string> OpenUrlAction { get; set; }

        public HistoryViewModel(IHistoryRepository repo, Action<string> log)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _log  = log  ?? (_ => { });

            RefreshCommand      = new RelayCommand(RefreshFireAndForget);
            PrevPageCommand     = new RelayCommand(PrevPage, () => CanPrev);
            NextPageCommand     = new RelayCommand(NextPage, () => CanNext);
            CustomFilterCommand = new RelayCommand(() => { _viewMode = HistoryViewMode.Custom; RefreshFireAndForget(); });
            DeleteCommand       = new RelayCommand<CompletionReportSummary>(s => DeleteFireAndForget(s));

            ResumeCommand = new RelayCommand<CompletionReportSummary>(s =>
            {
                if (s?.FullReport != null)
                {
                    _log(string.Format("[History] ▶ Resume requested: #{0} {1}", s.TaskId, s.TaskTitle));
                    ResumeFromHistoryAction?.Invoke(s.FullReport);
                }
            });

            OpenUrlCommand = new RelayCommand<CompletionReportSummary>(s =>
            {
                var url = s?.FullReport?.TaskUrl ?? string.Empty;
                if (string.IsNullOrEmpty(url)) return;
                _log("[History] 🔗 Open URL: " + url);
                OpenUrlAction?.Invoke(url);
            });
        }

        // ── Public async entry ────────────────────────────────────────────
        public async Task RefreshAsync()
        {
            IsLoading = true;
            try
            {
                IEnumerable<CompletionReport> reports;
                switch (ViewMode)
                {
                    case HistoryViewMode.Today:     reports = await _repo.GetTodayAsync();     break;
                    case HistoryViewMode.ThisWeek:  reports = await _repo.GetThisWeekAsync();  break;
                    case HistoryViewMode.ThisMonth: reports = await _repo.GetThisMonthAsync(); break;
                    default:
                        reports = await _repo.GetByDateRangeAsync(CustomFrom, CustomTo.AddDays(1).AddTicks(-1));
                        break;
                }
                _allItems = reports.Select(ToSummary).ToList();
                CurrentPage = 1;
                ApplyFilter();
                BuildSummary(reports);
            }
            catch (Exception ex) { _log("[History] " + ex.Message); }
            finally { IsLoading = false; }
        }

        public async Task DeleteAsync(CompletionReportSummary s)
        {
            if (s == null) return;
            try
            {
                await _repo.DeleteAsync(s.ReportId);
                _allItems.RemoveAll(x => x.ReportId == s.ReportId);
                ApplyFilter();
            }
            catch (Exception ex) { _log("[History] Delete error: " + ex.Message); }
        }

        // ── Private helpers ───────────────────────────────────────────────
        private void RefreshFireAndForget()       { var _ = RefreshAsync(); }
        private void DeleteFireAndForget(CompletionReportSummary s) { var _ = DeleteAsync(s); }

        private void ApplyFilter()
        {
            var filtered = _allItems;
            if (!string.IsNullOrWhiteSpace(SearchKeyword))
            {
                var kw = SearchKeyword.ToLower();
                filtered = filtered.Where(r =>
                    (r.TaskTitle ?? string.Empty).ToLower().Contains(kw) ||
                    (r.TaskId    ?? string.Empty).Contains(kw)).ToList();
            }
            var page = filtered.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
            Items.Clear();
            foreach (var i in page) Items.Add(i);
            OnProp(nameof(TotalPages)); OnProp(nameof(PageLabel));
            OnProp(nameof(CanPrev));    OnProp(nameof(CanNext));
            (PrevPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (NextPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void BuildSummary(IEnumerable<CompletionReport> reports)
        {
            var list  = reports.ToList();
            int total = list.Count;
            var totalEl = TimeSpan.FromSeconds(list.Sum(r => r.TotalElapsed.TotalSeconds));
            int todos = list.Sum(r => r.TodoTotal);
            int done  = list.Sum(r => r.TodoDone);
            Summary = new HistorySummary
            {
                TotalTasks   = total,
                TotalElapsed = totalEl,
                AvgElapsed   = total > 0 ? TimeSpan.FromSeconds(totalEl.TotalSeconds / total) : TimeSpan.Zero,
                TotalTodos   = todos,
                TodoDoneRate = todos > 0 ? (double)done / todos : 0
            };
        }

        private void PrevPage() { if (CanPrev) { CurrentPage--; ApplyFilter(); } }
        private void NextPage() { if (CanNext) { CurrentPage++; ApplyFilter(); } }

        private static CompletionReportSummary ToSummary(CompletionReport r) => new CompletionReportSummary
        {
            ReportId         = r.ReportId,
            TaskId           = r.TaskId,
            TaskTitle        = r.TaskTitle,
            StartedAt        = r.StartedAt.ToLocalTime(),
            CompletedAt      = r.CompletedAt.ToLocalTime(),
            TotalElapsed     = r.TotalElapsed,
            ElapsedDisplay   = FormatSpan(r.TotalElapsed),
            TodoDone         = r.TodoDone,
            TodoTotal        = r.TodoTotal,
            WasPushed        = r.WasPushed,
            JsonFilePath     = r.JsonFilePath,
            MarkdownFilePath = r.MarkdownFilePath,
            FullReport       = r
        };

        private static string FormatSpan(TimeSpan ts) =>
            ts.TotalHours >= 1
                ? string.Format("{0}h {1}m", (int)ts.TotalHours, ts.Minutes)
                : string.Format("{0}m {1}s", ts.Minutes, ts.Seconds);

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnProp(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
