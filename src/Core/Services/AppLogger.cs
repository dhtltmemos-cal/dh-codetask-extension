using System;
using System.IO;
using System.Text;
using System.Threading;
using DhCodetaskExtension.Services;

namespace DhCodetaskExtension.Core.Services
{
    /// <summary>
    /// Thread-safe logger: ghi vào file và VS Output Window.
    /// Tự động tạo file log trong %AppData%\DhCodetaskExtension\logs\.
    /// </summary>
    public sealed class AppLogger
    {
        private static AppLogger _instance;
        private static readonly object _initLock = new object();

        private readonly string _logFilePath;
        private readonly object _fileLock = new object();
        private OutputWindowService _outputWindow;

        private AppLogger()
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DhCodetaskExtension", "logs");
                Directory.CreateDirectory(dir);
                _logFilePath = Path.Combine(dir,
                    string.Format("devtask_{0:yyyyMMdd}.log", DateTime.Today));
            }
            catch { /* non-critical */ }
        }

        public static AppLogger Instance
        {
            get
            {
                if (_instance == null)
                    lock (_initLock)
                        if (_instance == null)
                            _instance = new AppLogger();
                return _instance;
            }
        }

        public void Init(OutputWindowService outputWindow)
        {
            _outputWindow = outputWindow;
        }

        // ── Public API ────────────────────────────────────────────────────

        public void Info(string message)  => Write("INFO ", message);
        public void Warn(string message)  => Write("WARN ", message);
        public void Error(string message) => Write("ERROR", message);

        public void Error(string context, Exception ex)
        {
            if (ex == null) { Error(context); return; }
            Write("ERROR", string.Format("[{0}] {1}\n  {2}", context, ex.Message,
                ex.StackTrace?.Replace("\n", "\n  ") ?? string.Empty));
        }

        /// <summary>
        /// Wrap an action in try/catch. Log the error and continue — never crash caller.
        /// </summary>
        public void TryCatch(string context, Action action)
        {
            try { action(); }
            catch (Exception ex) { Error(context, ex); }
        }

        /// <summary>
        /// Wrap an async operation in try/catch. Returns false if an exception occurred.
        /// </summary>
        public async System.Threading.Tasks.Task<bool> TryCatchAsync(string context,
            Func<System.Threading.Tasks.Task> action)
        {
            try { await action(); return true; }
            catch (Exception ex) { Error(context, ex); return false; }
        }

        // ── Private ───────────────────────────────────────────────────────

        private void Write(string level, string message)
        {
            var line = string.Format("[{0:HH:mm:ss}] [{1}] {2}",
                DateTime.Now, level, message);

            // Write to file (background, non-blocking)
            ThreadPool.QueueUserWorkItem(_ =>
            {
                if (string.IsNullOrEmpty(_logFilePath)) return;
                lock (_fileLock)
                {
                    try { File.AppendAllText(_logFilePath, line + "\n", Encoding.UTF8); }
                    catch { /* never crash */ }
                }
            });

            // Write to VS Output Window (must be marshalled via Dispatcher if on BG thread)
            var ow = _outputWindow;
            if (ow == null) return;
            try
            {
                if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() == true)
                {
                    Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
                    ow.Log(string.Format("[{0}] {1}", level.Trim(), message));
                }
                else
                {
                    System.Windows.Application.Current?.Dispatcher?.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.Background,
                        new Action(() =>
                        {
                            try
                            {
                                Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
                                ow.Log(string.Format("[{0}] {1}", level.Trim(), message));
                            }
                            catch { }
                        }));
                }
            }
            catch { /* never crash */ }
        }
    }
}
