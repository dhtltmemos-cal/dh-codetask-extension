using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using DhCodetaskExtension.Core.Services;
using DhCodetaskExtension.ViewModels;

namespace DhCodetaskExtension.ToolWindows
{
    public partial class TrackerControl : UserControl
    {
        private readonly TrackerViewModel _vm;

        public TrackerControl(TrackerViewModel vm)
        {
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));

            if (Application.Current != null)
                Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;

            _vm.ShowPauseReasonDialog = ShowPauseReasonDialogImpl;

            // v3.10: wire clipboard action so copy commands work from ViewModel
            _vm.CopyToClipboardAction = text =>
            {
                if (string.IsNullOrEmpty(text)) return;
                AppLogger.Instance.TryCatch("CopyToClipboard", () =>
                {
                    Clipboard.SetText(text);
                    AppLogger.Instance.Info("[UI] 📋 Copied: " + text);
                });
            };

            try
            {
                InitializeComponent();
                DataContext = _vm;
            }
            catch (Exception ex)
            {
                AppLogger.Instance.Error("TrackerControl.ctor", ex);
            }
        }

        // ── Pause reason dialog ───────────────────────────────────────────

        private string ShowPauseReasonDialogImpl()
        {
            try
            {
                var reasons = _vm != null
                    ? new System.Collections.Generic.List<string>(_vm.GetPauseReasons())
                    : new System.Collections.Generic.List<string> { "Hết giờ làm việc", "Chuyển việc khác", "Lý do khác" };
                var dlg = new PauseReasonDialog(reasons);
                var result = dlg.ShowDialog();
                if (result == true) return dlg.SelectedReason;
                return null;
            }
            catch (Exception ex)
            {
                AppLogger.Instance.Error("ShowPauseReasonDialog", ex);
                return string.Empty;
            }
        }

        // ── Dispatcher exception guard ────────────────────────────────────

        private static void OnDispatcherUnhandledException(object sender,
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            AppLogger.Instance.Error("DispatcherUnhandled", e.Exception);
            e.Handled = true;
        }

        // ── Button handlers ───────────────────────────────────────────────

        private void BtnHistory_Click(object sender, RoutedEventArgs e)
        {
            AppLogger.Instance.TryCatch("BtnHistory_Click", () => _vm.OpenHistoryAction?.Invoke());
        }

        private void BtnProjectHelper_Click(object sender, RoutedEventArgs e)
        {
            AppLogger.Instance.TryCatch("BtnProjectHelper_Click", () =>
            {
                AppLogger.Instance.Info("[UI] Opening Project Helper panel.");
                _vm.OpenProjectHelperAction?.Invoke();
            });
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            AppLogger.Instance.TryCatch("BtnSettings_Click", () => _vm.OpenSettingsAction?.Invoke());
        }

        private void BtnOpenLog_Click(object sender, RoutedEventArgs e)
        {
            AppLogger.Instance.TryCatch("BtnOpenLog_Click", () => _vm.OpenLogFileAction?.Invoke());
        }

        private void BtnOpenConfig_Click(object sender, RoutedEventArgs e)
        {
            AppLogger.Instance.TryCatch("BtnOpenConfig_Click", () => _vm.OpenConfigFileAction?.Invoke());
        }

        private void BtnOpenUrl_Click(object sender, RoutedEventArgs e)
        {
            AppLogger.Instance.TryCatch("BtnOpenUrl_Click", () =>
            {
                var url = _vm.TaskUrl?.Trim();
                if (!string.IsNullOrEmpty(url))
                {
                    AppLogger.Instance.Info("[UI] Open URL: " + url);
                    try { Process.Start(url); }
                    catch (Exception ex) { AppLogger.Instance.Error("BtnOpenUrl_Click", ex); }
                }
            });
        }
    }
}
