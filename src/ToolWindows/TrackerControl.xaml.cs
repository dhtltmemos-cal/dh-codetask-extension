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
            // Mark as handled so VS does NOT crash/freeze — log and continue
            e.Handled = true;
        }

        private void BtnHistory_Click(object sender, RoutedEventArgs e)
        {
            AppLogger.Instance.TryCatch("BtnHistory_Click",
                () => _vm.OpenHistoryAction?.Invoke());
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            AppLogger.Instance.TryCatch("BtnSettings_Click",
                () => _vm.OpenSettingsAction?.Invoke());
        }
    }
}
