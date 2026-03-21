using System;
using System.ComponentModel;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace DhCodetaskExtension.Services
{
    // ====================================================================== //
    //  Options Page                                                           //
    // ====================================================================== //

    /// <summary>
    /// Hiển thị tại Tools › Options › DH Codetask Extension › General.
    /// Properties được tự động persist vào VS registry bởi DialogPage.
    ///
    /// HOW TO CUSTOMIZE:
    ///   - Thêm/xóa/đổi tên properties
    ///   - Dùng [Category], [DisplayName], [Description] để nhóm UI
    ///   - Cập nhật ProvideOptionPage attribute trên DhCodetaskPackage
    /// </summary>
    public class SampleOptionsPage : DialogPage
    {
        [Category("Connection")]
        [DisplayName("Server URL")]
        [Description("URL của backend server được dùng bởi extension này.")]
        public string ServerUrl { get; set; } = "https://api.example.com";

        [Category("Behavior")]
        [DisplayName("Auto Format on Save")]
        [Description("Tự động format document đang active khi save.")]
        public bool AutoFormat { get; set; } = false;

        [Category("Behavior")]
        [DisplayName("Max Log Items")]
        [Description("Số lượng tối đa items hiển thị trong Output pane log.")]
        public int MaxLogItems { get; set; } = 200;

        [Category("Behavior")]
        [DisplayName("Enable Debug Logging")]
        [Description("Log thông tin debug chi tiết vào Output pane.")]
        public bool EnableDebugLog { get; set; } = false;
    }

    // ====================================================================== //
    //  Service wrapper                                                        //
    // ====================================================================== //

    /// <summary>
    /// Wrapper mỏng quanh <see cref="SampleOptionsPage"/>.
    /// Cung cấp typed access đến settings và helper để mở Options dialog.
    /// Tất cả public methods phải được gọi trên UI thread.
    /// </summary>
    public sealed class OptionsService
    {
        private readonly AsyncPackage     _package;
        private readonly IServiceProvider _serviceProvider;

        public OptionsService(AsyncPackage package)
        {
            _package         = package ?? throw new ArgumentNullException(nameof(package));
            _serviceProvider = package;
        }

        public SampleOptionsPage GetPage()
            => (SampleOptionsPage)_package.GetDialogPage(typeof(SampleOptionsPage));

        // Typed accessors
        public string ServerUrl      => GetPage().ServerUrl;
        public bool   AutoFormat     => GetPage().AutoFormat;
        public int    MaxLogItems    => GetPage().MaxLogItems;
        public bool   EnableDebugLog => GetPage().EnableDebugLog;

        /// <summary>Mở Tools > Options dialog tại trang settings của extension.</summary>
        public void OpenOptionsDialog()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = _serviceProvider.GetService(typeof(DTE)) as DTE2;
            dte?.ExecuteCommand("Tools.Options", "DH Codetask Extension.General");
        }
    }
}
