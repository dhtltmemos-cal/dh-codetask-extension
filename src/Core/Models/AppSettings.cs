using System.Collections.Generic;

namespace DhCodetaskExtension.Core.Models
{
    public class AppSettings
    {
        // ── Gitea ────────────────────────────────────────────────────────
        public string GiteaBaseUrl  { get; set; } = "http://localhost:3000";
        public string GiteaToken    { get; set; } = string.Empty;
        public string GiteaUser     { get; set; } = string.Empty;

        // ── Git CLI ──────────────────────────────────────────────────────
        public bool   GitAutoPush   { get; set; } = false;
        public string GitUserName   { get; set; } = string.Empty;
        public string GitUserEmail  { get; set; } = string.Empty;

        // ── Storage & Report ─────────────────────────────────────────────
        public string StoragePath   { get; set; } = string.Empty;
        public string ReportFormat  { get; set; } = "json+markdown";
        public string TimeFormat    { get; set; } = "hh:mm";

        // ── History ──────────────────────────────────────────────────────
        public string HistoryDefaultView { get; set; } = "week";

        // ── Webhook ──────────────────────────────────────────────────────
        public bool   WebhookEnabled { get; set; } = false;
        public string WebhookUrl     { get; set; } = string.Empty;

        // ── Solution File Scanner (v3.3) ─────────────────────────────────
        /// <summary>Root directory to scan for .sln and .csproj files.</summary>
        public string DirectoryRootDhHosCodePath { get; set; } = string.Empty;

        /// <summary>Cache expiry for solution file list (minutes). Default 20.</summary>
        public int SolutionFileCacheMinutes { get; set; } = 20;

        // ── Task Pause Reasons (v3.3) ────────────────────────────────────
        /// <summary>Predefined pause reasons shown in the pause dialog.</summary>
        public List<string> TaskPauseReasons { get; set; } = new List<string>
        {
            "Hết giờ làm việc",
            "Chuyển việc khác",
            "Lý do khác"
        };

        // ── TODO Templates (v3.3) ────────────────────────────────────────
        /// <summary>Predefined TODO items that can be quickly added to a task.</summary>
        public List<string> TodoTemplates { get; set; } = new List<string>();

        // ── Extensions (freeform key/value) ──────────────────────────────
        public Dictionary<string, string> Extensions { get; set; } = new Dictionary<string, string>();
    }
}
