using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;

namespace DhCodetaskExtension.ToolWindows
{
    /// <summary>
    /// Tool window chính của extension (dockable).
    /// Mở qua: View > Other Windows > DH Codetask
    ///
    /// HOW TO CUSTOMIZE:
    ///   - Thay WindowGuidString bằng GUID mới tạo
    ///   - Thay Title bằng tên window của bạn
    ///   - Thay BitmapImageMoniker bằng icon KnownMonikers bạn muốn
    /// </summary>
    [Guid(WindowGuidString)]
    public class MainToolWindow : ToolWindowPane
    {
        // TODO: Generate a new GUID for your tool window
        public const string WindowGuidString = "F1A2B3C4-D5E6-7890-ABCD-EF0123456789";
        public const string Title            = "DH Codetask";

        /// <summary>
        /// Tham số <paramref name="state"/> là object trả về từ DhCodetaskPackage.InitializeToolWindowAsync.
        /// </summary>
        public MainToolWindow(MainToolWindowState state) : base()
        {
            Caption            = Title;
            BitmapImageMoniker = KnownMonikers.Extension; // TODO: Chọn icon của bạn
            Content            = new MainToolWindowControl(state);
        }
    }
}
