using System.Collections.Generic;
using Newtonsoft.Json;

namespace DhCodetaskExtension.Core.Models
{
    public class AppSettings
    {
        // ── Gitea ────────────────────────────────────────────
        public string GiteaBaseUrl  { get; set; } = "http://localhost:3000";
        public string GiteaToken    { get; set; } = string.Empty;
        public string GiteaUser     { get; set; } = string.Empty;
        /// <summary>Fix #7: HTTP timeout khi gọi Gitea API (giây). Mặc định 10s.</summary>
        public int    GiteaTimeoutSeconds { get; set; } = 10;

        // ── Git CLI ──────────────────────────────────────
        public bool   GitAutoPush   { get; set; } = false;
        public string GitUserName   { get; set; } = string.Empty;
        public string GitUserEmail  { get; set; } = string.Empty;

        // ── Storage & Report ─────────────────────────────
        public string StoragePath   { get; set; } = string.Empty;
        public string ReportFormat  { get; set; } = "json+markdown";
        public string TimeFormat    { get; set; } = "hh:mm";

        // ── History ────────────────────────────────────────
        public string HistoryDefaultView { get; set; } = "week";

        // ── Webhook ────────────────────────────────────────
        public bool   WebhookEnabled { get; set; } = false;
        public string WebhookUrl     { get; set; } = string.Empty;

        // ── Solution File Scanner ────────────────────────────────
        public string DirectoryRootDhHosCodePath { get; set; } = string.Empty;
        public int    SolutionFileCacheMinutes    { get; set; } = 20;

        // ── Ripgrep + Content Search (v3.13) ─────────────────────────────
        public string RipgrepPath { get; set; } = string.Empty;

        /// <summary>
        /// Extensions duoc phep tim kiem noi dung. Mac dinh: C#, web, config, markdown.
        /// Vi du them: ["*.py", "*.java", "*.sql"]
        /// De trong = dung danh sach mac dinh.
        /// </summary>
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<string> SearchFileExtensions { get; set; } = new List<string>();

        /// <summary>
        /// Thu muc loai tru khi tim kiem noi dung.
        /// Mac dinh da loai: bin, obj, .git, node_modules, packages, .vs, .idea,
        ///                   TestResults, Artifacts, dist, build, .nuget, wwwroot/lib.
        /// Them thu muc rieng o day.
        /// </summary>
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<string> SearchExcludeDirs { get; set; } = new List<string>();

        /// <summary>
        /// Timeout (giay) cho moi lan tim kiem. Mac dinh 45s.
        /// Tang len neu repo rat lon va mang cham.
        /// </summary>
        public int SearchTimeoutSeconds { get; set; } = 45;

        /// <summary>
        /// So ket qua toi da moi lan tim kiem. Mac dinh 200.
        /// </summary>
        public int SearchMaxResults { get; set; } = 200;

        // ── v3.11: Solution File Scan Config ─────────────────────────
        /// <summary>
        /// Pattern bổ sung cần bỏ qua khi quét .sln/.csproj.
        /// Mặc định đã bỏ: node_modules, .git, bin, obj, packages, .vs, .idea, dist, build, .nuget, TestResults, Artifacts.
        /// Thêm pattern riêng ở đây, ví dụ: ["vendor", "third_party"]
        /// </summary>
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<string> ScanExcludePatterns { get; set; } = new List<string>();

        /// <summary>
        /// Giới hạn độ sâu khi quét filesystem (0 = không giới hạn).
        /// Không ảnh hưởng khi dùng ripgrep.
        /// </summary>
        public int ScanMaxDepth { get; set; } = 0;

        // ── v3.10: Default Server IP ────────────────────────────────
        /// <summary>IP server mặc định dùng làm fallback khi body issue không ghi rõ IP.</summary>
        public string DefaultServerIp { get; set; } = string.Empty;

        // ── Task Pause Reasons ───────────────────────────────────
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<string> TaskPauseReasons { get; set; } = new List<string>
        {
            "Hết giờ làm việc",
            "Chuyển việc khác",
            "Lý do khác"
        };

        // ── TODO Templates ────────────────────────────────────────
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<string> TodoTemplates { get; set; } = new List<string>();

        // ── Extensions (freeform key/value) ─────────────────────────
        public Dictionary<string, string> Extensions { get; set; } = new Dictionary<string, string>();
    }
}
