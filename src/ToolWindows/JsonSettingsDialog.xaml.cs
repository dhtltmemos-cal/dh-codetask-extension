using System;
using System.Windows;
using System.Windows.Media;
using DhCodetaskExtension.Services;
using Microsoft.VisualStudio.Shell;

namespace DhCodetaskExtension.ToolWindows
{
    /// <summary>
    /// JSON Settings dialog — full-width editor với Format, Validate, Save.
    /// Mở từ: Tools > DH Codetask Extension Settings (JSON)...
    /// </summary>
    public partial class JsonSettingsDialog : Window
    {
        private static readonly Color ColorOk      = Color.FromRgb(0xE8, 0xF5, 0xE9);
        private static readonly Color ColorOkBdr   = Color.FromRgb(0x4C, 0xAF, 0x50);
        private static readonly Color ColorErr     = Color.FromRgb(0xFF, 0xF3, 0xE0);
        private static readonly Color ColorErrBdr  = Color.FromRgb(0xFF, 0x98, 0x00);
        private static readonly Color ColorInfo    = Color.FromRgb(0xE3, 0xF2, 0xFD);
        private static readonly Color ColorInfoBdr = Color.FromRgb(0x21, 0x96, 0xF3);

        private readonly JsonConfigService   _jsonConfig;
        private readonly OutputWindowService _outputWindow;
        private readonly StatusBarService    _statusBar;
        private bool _suppressTextChanged;

        public JsonSettingsDialog(
            JsonConfigService   jsonConfig,
            OutputWindowService outputWindow,
            StatusBarService    statusBar)
        {
            _jsonConfig   = jsonConfig   ?? throw new ArgumentNullException(nameof(jsonConfig));
            _outputWindow = outputWindow ?? throw new ArgumentNullException(nameof(outputWindow));
            _statusBar    = statusBar    ?? throw new ArgumentNullException(nameof(statusBar));
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            LoadJsonIntoEditor(_jsonConfig.GetCurrentJson());
            TxtFilePath.Text = _jsonConfig.ConfigFilePath;
            SetStatusOk("✔  JSON loaded. Ready to edit.");
        }

        private void LoadJsonIntoEditor(string json)
        {
            _suppressTextChanged = true;
            TxtJson.Text = json;
            _suppressTextChanged = false;
            UpdateCounter();
        }

        private void BtnFormat_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            JsonFormatResult result = _jsonConfig.FormatJson(TxtJson.Text);
            if (!result.IsOk) { SetStatusError("✘  Cannot format: " + result.Error); return; }
            int caretLine = TxtJson.GetLineIndexFromCharacterIndex(TxtJson.CaretIndex);
            LoadJsonIntoEditor(result.Result);
            try
            {
                int targetLine = Math.Min(caretLine, TxtJson.LineCount - 1);
                int newIdx = TxtJson.GetCharacterIndexFromLineIndex(targetLine);
                TxtJson.CaretIndex = newIdx;
                TxtJson.ScrollToLine(targetLine);
            }
            catch { }
            SetStatusOk("✔  JSON formatted.");
            _outputWindow.Log("[JsonConfig] JSON formatted.");
        }

        private void BtnValidate_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            JsonParseResult result = _jsonConfig.TryParse(TxtJson.Text);
            if (result.Valid)
            {
                SetStatusOk("✔  JSON is valid.");
                _outputWindow.Log("[JsonConfig] Validation: OK.");
            }
            else
            {
                SetStatusError("✘  " + result.Error);
                _outputWindow.Log("[JsonConfig] Validation FAILED: " + result.Error);
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            MessageBoxResult answer = MessageBox.Show(
                "Reset cấu hình về mặc định?\nJSON hiện tại sẽ bị ghi đè.",
                "Confirm Reset", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;
            LoadJsonIntoEditor(JsonConfigService.DefaultJson);
            SetStatusInfo("↩  Default JSON loaded. Bấm Save để lưu.");
            _outputWindow.Log("[JsonConfig] Default JSON loaded into editor.");
        }

        private void BtnReload_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            LoadJsonIntoEditor(_jsonConfig.GetCurrentJson());
            SetStatusInfo("🔄  Reloaded from file. Thay đổi chưa save bị hủy.");
            _outputWindow.Log("[JsonConfig] Editor reloaded from file.");
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try { Clipboard.SetText(TxtJson.Text); SetStatusInfo("📋  JSON copied to clipboard."); }
            catch (Exception ex) { SetStatusError("✘  Copy failed: " + ex.Message); }
        }

        private void BtnPaste_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                string clip = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(clip)) { SetStatusError("✘  Clipboard is empty."); return; }
                JsonFormatResult fmt = _jsonConfig.FormatJson(clip);
                LoadJsonIntoEditor(fmt.IsOk ? fmt.Result : clip);
                if (fmt.IsOk) SetStatusOk("✔  Pasted and formatted from clipboard.");
                else SetStatusInfo("📌  Pasted from clipboard (not valid JSON yet).");
                _outputWindow.Log("[JsonConfig] Pasted from clipboard.");
            }
            catch (Exception ex) { SetStatusError("✘  Paste failed: " + ex.Message); }
        }

        private void TxtJson_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_suppressTextChanged) return;
            UpdateCounter();
            string text = TxtJson.Text;
            if (string.IsNullOrWhiteSpace(text)) { SetStatusError("✘  Editor is empty."); return; }
            int open = 0, close = 0;
            foreach (char c in text) { if (c == '{') open++; else if (c == '}') close++; }
            if (open != close)
                SetStatusInfo($"⚠  Braces mismatch: {open} {{ vs {close} }}. Keep typing…");
            else
                SetStatusOk("✔  Looks good. Press Validate or Save to confirm.");
        }

        private void UpdateCounter()
        {
            string text = TxtJson.Text ?? "";
            int lines   = TxtJson.LineCount > 0 ? TxtJson.LineCount : 1;
            TxtCounter.Text = text.Length.ToString("N0") + " chars · " + lines + " lines";
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _outputWindow.Activate();
            JsonSaveResult result = _jsonConfig.Save(TxtJson.Text);
            if (!result.Success) { SetStatusError("✘  " + result.Error); _outputWindow.Log("[JsonConfig] Save FAILED: " + result.Error); return; }
            string summary = result.ChangedKeys.Count == 0
                ? "No changes."
                : result.ChangedKeys.Count + " key(s) changed: " + string.Join(", ", result.ChangedKeys);
            SetStatusOk("✔  Saved. " + summary);
            _statusBar.SetText("JsonConfig saved. " + summary);
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SetStatusOk(string message)    => SetStatus(message, ColorOk,   ColorOkBdr);
        private void SetStatusError(string message) => SetStatus(message, ColorErr,  ColorErrBdr);
        private void SetStatusInfo(string message)  => SetStatus(message, ColorInfo, ColorInfoBdr);

        private void SetStatus(string message, Color bg, Color border)
        {
            TxtStatus.Text           = message;
            StatusBorder.Background  = new SolidColorBrush(bg);
            StatusBorder.BorderBrush = new SolidColorBrush(border);
        }
    }
}
