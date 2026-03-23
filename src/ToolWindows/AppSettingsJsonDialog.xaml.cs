using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using DhCodetaskExtension.Core.Models;
using DhCodetaskExtension.Core.Services;
using DhCodetaskExtension.Providers.TaskProviders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DhCodetaskExtension.ToolWindows
{
    /// <summary>
    /// JSON editor cho AppSettings.
    /// v3.12: Thêm nút "Test Gitea" để kiểm tra kết nối trước khi save.
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
                SetOk("\u2714  Settings loaded.");
            };
        }

        // ── Toolbar ───────────────────────────────────────────────────────

        private void BtnFormat_Click(object s, RoutedEventArgs e)
        {
            ParseResult r = TryParseJson(TxtJson.Text);
            if (r.Valid) { _suppress = true; TxtJson.Text = r.Obj.ToString(Formatting.Indented); _suppress = false; UpdateCounter(); SetOk("\u2714  Formatted."); }
            else SetErr("\u2718  " + r.Error);
        }

        private void BtnValidate_Click(object s, RoutedEventArgs e)
        {
            ParseResult r = TryParseJson(TxtJson.Text);
            if (!r.Valid) { SetErr("\u2718  " + r.Error); return; }
            string err = ValidateAppSettings(r.Obj);
            if (err != null) SetWarn("\u26a0  " + err);
            else             SetOk("\u2714  JSON h\u1ee3p l\u1ec7.");
        }

        private void BtnReset_Click(object s, RoutedEventArgs e)
        {
            if (MessageBox.Show("Reset v\u1ec1 m\u1eb7c \u0111\u1ecbnh?", "X\u00e1c nh\u1eadn",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _suppress = true;
                TxtJson.Text = JsonConvert.SerializeObject(new AppSettings(), Formatting.Indented);
                _suppress = false;
                UpdateCounter();
                SetInfo("\u21a9  Default loaded. B\u1ea5m Save \u0111\u1ec3 \u00e1p d\u1ee5ng.");
            }
        }

        private void BtnReload_Click(object s, RoutedEventArgs e)
        {
            _suppress = true;
            TxtJson.Text = JsonConvert.SerializeObject(_current, Formatting.Indented);
            _suppress = false;
            UpdateCounter();
            SetInfo("\U0001F504  Reloaded t\u1eeb settings hi\u1ec7n t\u1ea1i.");
        }

        private void BtnOpenFile_Click(object s, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_filePath) && File.Exists(_filePath))
            {
                try { Process.Start(_filePath); }
                catch (Exception ex) { SetErr("\u2718  " + ex.Message); }
            }
            else { SetWarn("\u26a0  File ch\u01b0a \u0111\u01b0\u1ee3c t\u1ea1o. H\u00e3y Save tr\u01b0\u1edbc."); }
        }

        /// <summary>
        /// Fix #7: Test Gitea connection using current JSON values (not yet saved).
        /// Parses JSON from editor, creates temp AppSettings, calls TestConnectionAsync.
        /// </summary>
        private async void BtnTestGitea_Click(object s, RoutedEventArgs e)
        {
            ParseResult r = TryParseJson(TxtJson.Text);
            if (!r.Valid) { SetErr("JSON l\u1ed7i: " + r.Error + " — h\u00e3y Format/Validate tr\u01b0\u1edbc."); return; }

            AppSettings tempSettings;
            try { tempSettings = r.Obj.ToObject<AppSettings>() ?? new AppSettings(); }
            catch (Exception ex) { SetErr("Kh\u00f4ng parse \u0111\u01b0\u1ee3c AppSettings: " + ex.Message); return; }

            if (string.IsNullOrWhiteSpace(tempSettings.GiteaBaseUrl))
            {
                SetWarn("\u26a0  GiteaBaseUrl ch\u01b0a \u0111\u01b0\u1ee3c c\u1ea5u h\u00ecnh.");
                return;
            }

            SetInfo("\U0001F50C \u0110ang ki\u1ec3m tra k\u1ebft n\u1ed1i...");
            BtnTestGitea.IsEnabled = false;

            try
            {
                var provider = new GiteaTaskProvider(() => tempSettings);
                var result   = await provider.TestConnectionAsync();
                if (result.Success) SetOk(result.Message);
                else                SetWarn("\u26a0  " + result.Message);
            }
            catch (Exception ex)
            {
                SetErr("L\u1ed7i: " + ex.Message);
            }
            finally
            {
                BtnTestGitea.IsEnabled = true;
            }
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
            if (!r.Valid) { SetErr("\u2718  JSON l\u1ed7i: " + r.Error); return; }

            string warn = ValidateAppSettings(r.Obj);
            if (warn != null)
            {
                var ans = MessageBox.Show(
                    string.Format("\u26a0 C\u1ea3nh b\u00e1o: {0}\n\nV\u1eabn l\u01b0u?", warn),
                    "X\u00e1c nh\u1eadn", MessageBoxButton.YesNo, MessageBoxImage.Warning);
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
                SetErr("\u2718  Kh\u00f4ng th\u1ec3 parse th\u00e0nh AppSettings: " + ex.Message);
            }
        }

        private void BtnCancel_Click(object s, RoutedEventArgs e) { DialogResult = false; Close(); }

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
                return new ParseResult { Valid = false, Error = "JSON tr\u1ed1ng." };
            try { return new ParseResult { Valid = true, Obj = JObject.Parse(json) }; }
            catch (JsonException ex) { return new ParseResult { Valid = false, Error = ex.Message }; }
        }

        private static string ValidateAppSettings(JObject obj)
        {
            var url = obj["GiteaBaseUrl"]?.ToString() ?? string.Empty;
            if (!string.IsNullOrEmpty(url) &&
                !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return "GiteaBaseUrl ph\u1ea3i b\u1eaft \u0111\u1ea7u b\u1eb1ng http:// ho\u1eb7c https://";

            var webhookEnabled = obj["WebhookEnabled"]?.ToObject<bool>() ?? false;
            var webhookUrl     = obj["WebhookUrl"]?.ToString() ?? string.Empty;
            if (webhookEnabled && string.IsNullOrWhiteSpace(webhookUrl))
                return "WebhookUrl kh\u00f4ng \u0111\u01b0\u1ee3c \u0111\u1ec3 tr\u1ed1ng khi WebhookEnabled = true";

            return null;
        }

        private void SetOk(string m)   => Set(m, Color.FromRgb(0xE8,0xF5,0xE9), Color.FromRgb(0x4C,0xAF,0x50));
        private void SetErr(string m)  => Set(m, Color.FromRgb(0xFF,0xEB,0xEE), Color.FromRgb(0xF4,0x43,0x36));
        private void SetWarn(string m) => Set(m, Color.FromRgb(0xFF,0xF3,0xE0), Color.FromRgb(0xFF,0x98,0x00));
        private void SetInfo(string m) => Set(m, Color.FromRgb(0xE3,0xF2,0xFD), Color.FromRgb(0x21,0x96,0xF3));

        private void Set(string msg, Color bg, Color bd)
        {
            TxtStatus.Text           = msg;
            StatusBorder.Background  = new SolidColorBrush(bg);
            StatusBorder.BorderBrush = new SolidColorBrush(bd);
        }
    }
}
