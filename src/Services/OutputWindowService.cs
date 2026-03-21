using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace DhCodetaskExtension.Services
{
    /// <summary>
    /// Quản lý Output Window pane riêng cho extension này.
    /// Bọc IVsOutputWindow / IVsOutputWindowPane để caller không cần
    /// làm việc trực tiếp với COM interface.
    ///
    /// HOW TO CUSTOMIZE:
    ///   - Thay PaneGuid bằng GUID mới (phải unique cho mỗi extension)
    ///   - Thay PaneTitle bằng tên extension của bạn
    /// </summary>
    public sealed class OutputWindowService
    {
        // TODO: Generate a new GUID for your extension's output pane
        public static readonly Guid PaneGuid =
            new Guid("A1B2C3D4-E5F6-7890-ABCD-EF0123456789");

        private const string PaneTitle = "DH Codetask Extension";

        private IVsOutputWindowPane _pane;
        private readonly AsyncPackage _package;

        public OutputWindowService(AsyncPackage package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
        }

        // ------------------------------------------------------------------ //
        //  Initialization                                                      //
        // ------------------------------------------------------------------ //

        public async Task InitializeAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var outputWindow =
                await _package.GetServiceAsync(typeof(SVsOutputWindow))
                as IVsOutputWindow;

            if (outputWindow == null) return;

            var guid = PaneGuid;
            outputWindow.GetPane(ref guid, out _pane);

            if (_pane == null)
            {
                outputWindow.CreatePane(ref guid, PaneTitle,
                    fInitVisible: 1, fClearWithSolution: 0);
                outputWindow.GetPane(ref guid, out _pane);
            }
        }

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>Ghi một dòng text (tự động thêm \n).</summary>
        public void WriteLine(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _pane?.OutputString(message + "\n");
        }

        /// <summary>Ghi một dòng có timestamp — dùng cho log.</summary>
        public void Log(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _pane?.OutputString($"[{DateTime.Now:HH:mm:ss}] {message}\n");
        }

        /// <summary>Đưa pane này lên foreground trong Output window.</summary>
        public void Activate()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _pane?.Activate();
        }

        /// <summary>Xóa toàn bộ text trong pane.</summary>
        public void Clear()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _pane?.Clear();
        }
    }
}
