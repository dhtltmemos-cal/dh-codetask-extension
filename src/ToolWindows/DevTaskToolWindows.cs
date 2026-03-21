using System;
using System.Runtime.InteropServices;
using DhCodetaskExtension.ViewModels;
using DhCodetaskExtension.ToolWindows;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;

namespace DhCodetaskExtension.ToolWindows
{
    [Guid(WindowGuidString)]
    public class TrackerToolWindow : ToolWindowPane
    {
        public const string WindowGuidString = "A1B2C3D4-1111-2222-3333-EF0123456789";
        public const string Title = "DevTask Tracker";

        /// <summary>
        /// state = TrackerViewModel, truyền từ InitializeToolWindowAsync.
        /// Được gọi cả khi VS khởi động (window đã docked) lẫn khi ShowToolWindowAsync.
        /// </summary>
        public TrackerToolWindow(object state) : base()
        {
            Caption = Title;
            BitmapImageMoniker = KnownMonikers.Task;

            var vm = state as TrackerViewModel;
            if (vm != null)
                Content = new TrackerControl(vm);
        }
    }

    [Guid(WindowGuidString)]
    public class HistoryToolWindow : ToolWindowPane
    {
        public const string WindowGuidString = "B2C3D4E5-2222-3333-4444-F01234567890";
        public const string Title = "Task History";

        /// <summary>
        /// state = HistoryViewModel, truyền từ InitializeToolWindowAsync.
        /// </summary>
        public HistoryToolWindow(object state) : base()
        {
            Caption = Title;
            BitmapImageMoniker = KnownMonikers.History;

            var vm = state as HistoryViewModel;
            if (vm != null)
                Content = new HistoryControl(vm);
        }
    }
}