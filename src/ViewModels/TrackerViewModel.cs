using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using DhCodetaskExtension.Core.Events;
using DhCodetaskExtension.Core.Interfaces;
using DhCodetaskExtension.Core.Models;
using DhCodetaskExtension.Core.Services;
using DhCodetaskExtension.Providers.ReportProviders;

namespace DhCodetaskExtension.ViewModels
{
    public sealed class TrackerViewModel : INotifyPropertyChanged
    {
        private readonly IEventBus            _eventBus;
        private readonly IStorageService      _storage;
        private readonly IGitService          _git;
        private readonly IReportGenerator     _reportGenerator;
        private readonly TimeTrackingService  _timer;
        private readonly Func<AppSettings>    _settings;
        private readonly Action<string>       _log;
        private DispatcherTimer               _autoSave;
        private DispatcherTimer               _uiTimer;

        // ── Observable properties ─────────────────────────────────────────
        private string _taskUrl         = string.Empty;
        private string _statusMessage   = "Nhập URL issue để bắt đầu.";
        private bool   _isFetching;
        private string _taskTitle        = string.Empty;
        private string _taskDescription  = string.Empty;
        private string _labelsDisplay    = string.Empty;
        private string _workNotes        = string.Empty;
        private string _businessLogic    = string.Empty;
        private string _commitMessage    = string.Empty;
        private string _timerDisplay     = "00:00:00";
        private string _timerState       = "Idle";
        private bool   _gitAvailable;
        private string _repoRoot         = string.Empty;
        private string _newTodoText      = string.Empty;
        private TaskItem _currentTask;

        public string TaskUrl         { get => _taskUrl;         set { _taskUrl         = value; OnProp(nameof(TaskUrl)); } }
        public string StatusMessage   { get => _statusMessage;   set { _statusMessage   = value; OnProp(nameof(StatusMessage)); } }
        public bool   IsFetching      { get => _isFetching;      set { _isFetching      = value; OnProp(nameof(IsFetching)); } }
        public string TaskTitle       { get => _taskTitle;       set { _taskTitle       = value; OnProp(nameof(TaskTitle)); } }
        public string TaskDescription { get => _taskDescription; set { _taskDescription = value; OnProp(nameof(TaskDescription)); } }
        public string LabelsDisplay   { get => _labelsDisplay;   set { _labelsDisplay   = value; OnProp(nameof(LabelsDisplay)); } }
        public string WorkNotes       { get => _workNotes;       set { _workNotes       = value; OnProp(nameof(WorkNotes)); } }
        public string BusinessLogic   { get => _businessLogic;   set { _businessLogic   = value; OnProp(nameof(BusinessLogic)); } }
        public string CommitMessage   { get => _commitMessage;   set { _commitMessage   = value; OnProp(nameof(CommitMessage)); } }
        public string TimerDisplay    { get => _timerDisplay;    set { _timerDisplay    = value; OnProp(nameof(TimerDisplay)); } }

        /// <summary>
        /// When TimerState changes, three things must happen:
        /// 1. UI commands for the task (Start/Pause/Resume) update their enabled state.
        /// 2. Every TodoItemViewModel refreshes its CanStart — which now depends on parent Running state.
        /// 3. HasTodoTemplates is re-evaluated if needed.
        /// </summary>
        public string TimerState
        {
            get => _timerState;
            set
            {
                _timerState = value;
                OnProp(nameof(TimerState));
                RaiseCommandsChanged();
                // v3.8: refresh all todo items so Start button enables/disables correctly
                RefreshTodoParentStateCommands();
            }
        }

        public bool   GitAvailable    { get => _gitAvailable;    set { _gitAvailable    = value; OnProp(nameof(GitAvailable)); } }

        public string NewTodoText
        {
            get => _newTodoText;
            set
            {
                _newTodoText = value;
                OnProp(nameof(NewTodoText));
                (AddTodoCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public ObservableCollection<TodoItemViewModel> Todos         { get; } = new ObservableCollection<TodoItemViewModel>();
        public ObservableCollection<string>            TodoTemplates { get; } = new ObservableCollection<string>();

        /// <summary>
        /// v3.8: BooleanToVisibilityConverter requires a bool, not an int (Count).
        /// Bind Expander.Visibility to this instead of TodoTemplates.Count.
        /// </summary>
        public bool HasTodoTemplates => TodoTemplates.Count > 0;

        public int    TodoTotal   => Todos.Count;
        public int    TodoDone    => Todos.Count(t => t.IsDone);
        public int    TodoRunning => Todos.Count(t => t.IsRunning);
        public string TodoTotalElapsed
        {
            get
            {
                var ts = TimeSpan.FromSeconds(Todos.Sum(t => t.Model.TotalElapsed.TotalSeconds));
                return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)ts.TotalHours, ts.Minutes, ts.Seconds);
            }
        }

        // ── Commands ──────────────────────────────────────────────────────
        public ICommand FetchCommand            { get; }
        public ICommand ClearCommand            { get; }
        public ICommand StartCommand            { get; }
        public ICommand PauseCommand            { get; }
        public ICommand ResumeCommand           { get; }
        public ICommand StopCommand             { get; }
        public ICommand AddTodoCommand          { get; }
        public ICommand RegenerateCommitCommand { get; }
        public ICommand PushAndCompleteCommand  { get; }
        public ICommand SaveAndPauseCommand     { get; }
        public ICommand AddTodoFromTemplateCommand { get; }

        // ── Injected actions ──────────────────────────────────────────────
        public Func<string, Task<TaskFetchResult>> FetchTaskFunc       { get; set; }
        public Func<Task<System.Collections.Generic.IEnumerable<CompletionReport>>> LoadAllHistoryFunc { get; set; }
        public Action OpenSettingsAction       { get; set; }
        public Action OpenHistoryAction        { get; set; }
        public Action OpenLogFileAction        { get; set; }
        public Action OpenConfigFileAction     { get; set; }
        public Action OpenProjectHelperAction  { get; set; }
        public Func<string> ShowPauseReasonDialog { get; set; }

        public TrackerViewModel(
            IEventBus eventBus, IStorageService storage, IGitService git,
            IReportGenerator reportGenerator, TimeTrackingService timer,
            Func<AppSettings> settings, Action<string> log)
        {
            _eventBus        = eventBus;
            _storage         = storage;
            _git             = git;
            _reportGenerator = reportGenerator;
            _timer           = timer;
            _settings        = settings;
            _log             = log ?? (_ => { });

            FetchCommand            = new RelayCommand(FetchFireAndForget,      () => !IsFetching);
            ClearCommand            = new RelayCommand(ClearTask);
            StartCommand            = new RelayCommand(StartTask,              () => TimerState == "Idle" || TimerState == "Paused");
            PauseCommand            = new RelayCommand(PauseTaskFireAndForget, () => TimerState == "Running");
            ResumeCommand           = new RelayCommand(ResumeTask,             () => TimerState == "Paused");
            StopCommand             = new RelayCommand(StopTask,               () => TimerState == "Running" || TimerState == "Paused");
            AddTodoCommand          = new RelayCommand(AddTodo,                () => !string.IsNullOrWhiteSpace(NewTodoText));
            RegenerateCommitCommand = new RelayCommand(RegenerateCommit);
            PushAndCompleteCommand  = new RelayCommand(PushAndCompleteFireAndForget);
            SaveAndPauseCommand     = new RelayCommand(SaveAndPauseFireAndForget);

            // v3.8: Template button fills input box; user reviews then clicks Add.
            // NewTodoText setter raises AddTodoCommand.CanExecuteChanged automatically.
            AddTodoFromTemplateCommand = new RelayCommand<string>(t =>
            {
                if (!string.IsNullOrWhiteSpace(t))
                {
                    NewTodoText = t;
                    _log("[Tracker] 📋 TODO template selected: " + t);
                }
            });

            GitAvailable = _git?.IsAvailable() ?? false;
            _log(string.Format("[Tracker] Git available: {0}", GitAvailable));
            StartAutoSave();
            StartUiTimer();
            RefreshTodoTemplates();
        }

        // ── Fire-and-forget wrappers ──────────────────────────────────────
        private void FetchFireAndForget()           { var _ = FetchAsync(); }
        private void PauseTaskFireAndForget()       { var _ = PauseTaskAsync(); }
        private void PushAndCompleteFireAndForget() { var _ = CompleteFlowAsync(push: true); }
        private void SaveAndPauseFireAndForget()    { var _ = CompleteFlowAsync(push: false); }
        private void AutoSaveFireAndForget()        { var _ = AutoSaveCurrentAsync(); }

        // ── Fetch ─────────────────────────────────────────────────────────
        private async Task FetchAsync()
        {
            if (string.IsNullOrWhiteSpace(TaskUrl)) return;
            IsFetching    = true;
            StatusMessage = "Đang fetch...";
            _log("[Tracker] Fetching: " + TaskUrl);
            try
            {
                var normalizedUrl = NormalizeIssueUrl(TaskUrl);
                if (LoadAllHistoryFunc != null)
                {
                    try
                    {
                        var history = await LoadAllHistoryFunc();
                        var match   = history?.FirstOrDefault(r =>
                            NormalizeIssueUrl(r.TaskUrl ?? string.Empty) == normalizedUrl);
                        if (match != null && !string.IsNullOrEmpty(match.TaskTitle))
                        {
                            _currentTask    = new TaskItem
                            {
                                Id          = match.TaskId          ?? string.Empty,
                                Title       = match.TaskTitle        ?? string.Empty,
                                Description = match.Description      ?? string.Empty,
                                Labels      = match.Labels           ?? new string[0],
                                Url         = match.TaskUrl          ?? TaskUrl
                            };
                            TaskTitle       = _currentTask.Title;
                            TaskDescription = _currentTask.Description;
                            TaskUrl         = _currentTask.Url;
                            LabelsDisplay   = string.Join(", ", _currentTask.Labels);
                            RegenerateCommit();
                            StatusMessage = string.Format("📚 Lấy từ lịch sử #{0}: {1}", _currentTask.Id, _currentTask.Title);
                            _log(string.Format("[Tracker] 📚 Loaded from history #{0}: {1}", _currentTask.Id, _currentTask.Title));
                            _eventBus.Publish(new TaskFetchedEvent { Task = _currentTask, Url = TaskUrl });
                            DetectRepoRoot();
                            IsFetching = false;
                            return;
                        }
                    }
                    catch { /* non-critical */ }
                }

                var result = await FetchTaskFunc(TaskUrl);
                if (result.Success)
                {
                    _currentTask    = result.Task;
                    TaskTitle       = result.Task.Title;
                    TaskDescription = result.Task.Description;
                    LabelsDisplay   = string.Join(", ", result.Task.Labels ?? new string[0]);
                    RegenerateCommit();
                    StatusMessage = string.Format("✅ Fetched #{0}: {1}", result.Task.Id, result.Task.Title);
                    _log(string.Format("[Tracker] ✅ Fetch OK — #{0}: {1}", result.Task.Id, result.Task.Title));
                    _eventBus.Publish(new TaskFetchedEvent { Task = result.Task, Url = TaskUrl });
                    DetectRepoRoot();
                }
                else
                {
                    StatusMessage = "❌ " + result.ErrorMessage;
                    _log("[Tracker] ❌ Fetch failed: " + result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "❌ " + ex.Message;
                _log("[Tracker] ❌ Fetch exception: " + ex.Message);
            }
            finally { IsFetching = false; }
        }

        private static string NormalizeIssueUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return string.Empty;
            var idx = url.IndexOf('#');
            return idx >= 0 ? url.Substring(0, idx).TrimEnd('/') : url.TrimEnd('/');
        }

        private void ClearTask()
        {
            _log("[Tracker] 🗑 Task cleared: " + (TaskTitle ?? "(no title)"));
            _currentTask = null;
            TaskUrl = TaskTitle = TaskDescription = LabelsDisplay = string.Empty;
            WorkNotes = BusinessLogic = CommitMessage = string.Empty;
            Todos.Clear();
            _timer.Reset();
            TimerState    = "Idle";
            StatusMessage = "Nhập URL issue để bắt đầu.";
            var _ = _storage.ClearCurrentTaskAsync();
        }

        // ── Timer ─────────────────────────────────────────────────────────
        private void StartTask()
        {
            if (TimerState == "Idle")         _timer.Start();
            else if (TimerState == "Paused")  _timer.Resume();
            TimerState = "Running";   // setter refreshes todo CanStart
            _log(string.Format("[Tracker] ▶ Task started/resumed: \"{0}\"", TaskTitle));
            _eventBus.Publish(new TaskStartedEvent { StartTime = DateTime.Now });
        }

        private async Task PauseTaskAsync()
        {
            string reason = string.Empty;
            if (ShowPauseReasonDialog != null)
            {
                reason = ShowPauseReasonDialog() ?? string.Empty;
                if (reason == null) return;
            }
            foreach (var t in Todos) t.AutoPause();
            _timer.Pause(reason);
            TimerState = "Paused";    // setter refreshes todo CanStart → Start buttons disable
            _log(string.Format("[Tracker] ⏸ Task paused — reason: {0} — elapsed: {1}",
                string.IsNullOrEmpty(reason) ? "(none)" : reason,
                FormatSpan(_timer.GetElapsed())));
            _eventBus.Publish(new TaskPausedEvent { Elapsed = _timer.GetElapsed() });
            await AutoSaveCurrentAsync();
        }

        private void ResumeTask()
        {
            _timer.Resume();
            TimerState = "Running";   // setter refreshes todo CanStart → Start buttons enable
            _log("[Tracker] ▶ Task resumed");
            _eventBus.Publish(new TaskResumedEvent());
        }

        private void StopTask()
        {
            foreach (var t in Todos) t.AutoPause();
            _timer.Stop();
            TimerState = "Stopped";   // setter refreshes todo CanStart → Start buttons disable
            _log(string.Format("[Tracker] ⏹ Task stopped — total: {0}", FormatSpan(_timer.GetElapsed())));
        }

        // ── TODO ──────────────────────────────────────────────────────────
        private void AddTodo()
        {
            if (string.IsNullOrWhiteSpace(NewTodoText)) return;
            var text = NewTodoText.Trim();
            var item = new TodoItem { Text = text };
            Todos.Add(CreateTodoVm(item));
            _log("[Tracker] ➕ TODO added: " + text);
            NewTodoText = string.Empty;
            RaiseTodoSummary();
        }

        private TodoItemViewModel CreateTodoVm(TodoItem item)
        {
            // v3.8: pass parent running predicate so CanStart is gated by task state
            var vm = new TodoItemViewModel(
                item, _eventBus, _currentTask?.Id ?? string.Empty,
                isParentRunning: () => TimerState == "Running");

            vm.PropertyChanged += (s, e) => RaiseTodoSummary();
            vm.DeleteRequested += d =>
            {
                _log("[Tracker] 🗑 TODO deleted: " + d.Model.Text);
                Todos.Remove(d);
                RaiseTodoSummary();
            };
            return vm;
        }

        /// <summary>
        /// v3.8: Called whenever TimerState changes so every todo item refreshes its
        /// Start button enabled state based on the new parent task state.
        /// </summary>
        private void RefreshTodoParentStateCommands()
        {
            foreach (var vm in Todos)
                vm.RefreshParentStateCommands();
        }

        private void RaiseTodoSummary()
        {
            OnProp(nameof(TodoTotal)); OnProp(nameof(TodoDone));
            OnProp(nameof(TodoRunning)); OnProp(nameof(TodoTotalElapsed));
        }

        // ── Commit ────────────────────────────────────────────────────────
        private void RegenerateCommit()
        {
            if (_currentTask != null)
            {
                CommitMessage = CommitMessageGenerator.Generate(_currentTask, BusinessLogic);
                _log("[Tracker] 🔀 Commit message regenerated");
            }
        }

        // ── Complete flow ─────────────────────────────────────────────────
        private async Task CompleteFlowAsync(bool push)
        {
            _log(string.Format("[Tracker] {0} flow started for \"{1}\"",
                push ? "🚀 Push & Complete" : "⏸ Save & Pause", TaskTitle));

            foreach (var t in Todos) t.AutoPause();
            var sessions    = _timer.Stop();
            TimerState      = "Stopped";
            var completedAt = DateTime.Now;
            var s           = _settings();
            string branch   = "unknown";
            string hash     = string.Empty;
            bool   pushed   = false;

            if (push && GitAvailable && !string.IsNullOrEmpty(_repoRoot))
            {
                StatusMessage = "⏳ Đang commit & push...";
                _log("[Tracker] Running git commit + push in: " + _repoRoot);
                var gitResult = await _git.PushAndCompleteAsync(_repoRoot, CommitMessage, s.GitAutoPush);
                pushed = gitResult.Success;
                hash   = gitResult.CommitHash ?? string.Empty;
                branch = await _git.GetCurrentBranchAsync(_repoRoot);
                if (gitResult.Success)
                    _log(string.Format("[Tracker] ✅ Git push OK — hash: {0}, branch: {1}", hash, branch));
                else
                    _log("[Tracker] ❌ Git push failed: " + gitResult.Error);
                _eventBus.Publish(new CommitPushedEvent
                {
                    CommitMessage = CommitMessage, Success = pushed, Hash = hash, Error = gitResult.Error
                });
            }
            else if (!push && GitAvailable && !string.IsNullOrEmpty(_repoRoot))
            {
                branch = await _git.GetCurrentBranchAsync(_repoRoot);
            }

            var startedAt = _timer.StartedAt ?? completedAt.AddSeconds(-_timer.GetElapsed().TotalSeconds);
            var report = CompletionReport.CreateBuilder()
                .TaskId(_currentTask?.Id ?? string.Empty)
                .TaskTitle(_currentTask?.Title ?? TaskTitle)
                .TaskUrl(_currentTask?.Url ?? TaskUrl)
                .Labels(_currentTask?.Labels ?? new string[0])
                .Description(_currentTask?.Description ?? TaskDescription)
                .StartedAt(startedAt).CompletedAt(completedAt)
                .TotalElapsed(_timer.GetElapsed()).Sessions(sessions)
                .WorkNotes(WorkNotes).BusinessLogic(BusinessLogic)
                .Todos(Todos.Select(v => v.Model).ToList())
                .CommitMessage(CommitMessage).GitBranch(branch)
                .GitCommitHash(hash).WasPushed(pushed)
                .Build();

            var histDir = _storage.GetHistoryDirectory();
            try
            {
                await _storage.ArchiveReportAsync(report);
                await _reportGenerator.GenerateAsync(report, histDir);
                _log(string.Format("[Tracker] ✅ Report saved — elapsed: {0}, todos: {1}/{2}",
                    FormatSpan(report.TotalElapsed), report.TodoDone, report.TodoTotal));
            }
            catch (Exception ex) { _log("[Tracker] ❌ Report save error: " + ex.Message); }

            _eventBus.Publish(new ReportSavedEvent   { FilePath = report.MarkdownFilePath ?? string.Empty, Report = report });
            _eventBus.Publish(new TaskCompletedEvent { Report = report });

            if (push) await _storage.ClearCurrentTaskAsync();

            StatusMessage = push
                ? string.Format("✅ Hoàn thành — {0}", FormatSpan(_timer.GetElapsed()))
                : string.Format("⏸ Đã lưu tạm — {0}", FormatSpan(_timer.GetElapsed()));

            _log(string.Format("[Tracker] {0} — {1}",
                push ? "✅ Task completed" : "⏸ Task saved & paused",
                FormatSpan(_timer.GetElapsed())));

            if (push) ClearTask();
        }

        // ── Templates ─────────────────────────────────────────────────────
        private void RefreshTodoTemplates()
        {
            TodoTemplates.Clear();
            var templates = _settings().TodoTemplates;
            if (templates != null)
                foreach (var t in templates) TodoTemplates.Add(t);
            OnProp(nameof(HasTodoTemplates));   // v3.8: update Expander visibility
        }

        // ── Auto-save ─────────────────────────────────────────────────────
        private void StartAutoSave()
        {
            _autoSave = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _autoSave.Tick += (s, e) => AutoSaveFireAndForget();
            _autoSave.Start();
        }

        private async Task AutoSaveCurrentAsync()
        {
            try
            {
                var log = BuildWorkLog();
                if (log != null)
                {
                    await _storage.SaveCurrentTaskAsync(log);
                    _log("[Tracker] 💾 Auto-saved current task.");
                }
            }
            catch { }
        }

        private WorkLog BuildWorkLog()
        {
            if (_currentTask == null && string.IsNullOrEmpty(TaskTitle)) return null;
            return new WorkLog
            {
                Task          = _currentTask,
                Sessions      = _timer.GetSessions(),
                Todos         = Todos.Select(v => v.Model).ToList(),
                WorkNotes     = WorkNotes,
                BusinessLogic = BusinessLogic,
                CommitMessage = CommitMessage,
                StartedAt     = _timer.StartedAt,
                TimerState    = TimerState,
                RepoRoot      = _repoRoot
            };
        }

        public async Task RestoreFromLogAsync(WorkLog log)
        {
            if (log == null) return;
            _currentTask = log.Task;
            if (log.Task != null)
            {
                TaskTitle       = log.Task.Title;
                TaskDescription = log.Task.Description;
                TaskUrl         = log.Task.Url;
                LabelsDisplay   = string.Join(", ", log.Task.Labels ?? new string[0]);
            }
            WorkNotes     = log.WorkNotes;
            BusinessLogic = log.BusinessLogic;
            CommitMessage = log.CommitMessage;
            _repoRoot     = log.RepoRoot ?? string.Empty;

            _timer.RestoreFrom(log.Sessions, TrackingState.Paused);
            TimerState = "Paused";  // setter refreshes todo CanStart

            Todos.Clear();
            foreach (var t in (log.Todos ?? new System.Collections.Generic.List<TodoItem>()))
                Todos.Add(CreateTodoVm(t));

            RefreshTodoTemplates();
            _log(string.Format("[Tracker] 🔄 Restored task: \"{0}\" ({1} TODOs)", TaskTitle, Todos.Count));
            StatusMessage = string.Format("▶ Task \"{0}\" được khôi phục. Nhấn Bắt đầu để tiếp tục.", TaskTitle);
            await Task.CompletedTask;
        }

        // ── UI timer ──────────────────────────────────────────────────────
        private void StartUiTimer()
        {
            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _uiTimer.Tick += (s, e) =>
            {
                var ts = _timer.GetElapsed();
                TimerDisplay = string.Format("{0:D2}:{1:D2}:{2:D2}",
                    (int)ts.TotalHours, ts.Minutes, ts.Seconds);
            };
            _uiTimer.Start();
        }

        private void DetectRepoRoot()
        {
            try { _repoRoot = _git?.FindRepoRoot(AppDomain.CurrentDomain.BaseDirectory) ?? string.Empty; }
            catch { }
        }

        private void RaiseCommandsChanged()
        {
            (StartCommand   as RelayCommand)?.RaiseCanExecuteChanged();
            (PauseCommand   as RelayCommand)?.RaiseCanExecuteChanged();
            (ResumeCommand  as RelayCommand)?.RaiseCanExecuteChanged();
            (StopCommand    as RelayCommand)?.RaiseCanExecuteChanged();
            (FetchCommand   as RelayCommand)?.RaiseCanExecuteChanged();
        }

        public System.Collections.Generic.IEnumerable<string> GetPauseReasons()
        {
            var r = _settings().TaskPauseReasons;
            return r != null && r.Count > 0
                ? (System.Collections.Generic.IEnumerable<string>)r
                : new[] { "Hết giờ làm việc", "Chuyển việc khác", "Lý do khác" };
        }

        private static string FormatSpan(TimeSpan ts) =>
            string.Format("{0}h {1}m {2}s", (int)ts.TotalHours, ts.Minutes, ts.Seconds);

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnProp(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
