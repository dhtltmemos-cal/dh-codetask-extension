using System;
using System.Windows;
using System.Windows.Controls;
using DhCodetaskExtension.Services;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace DhCodetaskExtension.ToolWindows
{
    /// <summary>
    /// Code-behind cho MainToolWindowControl.xaml.
    /// Tất cả button click handlers nằm ở đây.
    ///
    /// HOW TO CUSTOMIZE:
    ///   - Thêm button handlers mới theo pattern có sẵn
    ///   - Truy cập services qua các private properties bên dưới
    ///   - Tất cả UI handlers chạy trên UI thread; dùng ThreadHelper.ThrowIfNotOnUIThread() để guard
    /// </summary>
    public partial class MainToolWindowControl : UserControl
    {
        private readonly MainToolWindowState _state;

        // Service shortcuts
        private OutputWindowService  OutputWindow => _state.OutputWindow;
        private StatusBarService     StatusBar    => _state.StatusBar;
        private ConfigurationService Config       => _state.Config;
        private JsonConfigService    JsonConfig   => _state.JsonConfig;

        public MainToolWindowControl(MainToolWindowState state)
        {
            _state = state;
            InitializeComponent();
        }

        // ================================================================== //
        //  Output Window                                                       //
        // ================================================================== //

        private void Button_WriteOutput_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            OutputWindow.Activate();
            OutputWindow.Log("Hello from DH Codetask Extension!");
            OutputWindow.WriteLine("You can write arbitrary text here.");
            StatusBar.SetText("Written to Output Window.");
        }

        private void Button_ClearOutput_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            OutputWindow.Clear();
            OutputWindow.Log("Output pane cleared.");
            StatusBar.SetText("Output pane cleared.");
        }

        // ================================================================== //
        //  Status Bar                                                          //
        // ================================================================== //

        private void Button_SetStatus_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            StatusBar.SetText($"Hello from DH Codetask Extension – {DateTime.Now:T}");
            OutputWindow.Log("Status bar text updated.");
        }

        private void Button_Animate_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                OutputWindow.Log("Starting 3-second animation…");
                await StatusBar.RunWithAnimationAsync(
                    async () => await Task.Delay(3000), "Processing… please wait");
                OutputWindow.Log("Animation finished.");
            });
        }

        private void Button_Progress_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                OutputWindow.Log("Starting progress bar demo…");
                uint cookie = 0;
                const uint total = 5;
                for (uint i = 1; i <= total; i++)
                {
                    StatusBar.ReportProgress(ref cookie, "Demo progress", i, total);
                    OutputWindow.Log($"  Step {i}/{total}");
                    await Task.Delay(500);
                }
                StatusBar.ClearProgress(ref cookie);
                StatusBar.SetText("Progress complete.");
                OutputWindow.Log("Progress bar demo finished.");
            });
        }

        // ================================================================== //
        //  Configuration (XML)                                                 //
        // ================================================================== //

        private void Button_ConfigDump_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (Config == null) { OutputWindow.Log("[Config] Not initialized."); return; }
            OutputWindow.Activate();
            Config.DumpToOutput();
            StatusBar.SetText("Config: logged to Output.");
        }

        private void Button_ConfigTest_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (Config == null) { OutputWindow.Log("[Config] Not initialized."); return; }
            OutputWindow.Activate();
            OutputWindow.Log("[Config] ── Typed getters ──");
            OutputWindow.Log($"[Config]  ServerUrl      = \"{Config.ServerUrl}\"");
            OutputWindow.Log($"[Config]  MaxResults     = {Config.MaxResults}");
            OutputWindow.Log($"[Config]  TimeoutSeconds = {Config.TimeoutSeconds}");
            OutputWindow.Log($"[Config]  EnableLogging  = {Config.EnableLogging}");
            OutputWindow.Log($"[Config]  OutputFormat   = \"{Config.OutputFormat}\"");
            OutputWindow.Log($"[Config]  DebugMode      = {Config.DebugMode}");
            StatusBar.SetText("Config: getters tested.");
        }

        private void Button_ConfigReset_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (Config == null) { OutputWindow.Log("[Config] Not initialized."); return; }
            OutputWindow.Activate();
            Config.ResetToDefaults(log: true);
            OutputWindow.Log("[Config] Reset to defaults.");
            StatusBar.SetText("Config: reset to defaults.");
        }

        private void Button_OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (Config == null || StatusBar == null) return;
            var dlg = new SettingsDialog(Config, OutputWindow, StatusBar);
            dlg.ShowDialog();
        }

        // ================================================================== //
        //  Configuration (JSON)                                                //
        // ================================================================== //

        private void Button_OpenJsonSettings_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (JsonConfig == null || StatusBar == null) return;
            var dlg = new JsonSettingsDialog(JsonConfig, OutputWindow, StatusBar);
            dlg.ShowDialog();
        }

        private void Button_JsonConfigTest_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (JsonConfig == null) { OutputWindow.Log("[JsonConfig] Not initialized."); return; }
            OutputWindow.Activate();
            OutputWindow.Log("[JsonConfig] ── Typed getters ──");
            OutputWindow.Log($"[JsonConfig]  ServerUrl      = \"{JsonConfig.ServerUrl}\"");
            OutputWindow.Log($"[JsonConfig]  MaxResults     = {JsonConfig.MaxResults}");
            OutputWindow.Log($"[JsonConfig]  TimeoutSeconds = {JsonConfig.TimeoutSeconds}");
            OutputWindow.Log($"[JsonConfig]  EnableLogging  = {JsonConfig.EnableLogging}");
            OutputWindow.Log($"[JsonConfig]  DebugMode      = {JsonConfig.DebugMode}");
            int retry = JsonConfig.Get<int>("Advanced.RetryCount", 3);
            OutputWindow.Log($"[JsonConfig]  Advanced.RetryCount = {retry}");
            StatusBar.SetText("JsonConfig: getters tested.");
        }

        private void Button_JsonConfigReset_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (JsonConfig == null) { OutputWindow.Log("[JsonConfig] Not initialized."); return; }
            OutputWindow.Activate();
            var result = JsonConfig.ResetToDefault();
            string msg = result.Success ? "[JsonConfig] Reset to default." : $"[JsonConfig] Reset failed: {result.Error}";
            OutputWindow.Log(msg);
            StatusBar.SetText(msg);
        }

        // ================================================================== //
        //  TODO: Thêm button handlers của bạn bên dưới                        //
        // ================================================================== //

        // Ví dụ pattern cho handler mới:
        //
        // private void Button_MyFeature_Click(object sender, RoutedEventArgs e)
        // {
        //     ThreadHelper.ThrowIfNotOnUIThread();
        //     // Your code here
        //     OutputWindow.Log("[MyFeature] Done.");
        //     StatusBar.SetText("MyFeature completed.");
        // }
    }
}
