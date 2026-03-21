using System;
using System.ComponentModel.Design;
using DhCodetaskExtension.ToolWindows;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace DhCodetaskExtension
{
    /// <summary>
    /// Command mở Settings dialog (XML) từ Tools menu.
    /// Đăng ký trong CommandTable.vsct với ID CmdIdSettings (0x0400).
    /// Hiển thị tại: Tools > DH Codetask Extension Settings...
    /// </summary>
    internal sealed class ShowSettings
    {
        private readonly AsyncPackage _package;

        private ShowSettings(AsyncPackage package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var cs = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (cs == null) return;
            var cmdId = new CommandID(PackageGuids.CommandSetGuid, PackageIds.CmdIdSettings);
            var cmd   = new MenuCommand(new ShowSettings(package).Execute, cmdId);
            cs.AddCommand(cmd);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var pkg = (DhCodetaskPackage)_package;
            var dlg = new SettingsDialog(pkg.Config, pkg.OutputWindow, pkg.StatusBar);
            dlg.ShowDialog();
        }
    }
}
