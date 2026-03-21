using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DhCodetaskExtension.Core.Services;
using DhCodetaskExtension.ViewModels;
using Microsoft.VisualStudio.Shell;
using Microsoft.Win32;

namespace DhCodetaskExtension.ToolWindows
{
    public partial class HistoryControl : UserControl
    {
        private readonly HistoryViewModel _vm;
        private Point _downPoint;

        public HistoryControl(HistoryViewModel vm)
        {
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            try
            {
                InitializeComponent();
                DataContext = _vm;
            }
            catch (Exception ex)
            {
                AppLogger.Instance.Error("HistoryControl.ctor", ex);
            }
            // Use non-async Loaded handler to avoid VSTHRD101
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // JoinableTaskFactory.RunAsync: correct way to fire async from sync void handler
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                try { await _vm.RefreshAsync(); }
                catch (Exception ex) { AppLogger.Instance.Error("HistoryControl.OnLoaded", ex); }
            });
        }

        private void Filter_Today(object s, RoutedEventArgs e)
        {
            AppLogger.Instance.TryCatch("Filter_Today", () =>
            { _vm.ViewMode = HistoryViewMode.Today; CustomRangePanel.Visibility = Visibility.Collapsed; });
        }

        private void Filter_Week(object s, RoutedEventArgs e)
        {
            AppLogger.Instance.TryCatch("Filter_Week", () =>
            { _vm.ViewMode = HistoryViewMode.ThisWeek; CustomRangePanel.Visibility = Visibility.Collapsed; });
        }

        private void Filter_Month(object s, RoutedEventArgs e)
        {
            AppLogger.Instance.TryCatch("Filter_Month", () =>
            { _vm.ViewMode = HistoryViewMode.ThisMonth; CustomRangePanel.Visibility = Visibility.Collapsed; });
        }

        private void Filter_Custom(object s, RoutedEventArgs e)
        {
            AppLogger.Instance.TryCatch("Filter_Custom", () =>
            { CustomRangePanel.Visibility = Visibility.Visible; });
        }

        private void BtnRefresh_Click(object s, RoutedEventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                try { await _vm.RefreshAsync(); }
                catch (Exception ex) { AppLogger.Instance.Error("BtnRefresh_Click", ex); }
            });
        }

        private void Item_MouseDown(object s, MouseButtonEventArgs e) => _downPoint = e.GetPosition(null);

        private void Item_Click(object sender, MouseButtonEventArgs e)
        {
            AppLogger.Instance.TryCatch("Item_Click", () =>
            {
                var diff = e.GetPosition(null) - _downPoint;
                if (Math.Abs(diff.X) > 4 || Math.Abs(diff.Y) > 4) return;
                if (sender is FrameworkElement fe && fe.DataContext is CompletionReportSummary s)
                {
                    if (e.ClickCount == 2) _vm.OpenFileAction?.Invoke(s.MarkdownFilePath);
                    else _vm.OpenDetailAction?.Invoke(s);
                }
            });
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            AppLogger.Instance.TryCatch("BtnDelete_Click", () =>
            {
                if (!(sender is FrameworkElement fe) || !(fe.Tag is CompletionReportSummary s)) return;
                var r = MessageBox.Show(
                    string.Format("Xóa report #{0} — {1}?", s.TaskId, s.TaskTitle),
                    "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r == MessageBoxResult.Yes)
                    ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                    {
                        try { await _vm.DeleteAsync(s); }
                        catch (Exception ex) { AppLogger.Instance.Error("BtnDelete_Click.delete", ex); }
                    });
            });
        }

        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            AppLogger.Instance.TryCatch("BtnExportCsv_Click", () =>
            {
                var dlg = new SaveFileDialog
                {
                    Filter = "CSV files|*.csv",
                    FileName = string.Format("history_{0:yyyyMMdd}.csv", DateTime.Today)
                };
                if (dlg.ShowDialog() != true) return;
                var sb = new StringBuilder();
                sb.AppendLine("TaskId,TaskTitle,StartedAt,CompletedAt,TotalElapsed,TodoDone,TodoTotal,WasPushed");
                foreach (var item in _vm.Items)
                    sb.AppendLine(string.Format("{0},\"{1}\",{2},{3},{4},{5},{6},{7}",
                        item.TaskId, item.TaskTitle,
                        item.StartedAt.ToString("yyyy-MM-dd HH:mm"),
                        item.CompletedAt.ToString("yyyy-MM-dd HH:mm"),
                        item.ElapsedDisplay, item.TodoDone, item.TodoTotal, item.WasPushed));
                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("Export thành công!", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }
    }
}
