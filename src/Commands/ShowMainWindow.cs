using System;
using System.ComponentModel.Design;
using DhCodetaskExtension.ToolWindows;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace DhCodetaskExtension
{
    /// <summary>
    /// Command mở Main Tool Window.
    /// Đăng ký trong CommandTable.vsct với ID ShowMainWindowId (0x0100).
    /// Hiển thị tại: View > Other Windows > DH Codetask.
    /// </summary>
    internal sealed class ShowMainWindow
    {
        public static async Task InitializeAsync(AsyncPackage package)
        {
            var commandService = (IMenuCommandService)await package.GetServiceAsync(typeof(IMenuCommandService));
            var cmdId = new CommandID(PackageGuids.CommandSetGuid, PackageIds.ShowMainWindowId);
            var cmd   = new MenuCommand((s, e) => Execute(package), cmdId);
            commandService.AddCommand(cmd);
        }

        private static void Execute(AsyncPackage package)
        {
            package.JoinableTaskFactory.RunAsync(async () =>
            {
                await package.ShowToolWindowAsync(
                    typeof(MainToolWindow),
                    0,
                    create: true,
                    cancellationToken: package.DisposalToken);
            });
        }
    }
}
