using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace DhCodetaskExtension
{
    /// <summary>
    /// CmdIdSettings (0x0400) has been removed from the menu.
    /// This class is kept only as a stub so any existing references compile.
    /// Settings are now handled exclusively via AppSettingsJsonDialog (CmdIdJsonSettings / ShowTaskSettingsId).
    /// </summary>
    internal sealed class ShowSettings
    {
        private readonly AsyncPackage _package;
        private ShowSettings(AsyncPackage p) { _package = p; }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            // 0x0400 button removed from CommandTable.vsct — nothing to register.
            await Task.CompletedTask;
        }
    }
}
