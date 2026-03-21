using System;
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

            // Catch ALL unhandled WPF binding/render exceptions in this control
            // so "Working on it..." never gets stuck
            if (Application.Current != null)
                Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;

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

        private static void OnDispatcherUnhandledException(object sender,
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            AppLogger.Instance.Error("DispatcherUnhandled", e.Exception);
            e.Handled = true;
        }

        // ── Button click handlers ─────────────────────────────────────────

        private void BtnHistory_Click(object sender, RoutedEventArgs e)
        {
            AppLogger.Instance.TryCatch("BtnHistory_Click", () =>
            {
                AppLogger.Instance.Info("[UI] User clicked: Xem Lịch sử");
                _vm.OpenHistoryAction?.Invoke();
            });
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            AppLogger.Instance.TryCatch("BtnSettings_Click", () =>
            {
                AppLogger.Instance.Info("[UI] User clicked: Settings (JSON)");
                _vm.OpenSettingsAction?.Invoke();
            });
        }

        private void BtnOpenLog_Click(object sender, RoutedEventArgs e)
        {
            AppLogger.Instance.TryCatch("BtnOpenLog_Click", () =>
            {
                AppLogger.Instance.Info("[UI] User clicked: Open Log File");
                _vm.OpenLogFileAction?.Invoke();
            });
        }

        private void BtnOpenConfig_Click(object sender, RoutedEventArgs e)
        {
            AppLogger.Instance.TryCatch("BtnOpenConfig_Click", () =>
            {
                AppLogger.Instance.Info("[UI] User clicked: Open Config File");
                _vm.OpenConfigFileAction?.Invoke();
            });
        }
    }
}
