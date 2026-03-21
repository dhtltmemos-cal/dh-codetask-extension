using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace DhCodetaskExtension
{
    /// <summary>
    /// CmdIdJsonSettings (0x0500): opens AppSettingsJsonDialog — nguồn cấu hình duy nhất.
    /// </summary>
    internal sealed class ShowJsonSettings
    {
        private readonly DevTaskTrackerPackage _package;
        private ShowJsonSettings(DevTaskTrackerPackage p) { _package = p; }

        public static async Task InitializeAsync(DevTaskTrackerPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var cs = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (cs == null) return;
            cs.AddCommand(new MenuCommand(
                new ShowJsonSettings(package).Execute,
                new CommandID(PackageGuids.CommandSetGuid, PackageIds.CmdIdJsonSettings)));
        }

        private void Execute(object s, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _package.OpenSettings();
        }
    }
}
