using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace DhCodetaskExtension
{
    /// <summary>
    /// Command: Mở file log hôm nay trong editor ngoài.
    /// </summary>
    internal sealed class OpenLogFileCommand
    {
        private readonly DevTaskTrackerPackage _package;
        private OpenLogFileCommand(DevTaskTrackerPackage p) { _package = p; }

        public static async Task InitializeAsync(DevTaskTrackerPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var cs = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (cs == null) return;
            cs.AddCommand(new MenuCommand(
                new OpenLogFileCommand(package).Execute,
                new CommandID(PackageGuids.CommandSetGuid, PackageIds.CmdIdOpenLogFile)));
        }

        private void Execute(object s, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _package.OpenLogFile();
        }
    }

    /// <summary>
    /// Command: Mở file settings.json trong editor ngoài.
    /// </summary>
    internal sealed class OpenConfigFileCommand
    {
        private readonly DevTaskTrackerPackage _package;
        private OpenConfigFileCommand(DevTaskTrackerPackage p) { _package = p; }

        public static async Task InitializeAsync(DevTaskTrackerPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var cs = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (cs == null) return;
            cs.AddCommand(new MenuCommand(
                new OpenConfigFileCommand(package).Execute,
                new CommandID(PackageGuids.CommandSetGuid, PackageIds.CmdIdOpenConfigFile)));
        }

        private void Execute(object s, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _package.OpenConfigFile();
        }
    }
}
