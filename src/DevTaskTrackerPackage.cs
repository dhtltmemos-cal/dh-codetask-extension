using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DhCodetaskExtension.Core.Events;
using DhCodetaskExtension.Core.Interfaces;
using DhCodetaskExtension.Core.Models;
using DhCodetaskExtension.Core.Services;
using DhCodetaskExtension.Providers.GitProviders;
using DhCodetaskExtension.Providers.NotificationProviders;
using DhCodetaskExtension.Providers.ReportProviders;
using DhCodetaskExtension.Providers.StorageProviders;
using DhCodetaskExtension.Providers.TaskProviders;
using DhCodetaskExtension.Services;
using DhCodetaskExtension.ToolWindows;
using DhCodetaskExtension.ViewModels;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace DhCodetaskExtension
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(
        "DH Codetask Extension",
        "DevTask Tracker — Gitea, Time Tracking, TODO, Reports.", "3.3")]
    [ProvideToolWindow(typeof(TrackerToolWindow),
        Style = VsDockStyle.Tabbed, DockedWidth = 360,
        Window = "DocumentWell", Orientation = ToolWindowOrientation.Left)]
    [ProvideToolWindow(typeof(HistoryToolWindow),
        Style = VsDockStyle.Tabbed, DockedWidth = 500,
        Window = "DocumentWell", Orientation = ToolWindowOrientation.Right)]
    [Guid(PackageGuids.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideOptionPage(typeof(SampleOptionsPage),
        "DH Codetask Extension", "General", 0, 0, supportsAutomation: true)]
    public sealed class DevTaskTrackerPackage : AsyncPackage
    {
        // ── Public services ───────────────────────────────────────────────
        public OutputWindowService OutputWindow { get; private set; }
        public StatusBarService    StatusBar    { get; private set; }
        public AppSettings         Settings     { get; set; }

        // ── Singletons ────────────────────────────────────────────────────
        private EventBus                    _eventBus;
        private JsonStorageService          _storage;
        private HistoryQueryService         _historyRepo;
        private GitService                  _gitService;
        private TaskProviderFactory         _taskFactory;
        private CompositeReportGenerator    _reportGen;
        private WebhookNotificationProvider _webhook;
        private TimeTrackingService         _taskTimer;
        private SolutionFileService         _solutionFileService;

        private TrackerViewModel _trackerVm;
        private HistoryViewModel _historyVm;

        // ── Initialization ────────────────────────────────────────────────
        protected override async Task InitializeAsync(
            CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // 1. Infrastructure
            OutputWindow = new OutputWindowService(this);
            StatusBar    = new StatusBarService(this);
            await OutputWindow.InitializeAsync();
            await StatusBar.InitializeAsync();

            // 2. Storage + Settings
            _storage  = new JsonStorageService(GetStorageRoot);
            Settings  = await _storage.LoadSettingsAsync();

            AppLogger.Instance.Init(OutputWindow);
            AppLogger.Instance.Info("DevTaskTracker v3.3 initializing...");

            // 3. Core services
            _eventBus    = new EventBus();
            _historyRepo = new HistoryQueryService(() => Path.Combine(GetStorageRoot(), "history"));
            _historyRepo.StartWatcher();
            _gitService  = new GitService(() => Settings);
            _taskTimer   = new TimeTrackingService();

            // 4. Solution file service (v3.3)
            _solutionFileService = new SolutionFileService(() => Settings, GetStorageRoot);

            // 5. Task providers
            _taskFactory = new TaskProviderFactory();
            _taskFactory.Register(new ManualTaskProvider());
            _taskFactory.Register(new GiteaTaskProvider(() => Settings));

            // 6. Report generators
            _reportGen = new CompositeReportGenerator();
            _reportGen.Register(new JsonReportGenerator());
            _reportGen.Register(new MarkdownReportGenerator());

            // 7. Webhook
            _webhook = new WebhookNotificationProvider(() => Settings);
            _eventBus.Subscribe<TaskCompletedEvent>(e => _webhook.OnEvent(e));

            // 8. ViewModels
            _trackerVm = new TrackerViewModel(
                _eventBus, _storage, _gitService, _reportGen, _taskTimer,
                () => Settings, msg => { try { ThreadHelper.ThrowIfNotOnUIThread(); OutputWindow.Log(msg); } catch { } });

            _trackerVm.FetchTaskFunc        = url => _taskFactory.FetchAsync(url);
            _trackerVm.OpenSettingsAction   = OpenSettings;
            _trackerVm.OpenHistoryAction    = () => JoinableTaskFactory.RunAsync(ShowHistoryWindowAsync);
            _trackerVm.OpenLogFileAction    = OpenLogFile;
            _trackerVm.OpenConfigFileAction = OpenConfigFile;
            _trackerVm.SolutionFileService  = _solutionFileService;

            // Wire history loader for URL duplicate check
            _trackerVm.LoadAllHistoryFunc = async () =>
            {
                try { return await _historyRepo.GetAllAsync(); }
                catch { return System.Linq.Enumerable.Empty<CompletionReport>(); }
            };

            _historyVm = new HistoryViewModel(_historyRepo, msg => { try { ThreadHelper.ThrowIfNotOnUIThread(); OutputWindow.Log(msg); } catch { } });
            _historyVm.OpenDetailAction = OpenReportDetail;
            _historyVm.OpenFileAction   = OpenFileInShell;

            // Wire: resume task from history
            _historyVm.ResumeFromHistoryAction = report =>
            {
                if (report == null) return;
                OutputWindow.Log(string.Format("[History] ▶ Resuming task: #{0} — {1}",
                    report.TaskId, report.TaskTitle));
                var workLog = BuildWorkLogFromReport(report);
                JoinableTaskFactory.RunAsync(async () =>
                {
                    await _trackerVm.RestoreFromLogAsync(workLog);
                    await ShowTrackerWindowAsync();
                    OutputWindow.Log(string.Format("[History] ✔ Task #{0} loaded into Tracker.", report.TaskId));
                });
            };

            // Wire: open URL
            _historyVm.OpenUrlAction = url =>
            {
                if (string.IsNullOrEmpty(url)) return;
                OutputWindow.Log("[History] 🔗 Opening URL: " + url);
                try { Process.Start(url); }
                catch (Exception ex) { OutputWindow.Log("[History] ❌ " + ex.Message); }
            };

            // 9. UI thread — command registration
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            await ShowTrackerWindow.InitializeAsync(this);
            await ShowHistoryWindow.InitializeAsync(this);
            await ShowTaskSettings.InitializeAsync(this);
            await ShowMainWindow.InitializeAsync(this);
            await ShowSettings.InitializeAsync(this);
            await ShowJsonSettings.InitializeAsync(this);
            await OpenLogFileCommand.InitializeAsync(this);
            await OpenConfigFileCommand.InitializeAsync(this);

            // 10. Restore in-progress task
            try
            {
                var existing = await _storage.LoadCurrentTaskAsync();
                if (existing != null)
                {
                    await _trackerVm.RestoreFromLogAsync(existing);
                    OutputWindow.Log(string.Format("[Restore] Task: \"{0}\"", existing.Task?.Title));
                    StatusBar.SetText(string.Format("Task dở: {0} — Resume trong Tracker.", existing.Task?.Title));
                }
            }
            catch (Exception ex) { OutputWindow.Log("[Restore] " + ex.Message); }

            OutputWindow.Log("[DevTaskTracker] v3.3 loaded. Settings: " + _storage.GetSettingsFilePath());
            StatusBar.SetText("DevTask Tracker ready.");
        }

        // ── Tool window factory ───────────────────────────────────────────
        public override IVsAsyncToolWindowFactory GetAsyncToolWindowFactory(Guid toolWindowType)
        {
            if (toolWindowType.Equals(Guid.Parse(TrackerToolWindow.WindowGuidString)) ||
                toolWindowType.Equals(Guid.Parse(HistoryToolWindow.WindowGuidString))  ||
                toolWindowType.Equals(Guid.Parse(MainToolWindow.WindowGuidString)))
                return this;
            return null;
        }

        protected override string GetToolWindowTitle(Type toolWindowType, int id)
        {
            if (toolWindowType == typeof(TrackerToolWindow)) return TrackerToolWindow.Title;
            if (toolWindowType == typeof(HistoryToolWindow)) return HistoryToolWindow.Title;
            if (toolWindowType == typeof(MainToolWindow))    return MainToolWindow.Title;
            return base.GetToolWindowTitle(toolWindowType, id);
        }

        protected override Task<object> InitializeToolWindowAsync(
            Type toolWindowType, int id, CancellationToken cancellationToken)
        {
            if (toolWindowType == typeof(TrackerToolWindow))
                return Task.FromResult<object>(_trackerVm);
            if (toolWindowType == typeof(HistoryToolWindow))
                return Task.FromResult<object>(_historyVm);
            if (toolWindowType == typeof(MainToolWindow))
                return Task.FromResult<object>(new MainToolWindowState
                {
                    OutputWindow = OutputWindow,
                    StatusBar    = StatusBar,
                });
            return Task.FromResult<object>(null);
        }

        // ── Public show methods ───────────────────────────────────────────
        public async Task ShowTrackerWindowAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();
            await ShowToolWindowAsync(typeof(TrackerToolWindow), 0, true, DisposalToken);
        }

        public async Task ShowHistoryWindowAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();
            await ShowToolWindowAsync(typeof(HistoryToolWindow), 0, true, DisposalToken);
        }

        // ── Settings ─────────────────────────────────────────────────────
        public void OpenSettings()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            OutputWindow.Log("[Settings] Opening JSON settings editor...");
            var dlg = new AppSettingsJsonDialog(
                Settings,
                _storage.GetSettingsFilePath(),
                newSettings =>
                {
                    Settings = newSettings;
                    JoinableTaskFactory.RunAsync(() => _storage.SaveSettingsAsync(newSettings));
                    OutputWindow.Log("[Settings] ✔ Settings saved.");
                    StatusBar.SetText("Settings saved.");
                });
            dlg.ShowDialog();
        }

        // ── Log & Config quick-open ───────────────────────────────────────
        public void OpenLogFile()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dir     = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DhCodetaskExtension", "logs");
            var logFile = Path.Combine(dir, string.Format("devtask_{0:yyyyMMdd}.log", DateTime.Today));

            if (!File.Exists(logFile) && Directory.Exists(dir))
            {
                var files = Directory.GetFiles(dir, "*.log");
                if (files.Length > 0) logFile = files.OrderByDescending(f => f).First();
            }

            if (File.Exists(logFile))
            {
                OutputWindow.Log("[Log] Opening: " + logFile);
                try { Process.Start(logFile); }
                catch (Exception ex) { OutputWindow.Log("[Log] ERROR: " + ex.Message); }
            }
            else
            {
                OutputWindow.Log("[Log] No log file found at: " + dir);
                StatusBar.SetText("No log file found.");
            }
        }

        public void OpenConfigFile()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var path = _storage.GetSettingsFilePath();
            if (File.Exists(path))
            {
                OutputWindow.Log("[Config] Opening: " + path);
                try { Process.Start(path); }
                catch (Exception ex) { OutputWindow.Log("[Config] ERROR: " + ex.Message); }
            }
            else
            {
                OutputWindow.Log("[Config] Creating defaults: " + path);
                JoinableTaskFactory.RunAsync(async () =>
                {
                    await _storage.SaveSettingsAsync(Settings ?? new AppSettings());
                    await JoinableTaskFactory.SwitchToMainThreadAsync();
                    if (File.Exists(path)) try { Process.Start(path); } catch { }
                });
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private void OpenReportDetail(CompletionReportSummary s)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            new ReportDetailDialog(s).ShowDialog();
        }

        private static void OpenFileInShell(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                try { Process.Start(path); } catch { }
        }

        private static WorkLog BuildWorkLogFromReport(CompletionReport r)
        {
            return new WorkLog
            {
                Task = new TaskItem
                {
                    Id          = r.TaskId          ?? string.Empty,
                    Title       = r.TaskTitle        ?? string.Empty,
                    Url         = r.TaskUrl          ?? string.Empty,
                    Labels      = r.Labels           ?? new string[0],
                    Description = r.Description      ?? string.Empty
                },
                Sessions      = new System.Collections.Generic.List<TimeSession>(),
                Todos         = new System.Collections.Generic.List<TodoItem>(
                                    r.Todos ?? new System.Collections.Generic.List<TodoItem>()),
                WorkNotes     = r.WorkNotes          ?? string.Empty,
                BusinessLogic = r.BusinessLogic      ?? string.Empty,
                CommitMessage = r.CommitMessage      ?? string.Empty,
                GitBranch     = r.GitBranch          ?? string.Empty,
                TimerState    = "Paused"
            };
        }

        private string GetStorageRoot()
        {
            if (!string.IsNullOrWhiteSpace(Settings?.StoragePath) &&
                Directory.Exists(Settings.StoragePath))
                return Settings.StoragePath;
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DhCodetaskExtension");
        }
    }
}
