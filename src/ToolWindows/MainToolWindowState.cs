using DhCodetaskExtension.Services;

namespace DhCodetaskExtension.ToolWindows
{
    /// <summary>
    /// State object truyền từ DhCodetaskPackage.InitializeToolWindowAsync
    /// vào constructor của MainToolWindow. Chứa DTE và tất cả services.
    ///
    /// HOW TO CUSTOMIZE:
    ///   Thêm properties ở đây cho các service mới bạn tạo,
    ///   rồi khởi tạo chúng trong DhCodetaskPackage.InitializeToolWindowAsync.
    /// </summary>
    public class MainToolWindowState
    {
        public EnvDTE80.DTE2        DTE          { get; set; }
        public OutputWindowService  OutputWindow { get; set; }
        public StatusBarService     StatusBar    { get; set; }
        public ConfigurationService Config       { get; set; }
        public JsonConfigService    JsonConfig   { get; set; }
    }
}
