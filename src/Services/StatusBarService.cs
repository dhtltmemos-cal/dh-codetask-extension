using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace DhCodetaskExtension.Services
{
    /// <summary>
    /// Wrapper mỏng quanh IVsStatusbar.
    /// Cho phép caller set text, hiện/ẩn animation progress,
    /// và hiển thị progress bar mà không cần thao tác raw COM.
    /// </summary>
    public sealed class StatusBarService
    {
        private IVsStatusbar _statusBar;
        private readonly AsyncPackage _package;

        public StatusBarService(AsyncPackage package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
        }

        // ------------------------------------------------------------------ //
        //  Initialization                                                      //
        // ------------------------------------------------------------------ //

        public async Task InitializeAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _statusBar =
                await _package.GetServiceAsync(typeof(SVsStatusbar))
                as IVsStatusbar;
        }

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>Hiển thị <paramref name="text"/> trên status bar.</summary>
        public void SetText(string text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            int frozen = 0;
            _statusBar?.IsFrozen(out frozen);
            if (frozen != 0)
                _statusBar?.FreezeOutput(0);
            _statusBar?.SetText(text);
        }

        /// <summary>Hiển thị icon animation với optional label.</summary>
        public void StartAnimation(string text = "Working…")
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            SetText(text);
            object icon = (short)Microsoft.VisualStudio.Shell.Interop.Constants.SBAI_General;
            _statusBar?.Animation(fOnOff: 1, pvIcon: ref icon);
        }

        /// <summary>Dừng animation.</summary>
        public void StopAnimation()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            object icon = (short)Microsoft.VisualStudio.Shell.Interop.Constants.SBAI_General;
            _statusBar?.Animation(fOnOff: 0, pvIcon: ref icon);
        }

        /// <summary>Hiển thị progress bar trên status bar.</summary>
        public void ReportProgress(ref uint cookie, string label, uint current, uint total)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _statusBar?.Progress(ref cookie, fInProgress: 1,
                pwszLabel: label, nComplete: current, nTotal: total);
        }

        /// <summary>Xóa/ẩn progress bar.</summary>
        public void ClearProgress(ref uint cookie)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _statusBar?.Progress(ref cookie, fInProgress: 0,
                pwszLabel: string.Empty, nComplete: 0, nTotal: 0);
        }

        /// <summary>
        /// Chạy <paramref name="work"/> trên background thread trong khi
        /// hiện animation, rồi xóa animation trên UI thread sau khi xong.
        /// </summary>
        public async Task RunWithAnimationAsync(Func<Task> work, string statusText = "Working…")
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            StartAnimation(statusText);
            try
            {
                await System.Threading.Tasks.Task.Run(work);
            }
            finally
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                StopAnimation();
                SetText("Ready");
            }
        }
    }
}
