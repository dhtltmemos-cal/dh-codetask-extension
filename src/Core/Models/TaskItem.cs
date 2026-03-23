namespace DhCodetaskExtension.Core.Models
{
    public class TaskItem
    {
        public string Id          { get; set; } = string.Empty;
        public string Title       { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string[] Labels    { get; set; } = new string[0];
        public string Url         { get; set; } = string.Empty;
        public string Owner       { get; set; } = string.Empty;
        public string Repo        { get; set; } = string.Empty;

        // ── v3.10: Thông tin từ Gitea issue ──────────────────────────────
        /// <summary>Username tạo issue.</summary>
        public string CreatedByUser  { get; set; } = string.Empty;

        /// <summary>Ngày tạo issue (ISO string để dễ serialize).</summary>
        public string CreatedAt      { get; set; } = string.Empty;

        // ── v3.10: Thông tin kết nối trích xuất từ body ──────────────────
        /// <summary>Thông tin IP/DB/Port trích xuất từ body issue.</summary>
        public IssueConnectionInfo ConnectionInfo { get; set; } = new IssueConnectionInfo();
    }
}
