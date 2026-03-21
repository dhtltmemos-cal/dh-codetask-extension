using System;
using System.IO;
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
        "DevTask Tracker — Gitea, Time Tracking, TODO, Reports.", "3.0")]
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
        public StatusBarService StatusBar { get; private set; }
        public AppSettings Settings { get; set; }

        // ── Singletons ────────────────────────────────────────────────────
        private EventBus _eventBus;
        private JsonStorageService _storage;
        private HistoryQueryService _historyRepo;
        private GitService _gitService;
        private TaskProviderFactory _taskFactory;
        private CompositeReportGenerator _reportGen;
        private WebhookNotificationProvider _webhook;
        private TimeTrackingService _taskTimer;

        // ViewModels kept as fields so InitializeToolWindowAsync can pass them
        private TrackerViewModel _trackerVm;
        private HistoryViewModel _historyVm;

        // ── Package initialization ────────────────────────────────────────
        protected override async Task InitializeAsync(
            CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // 1. Infrastructure
            OutputWindow = new OutputWindowService(this);
            StatusBar = new StatusBarService(this);
            await OutputWindow.InitializeAsync();
            await StatusBar.InitializeAsync();

            // 2. Storage + Settings
            _storage = new JsonStorageService(GetStorageRoot);
            Settings = await _storage.LoadSettingsAsync();

            // 3. Core services
            _eventBus = new EventBus();
            _historyRepo = new HistoryQueryService(() => Path.Combine(GetStorageRoot(), "history"));
            _historyRepo.StartWatcher();
            _gitService = new GitService(() => Settings);
            _taskTimer = new TimeTrackingService();

            // 4. Task providers
            _taskFactory = new TaskProviderFactory();
            _taskFactory.Register(new ManualTaskProvider());
            _taskFactory.Register(new GiteaTaskProvider(() => Settings));

            // 5. Report generators
            _reportGen = new CompositeReportGenerator();
            _reportGen.Register(new JsonReportGenerator());
            _reportGen.Register(new MarkdownReportGenerator());

            // 6. Webhook
            _webhook = new WebhookNotificationProvider(() => Settings);
            _eventBus.Subscribe<TaskCompletedEvent>(e => _webhook.OnEvent(e));

            // 7. ViewModels — must be created BEFORE InitializeToolWindowAsync is called
            _trackerVm = new TrackerViewModel(
                _eventBus, _storage, _gitService, _reportGen, _taskTimer,
                () => Settings, msg => OutputWindow.Log(msg));
            _trackerVm.FetchTaskFunc = url => _taskFactory.FetchAsync(url);
            _trackerVm.OpenSettingsAction = OpenSettings;
            _trackerVm.OpenHistoryAction = () => JoinableTaskFactory.RunAsync(ShowHistoryWindowAsync);

            _historyVm = new HistoryViewModel(_historyRepo, msg => OutputWindow.Log(msg));
            _historyVm.OpenDetailAction = OpenReportDetail;
            _historyVm.OpenFileAction = OpenFileInShell;

            // 8. Switch to UI thread for command registration
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            await ShowTrackerWindow.InitializeAsync(this);
            await ShowHistoryWindow.InitializeAsync(this);
            await ShowTaskSettings.InitializeAsync(this);
            await ShowMainWindow.InitializeAsync(this);
            await ShowSettings.InitializeAsync(this);
            await ShowJsonSettings.InitializeAsync(this);

            // 9. Restore in-progress task
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

            OutputWindow.Log("[DevTaskTracker] v3.0 loaded.");
            StatusBar.SetText("DevTask Tracker ready.");
        }

        // ── Tool window factory ───────────────────────────────────────────
        public override IVsAsyncToolWindowFactory GetAsyncToolWindowFactory(Guid toolWindowType)
        {
            if (toolWindowType.Equals(Guid.Parse(TrackerToolWindow.WindowGuidString)) ||
                toolWindowType.Equals(Guid.Parse(HistoryToolWindow.WindowGuidString)) ||
                toolWindowType.Equals(Guid.Parse(MainToolWindow.WindowGuidString)))
                return this;
            return null;
        }

        protected override string GetToolWindowTitle(Type toolWindowType, int id)
        {
            if (toolWindowType == typeof(TrackerToolWindow)) return TrackerToolWindow.Title;
            if (toolWindowType == typeof(HistoryToolWindow)) return HistoryToolWindow.Title;
            if (toolWindowType == typeof(MainToolWindow)) return MainToolWindow.Title;
            return base.GetToolWindowTitle(toolWindowType, id);
        }

        /// <summary>
        /// KEY FIX: Return the ViewModel as the state object.
        /// The ToolWindow constructor receives this state and uses it to set Content.
        /// This is called both on VS startup (for docked windows) and on ShowToolWindowAsync.
        /// </summary>
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
                    StatusBar = StatusBar,
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

        // ── Settings ──────────────────────────────────────────────────────
        public void OpenSettings()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dlg = new TaskSettingsDialog(Settings);
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                Settings = dlg.Result;
                JoinableTaskFactory.RunAsync(() => _storage.SaveSettingsAsync(dlg.Result));
                OutputWindow.Log("[Settings] Saved.");
                StatusBar.SetText("Settings saved.");
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
                try { System.Diagnostics.Process.Start(path); } catch { }
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