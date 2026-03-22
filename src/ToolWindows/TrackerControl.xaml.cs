using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DhCodetaskExtension.Core.Models;
using DhCodetaskExtension.Core.Services;
using DhCodetaskExtension.ViewModels;

namespace DhCodetaskExtension.ToolWindows
{
    /// <summary>Converts .sln/.csproj extension to icon character.</summary>
    public sealed class SolutionExtIconConverter : IValueConverter
    {
        public static readonly SolutionExtIconConverter Instance = new SolutionExtIconConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var ext = value as string ?? string.Empty;
            return ext == ".sln" ? "⬡" : "◈";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    public partial class TrackerControl : UserControl
    {
        private readonly TrackerViewModel _vm;

        public TrackerControl(TrackerViewModel vm)
        {
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));

            if (Application.Current != null)
                Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;

            // Wire pause reason dialog before InitializeComponent
            _vm.ShowPauseReasonDialog = ShowPauseReasonDialogImpl;

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
                return null; // cancelled
            }
            catch (Exception ex)
            {
                AppLogger.Instance.Error("ShowPauseReasonDialog", ex);
                return string.Empty; // proceed with empty reason
            }
        }

        // ── Button click handlers ─────────────────────────────────────────

        private static void OnDispatcherUnhandledException(object sender,
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            AppLogger.Instance.Error("DispatcherUnhandled", e.Exception);
            e.Handled = true;
        }

        private void BtnHistory_Click(object sender, RoutedEventArgs e)
        {
            AppLogger.Instance.TryCatch("BtnHistory_Click", () => _vm.OpenHistoryAction?.Invoke());
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

        // ── Solution file handlers ────────────────────────────────────────

        private void SolutionExpander_Expanded(object sender, RoutedEventArgs e)
        {
            // Lazy load on first expand
            if (_vm.SolutionFiles.Count == 0 && !_vm.SolutionIsLoading)
                _vm.RefreshSolutionFilesCommand.Execute(null);
        }

        private void SolutionFile_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            AppLogger.Instance.TryCatch("SolutionFile_Click", () =>
            {
                if (sender is FrameworkElement fe && fe.DataContext is SolutionFileEntry entry)
                {
                    AppLogger.Instance.Info("[UI] Open solution file: " + entry.FullPath);
                    try { Process.Start(entry.FullPath); }
                    catch (Exception ex) { AppLogger.Instance.Error("SolutionFile_Click", ex); }
                }
            });
        }

        private void SolutionFileCopy_Click(object sender, RoutedEventArgs e)
        {
            AppLogger.Instance.TryCatch("SolutionFileCopy_Click", () =>
            {
                if (sender is FrameworkElement fe && fe.Tag is string path && !string.IsNullOrEmpty(path))
                {
                    try
                    {
                        Clipboard.SetText(path);
                        AppLogger.Instance.Info("[UI] Copied path: " + path);
                    }
                    catch (Exception ex) { AppLogger.Instance.Error("SolutionFileCopy_Click", ex); }
                }
            });
        }
    }
}
