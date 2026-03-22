using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using DhCodetaskExtension.Core.Models;
using DhCodetaskExtension.Core.Services;
using DhCodetaskExtension.ViewModels;
using Microsoft.VisualStudio.Shell;

namespace DhCodetaskExtension.ToolWindows
{
    // ── Value converters ──────────────────────────────────────────────────

    public sealed class ExtBgConverter : IValueConverter
    {
        public static readonly ExtBgConverter Instance = new ExtBgConverter();
        private static readonly SolidColorBrush SlnBrush
            = new SolidColorBrush(Color.FromRgb(0x68, 0x21, 0x7A));
        private static readonly SolidColorBrush CsprojBrush
            = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC));

        public object Convert(object value, Type t, object p, CultureInfo c)
            => (value as string) == ".sln" ? (Brush)SlnBrush : (Brush)CsprojBrush;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    public sealed class ExtLabelConverter : IValueConverter
    {
        public static readonly ExtLabelConverter Instance = new ExtLabelConverter();
        public object Convert(object value, Type t, object p, CultureInfo c)
            => (value as string) == ".sln" ? "SLN" : "PROJ";
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    // ── Code-behind ───────────────────────────────────────────────────────

    public partial class ProjectHelperControl : UserControl
    {
        private readonly ProjectHelperViewModel _vm;

        public ProjectHelperControl(ProjectHelperViewModel vm)
        {
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            _vm.OpenFileAction   = OpenFileImpl;
            _vm.CopyToClipboard  = CopyToClipboardImpl;
            _vm.OpenFolderAction = OpenFolderImpl;
            try { InitializeComponent(); DataContext = _vm; }
            catch (Exception ex) { AppLogger.Instance.Error("ProjectHelperControl.ctor", ex); }
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                try { await _vm.RefreshAsync(forceRescan: false); }
                catch (Exception ex) { AppLogger.Instance.Error("ProjectHelperControl.OnLoaded", ex); }
            });
        }

        // ── File list buttons ─────────────────────────────────────────────

        private void BtnForceRefresh_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                try { await _vm.RefreshAsync(forceRescan: true); }
                catch (Exception ex) { AppLogger.Instance.Error("BtnForceRefresh_Click", ex); }
            });
        }

        private void FileItem_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            AppLogger.Instance.TryCatch("FileItem_Click", () =>
            {
                var fe = sender as FrameworkElement;
                if (fe == null) return;
                var entry = fe.DataContext as SolutionFileEntry;
                if (entry != null) OpenFileImpl(entry.FullPath);
            });
        }

        private void BtnOpenVs_Click(object sender, RoutedEventArgs e)
        {
            AppLogger.Instance.TryCatch("BtnOpenVs_Click", () =>
            {
                var fe = sender as FrameworkElement;
                if (fe == null) return;
                var entry = fe.Tag as SolutionFileEntry;
                if (entry != null) OpenFileImpl(entry.FullPath);
            });
        }

        private void BtnCopyPath_Click(object sender, RoutedEventArgs e)
        {
            AppLogger.Instance.TryCatch("BtnCopyPath_Click", () =>
            {
                var fe = sender as FrameworkElement;
                if (fe == null) return;
                var entry = fe.Tag as SolutionFileEntry;
                if (entry != null) CopyToClipboardImpl(entry.FullPath);
            });
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            AppLogger.Instance.TryCatch("BtnOpenFolder_Click", () =>
            {
                var fe = sender as FrameworkElement;
                if (fe == null) return;
                var entry = fe.Tag as SolutionFileEntry;
                if (entry != null)
                {
                    var dir = System.IO.Path.GetDirectoryName(entry.FullPath) ?? string.Empty;
                    OpenFolderImpl(dir);
                }
            });
        }

        // ── Search result buttons ─────────────────────────────────────────

        private void BtnOpenSearchResult_Click(object sender, RoutedEventArgs e)
        {
            AppLogger.Instance.TryCatch("BtnOpenSearchResult_Click", () =>
            {
                var fe = sender as FrameworkElement;
                if (fe == null) return;
                var result = fe.Tag as RipgrepSearchResult;
                if (result != null) OpenFileImpl(result.FilePath);
            });
        }

        private void BtnCopySearchPath_Click(object sender, RoutedEventArgs e)
        {
            AppLogger.Instance.TryCatch("BtnCopySearchPath_Click", () =>
            {
                var fe = sender as FrameworkElement;
                if (fe == null) return;
                var result = fe.Tag as RipgrepSearchResult;
                if (result != null)
                    CopyToClipboardImpl(string.Format("{0}:{1}", result.FilePath, result.LineNumber));
            });
        }

        // ── Implementations ───────────────────────────────────────────────

        private static void OpenFileImpl(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = path,
                    UseShellExecute = true
                });
                AppLogger.Instance.Info("[ProjectHelper] 📂 Opened: " + path);
            }
            catch (Exception ex)
            {
                AppLogger.Instance.Error("OpenFileImpl", ex);
                MessageBox.Show("Không thể mở file:\n" + ex.Message,
                    "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static void CopyToClipboardImpl(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            try { System.Windows.Clipboard.SetText(text); AppLogger.Instance.Info("[ProjectHelper] 📋 Copied: " + text); }
            catch (Exception ex) { AppLogger.Instance.Error("CopyToClipboardImpl", ex); }
        }

        private static void OpenFolderImpl(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !System.IO.Directory.Exists(dir)) return;
            try
            {
                Process.Start("explorer.exe", string.Format("\"{0}\"", dir));
                AppLogger.Instance.Info("[ProjectHelper] 📁 Explorer: " + dir);
            }
            catch (Exception ex) { AppLogger.Instance.Error("OpenFolderImpl", ex); }
        }
    }
}
