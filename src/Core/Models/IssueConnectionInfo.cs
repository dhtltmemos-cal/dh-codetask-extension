using System;

namespace DhCodetaskExtension.Core.Models
{
    /// <summary>
    /// Thông tin kết nối được trích xuất từ body của issue.
    /// Lưu cùng TaskItem để persist vào JSON.
    /// </summary>
    public class IssueConnectionInfo
    {
        /// <summary>IP hoặc hostname server cơ sở dữ liệu.</summary>
        public string IpServer  { get; set; } = string.Empty;

        /// <summary>Tên database / CSDL.</summary>
        public string Database  { get; set; } = string.Empty;

        /// <summary>Port kết nối (chuỗi số).</summary>
        public string Port      { get; set; } = string.Empty;

        /// <summary>Trả về true nếu có ít nhất 1 thông tin kết nối.</summary>
        public bool HasAny =>
            !string.IsNullOrEmpty(IpServer) ||
            !string.IsNullOrEmpty(Database) ||
            !string.IsNullOrEmpty(Port);

        /// <summary>Connection string ngắn gọn dạng "host:port/db" để display.</summary>
        public string Summary
        {
            get
            {
                if (!HasAny) return string.Empty;
                var parts = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrEmpty(IpServer)) parts.Add(IpServer);
                if (!string.IsNullOrEmpty(Port))     parts.Add(string.Format(":{0}", Port));
                if (!string.IsNullOrEmpty(Database)) parts.Add(string.Format("/{0}", Database));
                return string.Join(string.Empty, parts);
            }
        }
    }
}
