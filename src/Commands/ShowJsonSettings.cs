using System;
using System.ComponentModel.Design;
using DhCodetaskExtension.ToolWindows;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace DhCodetaskExtension
{
    /// <summary>
    /// Command mở JSON Settings dialog từ Tools menu.
    /// Đăng ký trong CommandTable.vsct với ID CmdIdJsonSettings (0x0500).
    /// Hiển thị tại: Tools > DH Codetask Extension Settings (JSON)...
    /// </summary>
    internal sealed class ShowJsonSettings
    {
        private readonly AsyncPackage _package;

        private ShowJsonSettings(AsyncPackage package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var cs = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (cs == null) return;
            var cmdId = new CommandID(PackageGuids.CommandSetGuid, PackageIds.CmdIdJsonSettings);
            var cmd   = new MenuCommand(new ShowJsonSettings(package).Execute, cmdId);
            cs.AddCommand(cmd);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var pkg = (DhCodetaskPackage)_package;
            var dlg = new JsonSettingsDialog(pkg.JsonConfig, pkg.OutputWindow, pkg.StatusBar);
            dlg.ShowDialog();
        }
    }
}
