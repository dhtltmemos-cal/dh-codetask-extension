using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using DhCodetaskExtension.Core.Models;
using DhCodetaskExtension.Core.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DhCodetaskExtension.ToolWindows
{
    /// <summary>
    /// JSON editor cho AppSettings — nguồn cấu hình duy nhất của DevTaskTracker.
    /// Thay thế hoàn toàn form-based settings dialog.
    /// </summary>
    public partial class AppSettingsJsonDialog : Window
    {
        private readonly AppSettings _current;
        private readonly string      _filePath;
        private readonly Action<AppSettings> _saveCallback;
        private bool _suppress;

        public AppSettingsJsonDialog(AppSettings current, string filePath, Action<AppSettings> saveCallback)
        {
            _current      = current      ?? new AppSettings();
            _filePath     = filePath     ?? string.Empty;
            _saveCallback = saveCallback ?? throw new ArgumentNullException(nameof(saveCallback));

            InitializeComponent();
            Loaded += (s, e) =>
            {
                TxtJson.Text    = JsonConvert.SerializeObject(_current, Formatting.Indented);
                TxtFilePath.Text = _filePath;
                UpdateCounter();
                SetOk("✔  Settings loaded.");
            };
        }

        // ── Toolbar ───────────────────────────────────────────────────────

        private void BtnFormat_Click(object s, RoutedEventArgs e)
        {
            ParseResult r = TryParseJson(TxtJson.Text);
            if (r.Valid)
            {
                _suppress = true;
                TxtJson.Text = r.Obj.ToString(Formatting.Indented);
                _suppress = false;
                UpdateCounter();
                SetOk("✔  Formatted.");
            }
            else { SetErr("✘  " + r.Error); }
        }

        private void BtnValidate_Click(object s, RoutedEventArgs e)
        {
            ParseResult r = TryParseJson(TxtJson.Text);
            if (!r.Valid) { SetErr("✘  " + r.Error); return; }

            string err = ValidateAppSettings(r.Obj);
            if (err != null) SetWarn("⚠  " + err);
            else             SetOk("✔  JSON hợp lệ.");
        }

        private void BtnReset_Click(object s, RoutedEventArgs e)
        {
            if (MessageBox.Show("Reset về mặc định?", "Xác nhận",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _suppress = true;
                TxtJson.Text = JsonConvert.SerializeObject(new AppSettings(), Formatting.Indented);
                _suppress = false;
                UpdateCounter();
                SetInfo("↩  Default loaded. Bấm Save để áp dụng.");
            }
        }

        private void BtnReload_Click(object s, RoutedEventArgs e)
        {
            _suppress = true;
            TxtJson.Text = JsonConvert.SerializeObject(_current, Formatting.Indented);
            _suppress = false;
            UpdateCounter();
            SetInfo("🔄  Reloaded từ settings hiện tại.");
        }

        private void BtnOpenFile_Click(object s, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_filePath) && File.Exists(_filePath))
            {
                try { Process.Start(_filePath); }
                catch (Exception ex) { SetErr("✘  " + ex.Message); }
            }
            else { SetWarn("⚠  File chưa được tạo. Hãy Save trước."); }
        }

        private void TxtJson_TextChanged(object s, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_suppress) return;
            UpdateCounter();
        }

        // ── Save / Cancel ─────────────────────────────────────────────────

        private void BtnSave_Click(object s, RoutedEventArgs e)
        {
            ParseResult r = TryParseJson(TxtJson.Text);
            if (!r.Valid) { SetErr("✘  JSON lỗi: " + r.Error); return; }

            string warn = ValidateAppSettings(r.Obj);
            if (warn != null)
            {
                var ans = MessageBox.Show(
                    string.Format("⚠ Cảnh báo: {0}\n\nVẫn lưu?", warn),
                    "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (ans != MessageBoxResult.Yes) return;
            }

            try
            {
                var settings = r.Obj.ToObject<AppSettings>() ?? new AppSettings();
                _saveCallback(settings);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                SetErr("✘  Không thể parse thành AppSettings: " + ex.Message);
            }
        }

        private void BtnCancel_Click(object s, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private void UpdateCounter()
        {
            TxtCounter.Text = string.Format("{0:N0} chars", TxtJson.Text?.Length ?? 0);
        }

        private sealed class ParseResult
        {
            public bool    Valid { get; set; }
            public JObject Obj   { get; set; }
            public string  Error { get; set; }
        }

        private static ParseResult TryParseJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new ParseResult { Valid = false, Error = "JSON trống." };
            try
            {
                return new ParseResult { Valid = true, Obj = JObject.Parse(json) };
            }
            catch (JsonException ex)
            {
                return new ParseResult { Valid = false, Error = ex.Message };
            }
        }

        /// <summary>Returns warning string if validation finds issues, null if OK.</summary>
        private static string ValidateAppSettings(JObject obj)
        {
            var url = obj["GiteaBaseUrl"]?.ToString() ?? string.Empty;
            if (!string.IsNullOrEmpty(url) &&
                !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return "GiteaBaseUrl phải bắt đầu bằng http:// hoặc https://";

            var webhookEnabled = obj["WebhookEnabled"]?.ToObject<bool>() ?? false;
            var webhookUrl     = obj["WebhookUrl"]?.ToString() ?? string.Empty;
            if (webhookEnabled && string.IsNullOrWhiteSpace(webhookUrl))
                return "WebhookUrl không được để trống khi WebhookEnabled = true";

            return null;
        }

        private void SetOk(string m) =>
            Set(m, Color.FromRgb(0xE8, 0xF5, 0xE9), Color.FromRgb(0x4C, 0xAF, 0x50));
        private void SetErr(string m) =>
            Set(m, Color.FromRgb(0xFF, 0xEB, 0xEE), Color.FromRgb(0xF4, 0x43, 0x36));
        private void SetWarn(string m) =>
            Set(m, Color.FromRgb(0xFF, 0xF3, 0xE0), Color.FromRgb(0xFF, 0x98, 0x00));
        private void SetInfo(string m) =>
            Set(m, Color.FromRgb(0xE3, 0xF2, 0xFD), Color.FromRgb(0x21, 0x96, 0xF3));

        private void Set(string msg, Color bg, Color bd)
        {
            TxtStatus.Text             = msg;
            StatusBorder.Background    = new SolidColorBrush(bg);
            StatusBorder.BorderBrush   = new SolidColorBrush(bd);
        }
    }
}
