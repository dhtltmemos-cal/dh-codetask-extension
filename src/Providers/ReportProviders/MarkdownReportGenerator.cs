using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DhCodetaskExtension.Core.Interfaces;
using DhCodetaskExtension.Core.Models;
using DhCodetaskExtension.Core.Services;

namespace DhCodetaskExtension.Providers.ReportProviders
{
    public sealed class MarkdownReportGenerator : IReportGenerator
    {
        public async Task<string> GenerateAsync(CompletionReport report, string outputDirectory)
        {
            var year  = report.CompletedAt.Year.ToString("D4");
            var month = report.CompletedAt.Month.ToString("D2");
            var dir   = Path.Combine(outputDirectory, year, month);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string slug  = Slugify(report.TaskTitle);
            string stamp = report.CompletedAt.ToString("yyyyMMdd_HHmmss");
            string path  = Path.Combine(dir, string.Format("{0}_{1}_{2}.md", stamp, report.TaskId, slug));

            var md = BuildMarkdown(report);
            await AtomicFile.WriteAllTextAsync(path, md);
            return path;
        }

        private static string BuildMarkdown(CompletionReport r)
        {
            var sb = new StringBuilder();
            bool paused  = !r.WasPushed && r.CompletedAt != default(DateTime);
            string status = paused ? "⏸ TẠM NGƯNG" : "✅ HOÀN THÀNH";

            sb.AppendLine(string.Format("# {0} — #{1}: {2}", status, r.TaskId, r.TaskTitle));
            sb.AppendLine();
            if (!string.IsNullOrEmpty(r.TaskUrl))
                sb.AppendLine(string.Format("> **Repo/URL:** {0}", r.TaskUrl));
            if (r.Labels?.Length > 0)
                sb.AppendLine(string.Format("> **Labels:** {0}", string.Join(", ", r.Labels)));
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            // Time section
            sb.AppendLine("## ⏱ Thời gian");
            sb.AppendLine();
            sb.AppendLine("| Mốc | Thời điểm |");
            sb.AppendLine("| --- | --------- |");
            sb.AppendLine(string.Format("| 🟢 Bắt đầu | {0:yyyy-MM-dd HH:mm:ss} |",
                r.StartedAt.ToLocalTime()));
            sb.AppendLine(string.Format("| 🔴 Kết thúc | {0:yyyy-MM-dd HH:mm:ss} |",
                r.CompletedAt.ToLocalTime()));
            sb.AppendLine(string.Format("| ⏳ Tổng làm việc | {0} |", FormatSpan(r.TotalElapsed)));
            sb.AppendLine();

            if (r.Sessions?.Count > 0)
            {
                sb.AppendLine("### Chi tiết các phiên làm việc");
                sb.AppendLine();
                sb.AppendLine("| # | Bắt đầu | Kết thúc | Thời lượng | Lý do ngưng |");
                sb.AppendLine("| - | ------- | -------- | ---------- | ----------- |");
                int idx = 1;
                foreach (var s in r.Sessions)
                {
                    var end = s.EndTime.HasValue
                        ? s.EndTime.Value.ToLocalTime().ToString("HH:mm:ss")
                        : "đang chạy";
                    var reason = string.IsNullOrEmpty(s.PauseReason) ? "—" : s.PauseReason;
                    sb.AppendLine(string.Format("| {0} | {1:HH:mm:ss} | {2} | {3} | {4} |",
                        idx++, s.StartTime.ToLocalTime(), end, FormatSpan(s.Duration), reason));
                }
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## 📝 Ghi chú thực hiện");
            sb.AppendLine();
            sb.AppendLine(string.IsNullOrWhiteSpace(r.WorkNotes) ? "_Không có ghi chú._" : r.WorkNotes);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## 💼 Mô tả nghiệp vụ");
            sb.AppendLine();
            sb.AppendLine(string.IsNullOrWhiteSpace(r.BusinessLogic) ? "_Không có mô tả._" : r.BusinessLogic);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## ✅ Danh sách TODO");
            sb.AppendLine();
            if (r.Todos?.Count > 0)
            {
                sb.AppendLine("| # | Nội dung | Trạng thái | Thời gian làm |");
                sb.AppendLine("| - | -------- | ---------- | ------------- |");
                int i = 1;
                foreach (var t in r.Todos)
                {
                    string done    = t.IsDone ? "✅ Xong" : "⬜ Chưa xong";
                    string elapsed = t.TotalElapsed.TotalSeconds > 0 ? FormatSpan(t.TotalElapsed) : "—";
                    sb.AppendLine(string.Format("| {0} | {1} | {2} | {3} |", i++, t.Text, done, elapsed));
                }
                sb.AppendLine();
                sb.AppendLine(string.Format("**Tổng TODO:** {0} | **Hoàn thành:** {1}/{0} | **Tổng giờ TODO:** {2}",
                    r.TodoTotal, r.TodoDone, FormatSpan(r.TotalTodoElapsed)));
            }
            else
            {
                sb.AppendLine("_Không có TODO item._");
            }
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## 🔀 Commit");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(r.CommitMessage ?? string.Empty);
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine(string.Format("**Branch:** {0}  ", r.GitBranch ?? "—"));
            string hashLine   = string.IsNullOrEmpty(r.GitCommitHash) ? "— chưa push" : r.GitCommitHash;
            string pushedIcon = r.WasPushed ? "✅ Pushed" : "⬜ Not pushed";
            sb.AppendLine(string.Format("**Commit Hash:** {0} | {1}", hashLine, pushedIcon));
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            // Checksum section (v3.3)
            if (!string.IsNullOrEmpty(r.Checksum))
            {
                sb.AppendLine("## 🔒 Toàn vẹn dữ liệu");
                sb.AppendLine();
                sb.AppendLine(string.Format("**SHA-256:** `{0}`", r.Checksum));
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
            }

            sb.AppendLine(string.Format("_Sinh tự động bởi DevTaskTracker — {0:yyyy-MM-dd HH:mm:ss}_",
                r.CreatedAt.ToLocalTime()));

            return sb.ToString();
        }

        private static string FormatSpan(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return string.Format("{0}h {1}m {2}s", (int)ts.TotalHours, ts.Minutes, ts.Seconds);
            if (ts.TotalMinutes >= 1)
                return string.Format("{0}m {1}s", ts.Minutes, ts.Seconds);
            return string.Format("{0}s", ts.Seconds);
        }

        private static string Slugify(string s)
        {
            if (string.IsNullOrEmpty(s)) return "task";
            s = s.ToLower().Trim();
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (c == ' ' || c == '-') sb.Append('-');
            }
            return sb.ToString().Trim('-');
        }
    }
}
