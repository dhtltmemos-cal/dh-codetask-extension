using System;
using System.Windows;
using System.Windows.Controls;
using DhCodetaskExtension.Services;
using Microsoft.VisualStudio.Shell;

namespace DhCodetaskExtension.ToolWindows
{
    /// <summary>
    /// WPF Settings dialog, modal, mở từ Tools menu.
    /// Load values từ ConfigurationService, validate, save khi click Save.
    ///
    /// HOW TO CUSTOMIZE:
    ///   - Thêm fields vào SettingsDialog.xaml và populate/save chúng ở đây
    ///   - Cập nhật Validate() với validation rules của bạn
    /// </summary>
    public partial class SettingsDialog : Window
    {
        private readonly ConfigurationService _config;
        private readonly OutputWindowService  _outputWindow;
        private readonly StatusBarService     _statusBar;

        public SettingsDialog(
            ConfigurationService config,
            OutputWindowService  outputWindow,
            StatusBarService     statusBar)
        {
            _config       = config       ?? throw new ArgumentNullException(nameof(config));
            _outputWindow = outputWindow ?? throw new ArgumentNullException(nameof(outputWindow));
            _statusBar    = statusBar    ?? throw new ArgumentNullException(nameof(statusBar));
            InitializeComponent();
            Loaded += OnLoaded;
        }

        // ------------------------------------------------------------------ //
        //  Load current values into form                                       //
        // ------------------------------------------------------------------ //

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            TxtServerUrl.Text         = _config.ServerUrl;
            PwdApiKey.Password        = _config.ApiKey;
            TxtTimeout.Text           = _config.TimeoutSeconds.ToString();
            TxtMaxResults.Text        = _config.MaxResults.ToString();
            ChkDebug.IsChecked        = _config.DebugMode;
            ChkEnableLogging.IsChecked= _config.EnableLogging;
            SelectComboItem(CmbOutputFormat, _config.OutputFormat);
            ValidationBorder.Visibility = Visibility.Collapsed;
        }

        private void SelectComboItem(ComboBox cmb, string text)
        {
            foreach (ComboBoxItem item in cmb.Items)
            {
                if (string.Equals(item.Content?.ToString(), text, StringComparison.OrdinalIgnoreCase))
                {
                    cmb.SelectedItem = item;
                    return;
                }
            }
            if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
        }

        // ------------------------------------------------------------------ //
        //  Validation                                                          //
        // ------------------------------------------------------------------ //

        private bool Validate(out string error)
        {
            string url = TxtServerUrl.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(url))
            { error = "Server URL không được để trống."; return false; }
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            { error = "Server URL phải bắt đầu bằng http:// hoặc https://"; return false; }
            if (!int.TryParse(TxtTimeout.Text, out int t) || t < 1 || t > 300)
            { error = "Timeout phải là số nguyên trong khoảng 1–300."; return false; }
            if (!int.TryParse(TxtMaxResults.Text, out int m) || m < 1 || m > 1000)
            { error = "Max Results phải là số nguyên trong khoảng 1–1000."; return false; }
            error = null;
            return true;
        }

        // ------------------------------------------------------------------ //
        //  Button handlers                                                     //
        // ------------------------------------------------------------------ //

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!Validate(out string error))
            {
                TxtValidation.Text          = "⚠  " + error;
                ValidationBorder.Visibility = Visibility.Visible;
                return;
            }

            _config.Set(ConfigurationService.KeyServerUrl,      TxtServerUrl.Text.Trim());
            _config.Set(ConfigurationService.KeyApiKey,         PwdApiKey.Password);
            _config.Set(ConfigurationService.KeyTimeoutSeconds, int.Parse(TxtTimeout.Text));
            _config.Set(ConfigurationService.KeyMaxResults,     int.Parse(TxtMaxResults.Text));
            _config.Set(ConfigurationService.KeyDebugMode,      ChkDebug.IsChecked == true);
            _config.Set(ConfigurationService.KeyEnableLogging,  ChkEnableLogging.IsChecked == true);
            string fmt = (CmbOutputFormat.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "JSON";
            _config.Set(ConfigurationService.KeyOutputFormat, fmt);

            _outputWindow.Log("[Settings] Saved from Settings dialog.");
            _statusBar.SetText("Settings saved.");
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
