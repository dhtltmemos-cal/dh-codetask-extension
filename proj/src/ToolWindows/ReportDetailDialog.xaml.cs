using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using DhCodetaskExtension.Core.Models;
using DhCodetaskExtension.ViewModels;

namespace DhCodetaskExtension.ToolWindows
{
    public partial class ReportDetailDialog : Window
    {
        private readonly CompletionReportSummary _summary;

        public ReportDetailDialog(CompletionReportSummary summary)
        {
            _summary = summary ?? throw new ArgumentNullException(nameof(summary));
            InitializeComponent();
            Populate();
        }

        private void Populate()
        {
            var r = _summary.FullReport;
            if (r == null) { Title = "Report không tồn tại"; return; }

            Title = string.Format("📋 Chi tiết: #{0}: {1}", r.TaskId, r.TaskTitle);
            TxtTitle.Text    = string.Format("#{0}: {1}", r.TaskId, r.TaskTitle);
            TxtStarted.Text  = string.Format("🟢 Bắt đầu: {0:yyyy-MM-dd HH:mm:ss}", r.StartedAt.ToLocalTime());
            TxtEnded.Text    = string.Format("🔴 Kết thúc: {0:yyyy-MM-dd HH:mm:ss}", r.CompletedAt.ToLocalTime());
            TxtElapsed.Text  = string.Format("⏳ Thời gian: {0}", FormatSpanFull(r.TotalElapsed));
            TxtNotes.Text    = r.WorkNotes;
            TxtBiz.Text      = r.BusinessLogic;
            TxtCommit.Text   = r.CommitMessage;
            TxtGit.Text      = string.Format("Branch: {0}  |  Hash: {1}  |  {2}",
                r.GitBranch,
                string.IsNullOrEmpty(r.GitCommitHash) ? "— chưa push" : r.GitCommitHash,
                r.WasPushed ? "✅ Pushed" : "⬜ Not pushed");
            TxtTodoHeader.Text = string.Format("✅ TODO ({0}/{1} xong, tổng {2}):",
                r.TodoDone, r.TodoTotal, FormatSpan(r.TotalTodoElapsed));

            var sessions = r.Sessions?.Select((s, i) => new
            {
                Index    = i + 1,
                Start    = s.StartTime.ToLocalTime().ToString("HH:mm:ss"),
                End      = s.EndTime.HasValue ? s.EndTime.Value.ToLocalTime().ToString("HH:mm:ss") : "đang chạy",
                Duration = FormatSpan(s.Duration)
            }).ToList();
            GridSessions.ItemsSource = sessions;

            var todos = r.Todos?.Select((t, i) => new
            {
                Index   = i + 1,
                t.Text,
                Status  = t.IsDone ? "✅ Xong" : "⬜ Chưa",
                Elapsed = t.TotalElapsed.TotalSeconds > 0 ? FormatSpan(t.TotalElapsed) : "—"
            }).ToList();
            GridTodos.ItemsSource = todos;
        }

        // ── Button handlers ───────────────────────────────────────────────

        /// <summary>Open the task URL in the system default browser.</summary>
        private void BtnOpenUrl_Click(object sender, RoutedEventArgs e)
        {
            var url = _summary?.FullReport?.TaskUrl;
            if (!string.IsNullOrWhiteSpace(url))
            {
                try { Process.Start(url); }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Lỗi mở URL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Task không có URL.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnOpenMd_Click(object sender, RoutedEventArgs e)
            => OpenFile(_summary.MarkdownFilePath);

        private void BtnOpenJson_Click(object sender, RoutedEventArgs e)
            => OpenFile(_summary.JsonFilePath);

        private void BtnClose_Click(object sender, RoutedEventArgs e)
            => Close();

        private static void OpenFile(string path)
        {
            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
            {
                try { Process.Start(path); }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("File không tồn tại.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private static string FormatSpanFull(TimeSpan ts) =>
            string.Format("{0} giờ {1} phút {2} giây", (int)ts.TotalHours, ts.Minutes, ts.Seconds);

        private static string FormatSpan(TimeSpan ts) =>
            ts.TotalHours >= 1
                ? string.Format("{0}h {1}m {2}s", (int)ts.TotalHours, ts.Minutes, ts.Seconds)
                : string.Format("{0}m {1}s", ts.Minutes, ts.Seconds);
    }
}
