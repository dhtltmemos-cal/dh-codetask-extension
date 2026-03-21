🤖 PROMPT: Xây dựng VS2017 Extension — DevTaskTracker

> Phiên bản: 3.0 — Tích hợp Gitea, Kiến trúc mở rộng, TODO Time Tracking,
> Completion Report, History Browser (Ngày / Tuần / Tháng)
> Mục tiêu: Sinh toàn bộ source code VSIX cho Visual Studio 2017

---

INTENT ANALYSIS

Câu hỏi Trả lời

Agent làm GÌ? Sinh code C#/XAML cho VS2017 VSIX extension — task tracker tích hợp Gitea
Input? Spec chức năng từ user
Output? Source code hoàn chỉnh, cấu trúc project, hướng dẫn build
Platform? Claude / GPT-4 với khả năng sinh code dài
Ai consume? Developer tự build & deploy VSIX vào VS2017

---

[1] ROLE & CONTEXT

Bạn là Senior .NET Developer chuyên xây dựng Visual Studio Extensions (VSIX).
Môi trường mục tiêu: Visual Studio 2017 (version 15.x), .NET Framework 4.6.1+.
Mục tiêu: Tạo một VSIX Extension tên DevTaskTracker giúp developer ghi nhận,
theo dõi và quản lý công việc ngay trong IDE, tích hợp chính với Gitea.

Triết lý kiến trúc — BẮT BUỘC áp dụng xuyên suốt:

> Mọi integration point (fetch task, ghi nhận sự kiện, push git, webhook...) phải
> được thiết kế theo Provider Pattern — một interface + default implementation,
> dễ dàng thêm provider mới mà KHÔNG sửa code hiện có.

---

[2] TASK DEFINITION

Tạo toàn bộ source code cho VSIX project gồm các file đầy đủ, không placeholder.

IN SCOPE:

Tool Window dạng panel dock vào VS

Fetch thông tin task từ URL — mặc định từ Gitea Issues API

Kiến trúc mở rộng: provider pattern cho mọi integration

Time tracking: bắt đầu, tạm ngưng, resume, kết thúc — cả ở cấp Task lẫn cấp TODO item

Ghi nhận nội dung thực hiện, danh sách TODO có time tracking riêng

Sinh commit message từ thông tin task

Lưu state local (JSON file)

Push git (gọi CLI git)

Event hook: mỗi hành động (start, pause, stop, todo-complete...) publish event để mở rộng

Completion Report: khi hoàn thành task → sinh file JSON + Markdown đầy đủ

History Browser: ToolWindow thứ 2 xem danh sách task theo Ngày/Tuần/Tháng

OUT OF SCOPE:

OAuth phức tạp (chỉ hỗ trợ Personal Access Token)

Sync real-time với server

Hỗ trợ VS 2019/2022

---

[3] INPUT SPECIFICATION

User cung cấp config — nếu thiếu, dùng DEFAULT:

TASK_TRACKER_SPEC:  
 gitea_base_url: [URL Gitea server — DEFAULT: "http://localhost:3000"]  
 gitea_token: [Personal Access Token — DEFAULT: "" (ẩn, nhập qua Settings UI)]  
 storage_path: [%APPDATA%\DevTaskTracker\ — DEFAULT giữ nguyên]  
 git_auto_push: [true | false — DEFAULT: false]  
 time_format: [hh:mm | decimal — DEFAULT: hh:mm]  
 webhook_enabled: [true | false — DEFAULT: false]  
 webhook_url: [URL nhận POST event — DEFAULT: ""]  
 report_format: [json+markdown | json | markdown — DEFAULT: json+markdown]  
 history_default_view: [day | week | month — DEFAULT: week]

---

[4] STEP-BY-STEP INSTRUCTIONS

Thực hiện theo thứ tự sau, KHÔNG bỏ bước.

---

Bước 1 — Scaffold Project Structure

DevTaskTracker/  
├── source.extension.vsixmanifest  
├── DevTaskTrackerPackage.cs  
│  
├── Core/ ← KHÔNG phụ thuộc vào VS SDK  
│ ├── Interfaces/  
│ │ ├── ITaskProvider.cs  
│ │ ├── ITimeTracker.cs  
│ │ ├── IStorageService.cs  
│ │ ├── IGitService.cs  
│ │ ├── IEventBus.cs  
│ │ ├── IReportGenerator.cs ← NEW  
│ │ └── IHistoryRepository.cs ← NEW  
│ ├── Events/  
│ │ ├── TaskEventArgs.cs  
│ │ └── TodoEventArgs.cs  
│ ├── Models/  
│ │ ├── TaskItem.cs  
│ │ ├── TodoItem.cs  
│ │ ├── TimeSession.cs  
│ │ ├── WorkLog.cs  
│ │ └── CompletionReport.cs ← NEW  
│ └── Services/  
│ ├── EventBus.cs  
│ ├── TimeTrackingService.cs  
│ └── HistoryQueryService.cs ← NEW  
│  
├── Providers/  
│ ├── TaskProviders/  
│ │ ├── GiteaTaskProvider.cs  
│ │ ├── ManualTaskProvider.cs  
│ │ └── TaskProviderFactory.cs  
│ ├── NotificationProviders/  
│ │ ├── WebhookNotificationProvider.cs  
│ │ └── INotificationProvider.cs  
│ ├── StorageProviders/  
│ │ └── JsonStorageService.cs  
│ └── ReportProviders/ ← NEW  
│ ├── JsonReportGenerator.cs  
│ ├── MarkdownReportGenerator.cs  
│ └── CompositeReportGenerator.cs  
│  
├── Commands/  
│ ├── OpenTrackerCommand.cs  
│ ├── OpenHistoryCommand.cs ← NEW  
│ └── PushAndCompleteCommand.cs  
│  
├── ViewModels/  
│ ├── TrackerViewModel.cs  
│ ├── TodoItemViewModel.cs  
│ └── HistoryViewModel.cs ← NEW  
│  
└── Views/  
 ├── TrackerToolWindow.cs  
 ├── TrackerControl.xaml / .xaml.cs  
 ├── HistoryToolWindow.cs ← NEW  
 ├── HistoryControl.xaml / .xaml.cs ← NEW  
 ├── ReportDetailDialog.xaml / .xaml.cs ← NEW  
 └── SettingsDialog.xaml / .xaml.cs

---

Bước 2 — Core Interfaces (Provider Pattern)

ITaskProvider.cs

public interface ITaskProvider  
{  
 bool CanHandle(string url);  
 Task<(TaskItem? task, string? errorMessage)> FetchAsync(  
 string url, CancellationToken cancellationToken = default);  
}

IEventBus.cs

public interface IEventBus  
{  
 void Publish<TEvent>(TEvent eventArgs) where TEvent : EventArgs;  
 void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : EventArgs;  
 void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : EventArgs;  
}

IReportGenerator.cs — NEW

public interface IReportGenerator  
{  
 // Sinh file báo cáo, trả về đường dẫn file đã lưu  
 Task<string> GenerateAsync(CompletionReport report, string outputDirectory);  
}

IHistoryRepository.cs — NEW

public interface IHistoryRepository  
{  
 Task<IEnumerable<CompletionReport>> GetAllAsync();  
 Task<IEnumerable<CompletionReport>> GetByDateRangeAsync(DateTime from, DateTime to);  
 Task<IEnumerable<CompletionReport>> GetTodayAsync();  
 Task<IEnumerable<CompletionReport>> GetThisWeekAsync(); // Monday–Sunday  
 Task<IEnumerable<CompletionReport>> GetThisMonthAsync();  
 Task DeleteAsync(string reportId);  
}

---

Bước 3 — CompletionReport Model — NEW

// Bản chụp hoàn chỉnh khi task kết thúc — IMMUTABLE sau khi archive  
public class CompletionReport  
{  
 // Định danh  
 public string ReportId { get; set; } // GUID — tên file chính  
 public DateTime CreatedAt { get; set; } // Thời điểm sinh report

    // Thông tin Task
    public string TaskId { get; set; }
    public string TaskTitle { get; set; }
    public string TaskUrl { get; set; }
    public string[] Labels { get; set; }
    public string Description { get; set; }

    // Thời gian — QUAN TRỌNG
    public DateTime StartedAt { get; set; }      // Thời điểm Start đầu tiên
    public DateTime CompletedAt { get; set; }    // Thời điểm Stop/Push cuối
    public TimeSpan TotalElapsed { get; set; }   // Tổng thời gian làm việc thực tế
    public List<TimeSession> Sessions { get; set; } // Chi tiết từng phiên làm việc

    // Nội dung thực hiện
    public string WorkNotes { get; set; }
    public string BusinessLogic { get; set; }
    public List<TodoItem> Todos { get; set; }

    // Kết quả
    public string CommitMessage { get; set; }
    public string GitBranch { get; set; }
    public string GitCommitHash { get; set; }    // Hash sau khi push
    public bool WasPushed { get; set; }

    // Computed
    public int TodoTotal => Todos?.Count ?? 0;
    public int TodoDone => Todos?.Count(t => t.IsDone) ?? 0;
    public TimeSpan TotalTodoElapsed => TimeSpan.FromSeconds(
        Todos?.Sum(t => t.TotalElapsed.TotalSeconds) ?? 0);

}

---

Bước 4 — GiteaTaskProvider

// Implement ITaskProvider  
// CanHandle(url): true nếu url match pattern gitea_base_url  
//  
// FetchAsync(url):  
// Parse URL: {owner}/{repo}/issues/{number}  
// GET {gitea_base_url}/api/v1/repos/{owner}/{repo}/issues/{number}  
// Header: Authorization: token {gitea_token}  
// Map:  
// TaskItem.Id = issue.number  
// TaskItem.Title = issue.title  
// TaskItem.Description = issue.body (strip markdown, max 500 chars)  
// TaskItem.Labels = issue.labels[].name  
// TaskItem.Url = issue.html_url  
// Fallback: HTTP GET url → parse <title> tag từ HTML  
// Timeout: 10 giây  
// KHÔNG throw — wrap exception → (null, errorMessage)

---

Bước 5 — TimeTrackingService

// State machine dùng chung cho Task và TodoItem  
// States: IDLE → RUNNING → PAUSED → COMPLETED  
//  
// Start(): Tạo TimeSession { StartTime = DateTime.UtcNow }, publish event, auto-save timer 30s  
// Pause(): Ghi PausedAt, cộng elapsed, publish event  
// Resume(): Tạo session mới, publish event  
// Stop(): Finalize, tính TotalElapsed = sum(sessions), publish event, trả WorkLog  
// GetElapsed(): TimeSpan real-time kể cả khoảng đang chạy

---

Bước 6 — TodoItem với Time Tracking

public class TodoItem : INotifyPropertyChanged  
{  
 public string Id { get; set; } // GUID  
 public string Text { get; set; }  
 public bool IsDone { get; set; }  
 public List<TimeSession> Sessions { get; set; } = new();  
 public TodoStatus Status { get; set; } // IDLE | RUNNING | PAUSED | DONE  
 public TimeSpan TotalElapsed => TimeSpan.FromSeconds(Sessions.Sum(s => s.ElapsedSeconds));  
}

// TodoItemViewModel expose:  
// - StartCommand, PauseCommand, CompleteCommand  
// - ElapsedDisplay: "00:12:34" — cập nhật mỗi giây khi Running  
// - Publish TodoStartedEvent, TodoPausedEvent, TodoCompletedEvent qua IEventBus

---

Bước 7 — MarkdownReportGenerator — NEW

Sinh file .md với nội dung chuẩn:

# ✅ Completion Report — #{TaskId}: {TaskTitle}

> **Repo/URL:** {TaskUrl}  
> **Labels:** {label1}, {label2}

---

## ⏱ Thời gian

| Mốc              | Thời điểm             |
| ---------------- | --------------------- |
| 🟢 Bắt đầu       | 2024-01-15 09:00:00   |
| 🔴 Kết thúc      | 2024-01-15 11:23:41   |
| ⏳ Tổng làm việc | 2 giờ 23 phút 41 giây |

### Chi tiết các phiên làm việc

| #   | Bắt đầu  | Kết thúc | Thời lượng |
| --- | -------- | -------- | ---------- |
| 1   | 09:00:00 | 10:15:00 | 1h 15m     |
| 2   | 10:30:00 | 11:23:41 | 53m 41s    |

---

## 📝 Ghi chú thực hiện

{WorkNotes}

---

## 💼 Mô tả nghiệp vụ

{BusinessLogic}

---

## ✅ Danh sách TODO

| #   | Nội dung       | Trạng thái   | Thời gian làm |
| --- | -------------- | ------------ | ------------- |
| 1   | Viết unit test | ✅ Xong      | 00:25:00      |
| 2   | Review PR      | ✅ Xong      | 00:18:00      |
| 3   | Cập nhật docs  | ⬜ Chưa xong | —             |

**Tổng TODO:** 3 | **Hoàn thành:** 2/3 | **Tổng giờ TODO:** 00:43:00

---

## 🔀 Commit

{CommitMessage}

**Branch:** {GitBranch}  
**Commit Hash:** {GitCommitHash hoặc "— chưa push"}

---

_Sinh tự động bởi DevTaskTracker — {CreatedAt}_

Quy tắc đặt tên và lưu file:

%APPDATA%\DevTaskTracker\history\  
 └── {YYYY}\  
 └── {MM}\  
 ├── {YYYYMMDD}_{HHmmss}_{TaskId}_{TitleSlug}.json  
 └── {YYYYMMDD}_{HHmmss}_{TaskId}_{TitleSlug}.md

Ví dụ: 2024\01\20240115_112341_142_add-user-auth.md

---

Bước 8 — HistoryQueryService — NEW

// Implementation của IHistoryRepository  
// Đọc lazy từ thư mục history/ — scan đệ quy \*.json  
//  
// GetAllAsync():  
// Scan %APPDATA%\DevTaskTracker\history\*\*\*.json  
// Deserialize → List<CompletionReport>  
// Sort theo CompletedAt DESC  
// Cache in-memory, invalidate khi có file mới (FileSystemWatcher)  
//  
// GetByDateRangeAsync(from, to):  
// Tối ưu: chỉ scan thư mục YYYY\MM phù hợp với range  
// Filter: report.CompletedAt >= from && <= to  
//  
// GetTodayAsync():  
// from = DateTime.Today 00:00:00  
// to = DateTime.Today 23:59:59  
//  
// GetThisWeekAsync():  
// from = Monday tuần hiện tại 00:00:00  
// to = Sunday tuần hiện tại 23:59:59  
//  
// GetThisMonthAsync():  
// from = ngày 1 tháng hiện tại 00:00:00  
// to = ngày cuối tháng 23:59:59  
//  
// GetSummaryAsync(reports): trả về HistorySummary  
// { TotalTasks, TotalElapsed, AvgElapsed, TotalTodos, TodoDoneRate }

---

Bước 9 — HistoryViewModel — NEW

public class HistoryViewModel : INotifyPropertyChanged  
{  
 // Filter state  
 public HistoryViewMode ViewMode { get; set; } // Today | ThisWeek | ThisMonth | Custom  
 public DateTime CustomFrom { get; set; }  
 public DateTime CustomTo { get; set; }  
 public string SearchKeyword { get; set; } // Filter theo title / taskId

    // Data
    public ObservableCollection<CompletionReportSummary> Items { get; }
    public HistorySummary Summary { get; }         // Tổng hợp bên trên danh sách
    public bool IsLoading { get; }

    // Commands
    public ICommand ChangeViewModeCommand { get; } // Today / Week / Month / Custom
    public ICommand RefreshCommand { get; }
    public ICommand OpenDetailCommand { get; }     // Mở ReportDetailDialog
    public ICommand OpenFileCommand { get; }       // Mở file .md trong VS Editor
    public ICommand DeleteReportCommand { get; }   // Xóa có confirm dialog
    public ICommand ExportCsvCommand { get; }      // Export danh sách lọc ra CSV

}

public enum HistoryViewMode { Today, ThisWeek, ThisMonth, Custom }

// CompletionReportSummary — row trong list (không load toàn bộ JSON)  
public class CompletionReportSummary  
{  
 public string ReportId { get; set; }  
 public string TaskId { get; set; }  
 public string TaskTitle { get; set; }  
 public DateTime StartedAt { get; set; }  
 public DateTime CompletedAt { get; set; }  
 public TimeSpan TotalElapsed { get; set; }  
 public string ElapsedDisplay { get; set; } // "2h 23m"  
 public int TodoDone { get; set; }  
 public int TodoTotal { get; set; }  
 public bool WasPushed { get; set; }  
 public string FilePath { get; set; }  
}

---

Bước 10 — HistoryControl.xaml — NEW

Sinh XAML đầy đủ với layout:

┌──────────────────────────────────────────────────────────────┐  
│ 📚 TASK HISTORY │  
├──────────────────────────────────────────────────────────────┤  
│ [Hôm nay] [Tuần này] [Tháng này] [Tùy chỉnh ▼] │  
│ Tùy chỉnh: [Từ: DatePicker] → [Đến: DatePicker] [Lọc] │  
│ 🔍 [Search theo tiêu đề / task ID ─────────────] [🔄] │  
├──────────────────────────────────────────────────────────────┤  
│ 📊 TỔNG HỢP — Tuần này │  
│ ┌──────────┬───────────────┬───────────────┬─────────────┐ │  
│ │ 12 tasks │ 18h 42m tổng │ 1h 33m / task │ 87% TODO ✓ │ │  
│ └──────────┴───────────────┴───────────────┴─────────────┘ │  
├──────────────────────────────────────────────────────────────┤  
│ DANH SÁCH — nhóm theo ngày (sort: Hoàn thành ↓) │  
│ ┌──────────────────────────────────────────────────────┐ │  
│ │ 📅 Thứ Hai, 15/01/2024 (2 tasks) │ │  
│ │ ✅ #142 Add user authentication │ │  
│ │ 09:00 → 11:23 │ ⏱ 2h 23m │ 3/3 ✓ │ 🔀 Pushed │ │  
│ │ ✅ #138 Fix login redirect bug │ │  
│ │ 14:00 → 15:10 │ ⏱ 1h 10m │ 1/2 ✓ │ 🔀 Pushed │ │  
│ ├──────────────────────────────────────────────────────┤ │  
│ │ 📅 Thứ Ba, 16/01/2024 (1 task) │ │  
│ │ ✅ #145 Refactor DB layer │ │  
│ │ 09:30 → 13:45 │ ⏱ 4h 15m │ 5/5 ✓ │ ⬜ Not pushed│ │  
│ └──────────────────────────────────────────────────────┘ │  
│ [◀ Prev] Trang 1/3 [Next ▶] [Export CSV] │  
└──────────────────────────────────────────────────────────────┘

Yêu cầu XAML:

Grouped ListView — CollectionViewSource với GroupDescriptions theo ngày

Group header hiển thị: "📅 Thứ Ba, 16/01/2024 (1 task)"

Click row → mở ReportDetailDialog

Double-click → mở file .md trong VS Editor

Summary bar dùng UniformGrid 4 ô

[Hôm nay/Tuần/Tháng] dùng RadioButton style toggle group

Phân trang: 20 items/trang dùng ICollectionView + pagination logic

---

Bước 11 — ReportDetailDialog — NEW

┌─────────────────────────────────────────────────────────┐  
│ 📋 Chi tiết: #142 Add user authentication │  
├─────────────────────────────────────────────────────────┤  
│ 🟢 Bắt đầu: 15/01/2024 09:00:00 │  
│ 🔴 Kết thúc: 15/01/2024 11:23:41 │  
│ ⏳ Thời gian: 2 giờ 23 phút 41 giây │  
│ │  
│ Chi tiết phiên làm việc: │  
│ ┌──────────────────────────────────────────────────┐ │  
│ │ Phiên 1: 09:00:00 → 10:15:00 (1h 15m 00s) │ │  
│ │ Phiên 2: 10:30:00 → 11:23:41 (0h 53m 41s) │ │  
│ └──────────────────────────────────────────────────┘ │  
├─────────────────────────────────────────────────────────┤  
│ 📝 Ghi chú: [readonly multiline TextBox] │  
│ 💼 Nghiệp vụ: [readonly multiline TextBox] │  
├─────────────────────────────────────────────────────────┤  
│ ✅ TODO (3/3 xong, tổng 43 phút) │  
│ ✓ Viết unit test 00:25:00 │  
│ ✓ Review PR 00:18:00 │  
│ ⬜ Cập nhật docs — │  
├─────────────────────────────────────────────────────────┤  
│ 🔀 feat(backend): add-user-authentication-flow │  
│ Branch: main │ Hash: a3f9d12 │ 🔀 Pushed │  
├─────────────────────────────────────────────────────────┤  
│ [📂 Mở .md] [📂 Mở .json] [🗑 Xóa] [✕ Đóng] │  
└─────────────────────────────────────────────────────────┘

---

Bước 12 — Luồng Hoàn thành Task — NEW

Khi user nhấn [🚀 Push & Complete]:

1. GitService.PushAndCompleteAsync(commitMsg)  
   → Lấy commitHash sau khi push

2. TimeTrackingService.Stop()  
   → Finalize WorkLog, CompletedAt = DateTime.UtcNow

3. CompletionReportBuilder.Build(workLog, gitResult):  
   → Tạo CompletionReport đầy đủ:
   - StartedAt = sessions[0].StartTime
   - CompletedAt = DateTime.UtcNow
   - TotalElapsed = sum(all sessions)
   - Sessions[] = chi tiết từng phiên (giờ bắt đầu, giờ kết thúc, thời lượng)
   - Todos[] = snapshot với time tracking riêng từng item
   - CommitMessage, GitBranch, GitCommitHash, WasPushed = true

4. CompositeReportGenerator.GenerateAsync(report, historyDir):  
   → JsonReportGenerator → lưu .json  
   → MarkdownReportGenerator → lưu .md

5. StorageService.ClearCurrentTask()  
   → Xóa current-task.json

6. EventBus.Publish(TaskCompletedEvent { Report = report })  
   → WebhookProvider nhận → POST payload đầy đủ ra ngoài

7. UI:  
   → Toast: "✅ Task #142 hoàn thành — 2h 23m — Report đã lưu"  
   → Nút [Xem Report] → mở ReportDetailDialog ngay  
   → Clear form, sẵn sàng task mới

───────────────────────────────────────────────────────────  
Khi user nhấn [⏸ Save & Pause Task] (KHÔNG push):  
 → Bước 2, 3, 4 (không push git)  
 → current-task.json GIỮ LẠI  
 → WasPushed = false  
 → File .md có tag "⏸ TẠM NGƯNG" thay vì "✅ HOÀN THÀNH"

---

Bước 13 — EventBus & Events đầy đủ

// Events định nghĩa sẵn:  
TaskFetchedEvent { TaskItem Task, string Url }  
TaskStartedEvent { WorkLog Log, DateTime StartTime }  
TaskPausedEvent { WorkLog Log, TimeSpan Elapsed }  
TaskResumedEvent { WorkLog Log }  
TaskCompletedEvent { CompletionReport Report } // ← chứa Report đầy đủ  
TodoStartedEvent { TodoItem Todo, string TaskId }  
TodoPausedEvent { TodoItem Todo }  
TodoCompletedEvent { TodoItem Todo, TimeSpan Elapsed }  
CommitPushedEvent { string CommitMessage, bool Success, string Hash }  
ReportSavedEvent { string FilePath, CompletionReport Report } // ← NEW

---

Bước 14 — WebhookNotificationProvider

// POST JSON mỗi khi có event, retry 3 lần exponential backoff (1s, 2s, 4s)  
// Payload TaskCompletedEvent:  
// {  
// "event": "task.completed",  
// "timestamp": "2024-01-15T11:23:41Z",  
// "task": { "id": 142, "title": "...", "url": "..." },  
// "elapsed_seconds": 8621,  
// "elapsed_display": "2h 23m 41s",  
// "started_at": "2024-01-15T09:00:00Z",  
// "completed_at": "2024-01-15T11:23:41Z",  
// "todos": { "total": 3, "done": 3, "elapsed_seconds": 2580 },  
// "commit": { "message": "feat(backend)...", "hash": "a3f9d12", "pushed": true },  
// "report_file": "2024\\01\\20240115_112341_142_add-user-auth.md"  
// }  
// Fail silently — KHÔNG block UI

---

Bước 15 — Auto Commit Message Generator

Format: "{type}({scope}): {title_slug}"  
 " {business_short}"  
 " Ref: #{task_id}"

type = "fix"/"feat"/"chore" — detect từ labels + title keywords  
scope = tên repo từ URL Gitea  
task_id, title_slug từ TaskItem  
business_short = 15 từ đầu của BusinessLogic  
[OPTIONAL] Bỏ dòng 2 nếu BusinessLogic trống  
[OPTIONAL] Bỏ dòng Ref nếu không parse được task_id

---

Bước 16 — GitService

// FindRepoRoot(startPath): leo thư mục tìm ".git"  
// GetCurrentBranch(): string  
// RunGitCommandAsync(repoRoot, args): Process capture stdout+stderr  
// PushAndCompleteAsync(repoRoot, commitMsg):  
// 1. git add -A  
// 2. git commit -m "{commitMsg}"  
// 3. git push (nếu git_auto_push = true)  
// 4. git rev-parse HEAD → lấy hash  
// 5. Publish CommitPushedEvent { Hash }  
// 6. Trả về (success, output, hash)  
// Nếu git không có trong PATH → disable nút Push + tooltip

---

Bước 17 — StorageService (JSON)

// Paths:  
// current-task.json ← task đang làm  
// history\{YYYY}\{MM}\*.json ← báo cáo hoàn thành theo năm/tháng  
// settings.json

// ArchiveReport(CompletionReport):  
// → Tạo thư mục history\{YYYY}\{MM}\ nếu chưa có  
// → Lưu {YYYYMMDD}_{HHmmss}_{TaskId}\_{slug}.json (atomic write)  
// → Gọi MarkdownReportGenerator → lưu .md cùng thư mục  
// → Publish ReportSavedEvent

// HistoryQueryService sử dụng FileSystemWatcher để invalidate cache  
// khi có file mới được thêm vào thư mục history

---

Bước 18 — Package Registration

// DevTaskTrackerPackage.cs:  
// - [ProvideToolWindow(typeof(TrackerToolWindow), Style = VsDockStyle.Tabbed)]  
// - [ProvideToolWindow(typeof(HistoryToolWindow), Style = VsDockStyle.Tabbed)] ← NEW  
// - [ProvideMenuResource("Menus.ctmenu", 1)]  
// - [ProvideOptionPage(typeof(SettingsPage), "DevTaskTracker", "General", ...)]  
//  
// InitializeAsync():  
// 1. EventBus (singleton)  
// 2. StorageService + HistoryQueryService  
// 3. Load settings  
// 4. Đăng ký TaskProviderFactory, CompositeReportGenerator  
// 5. Subscribe WebhookProvider nếu enabled  
// 6. Restore task dở → InfoBar "Resume task #id?"

---

Bước 19 — TrackerControl.xaml (UI chính — đầy đủ)

┌──────────────────────────────────────────────────────┐  
│ 🔗 [URL TextBox ─────────────────] [Fetch] [Clear] │  
│ Status: "Fetching..." / "✅ Gitea #123" / ❌ lỗi │  
├──────────────────────────────────────────────────────┤  
│ 📌 Title: [editable] │  
│ 🏷 Labels: [badge1] [badge2] │  
│ 📄 Description: [editable multiline] │  
├──────────────────────────────────────────────────────┤  
│ ⏱ TASK TIMER: 00:23:41 ● RUNNING │  
│ [▶ Start] [⏸ Pause] [▶ Resume] [⏹ Stop] │  
├──────────────────────────────────────────────────────┤  
│ 📝 Work Notes: [multiline TextBox] │  
├──────────────────────────────────────────────────────┤  
│ ✅ TODO List: [+ Add Item] │  
│ [New item TextBox ───────────────] [Add] │  
│ ┌────────────────────────────────────────────────┐ │  
│ │ ☐ [item 1 text]──⏱00:05:12 [▶][⏸][✓][🗑] │ │  
│ │ ☐ [item 2 text]──⏱00:00:00 [▶][⏸][✓][🗑] │ │  
│ │ ☑ [item 3 text]──⏱00:12:30 DONE │ │  
│ └────────────────────────────────────────────────┘ │  
│ Tổng: 3 | Xong: 1 | Đang: 1 | Tổng: 00:17:42 │  
├──────────────────────────────────────────────────────┤  
│ 💼 Business Logic: [multiline TextBox] │  
├──────────────────────────────────────────────────────┤  
│ 🔀 Commit: [editable] [↺ Regenerate] │  
├──────────────────────────────────────────────────────┤  
│ [🚀 Push & Complete] [⏸ Save & Pause] │  
│ [📚 Xem Lịch sử] [⚙ Settings] │  
└──────────────────────────────────────────────────────┘

---

[5] OUTPUT FORMAT

Với mỗi file:

### 📄 {ĐƯỜNG_DẪN_FILE}

````csharp / xml / xaml
{nội dung code đầy đủ — không placeholder, không "// TODO: implement"}


---

Sau tất cả file:
```markdown
## 🔧 SETUP INSTRUCTIONS
1. Prerequisites: SDK, NuGet packages (danh sách đầy đủ với version)
2. Tạo VSIX project mới trong VS2017 (step by step)
3. Cấu hình Gitea Personal Access Token
4. Build & Debug trong Experimental Instance
5. Deploy .vsix
6. Cấu trúc thư mục history sau khi chạy thực tế

## 🔌 EXTENSION GUIDE — Cách mở rộng
1. Thêm ITaskProvider mới (ví dụ: Jira)
2. Thêm IReportGenerator mới (ví dụ: HTML report)
3. Thêm INotificationProvider mới (ví dụ: Slack)
4. Thêm event mới vào EventBus
5. Thêm view filter mới vào HistoryBrowser


---

[6] CONSTRAINTS & RULES

LUÔN dùng async/await đúng — không async void trừ event handler

LUÔN dispose HttpClient đúng (singleton hoặc factory)

KHÔNG dùng Thread.Sleep — dùng Task.Delay

KHÔNG hard-code URL, token, path — đọc từ settings

LUÔN try-catch ở boundary (UI, HTTP, Git, file I/O)

KHÔNG dùng deprecated VS SDK API — dùng MEF + AsyncPackage

Provider mới KHÔNG được sửa code class đã tồn tại (Open/Closed Principle)

Mọi VS UI interaction phải trên UI thread (JoinableTaskFactory)

EventBus publish non-blocking — subscriber lỗi không crash publisher

File lưu phải atomic: ghi temp → rename (tránh corrupt nếu crash)

HistoryQueryService: đọc lazy, không load toàn bộ JSON vào RAM cùng lúc

CompletionReport: immutable sau khi archive — không cho phép sửa

Grouping trong History list phải dùng local timezone, không phải UTC



---

[7] FALLBACK LOGIC

Tình huống	Xử lý

URL không phải Gitea	TaskProviderFactory thử từng provider → fallback ManualTaskProvider
Gitea token sai/hết hạn	Hiển thị lỗi 401 rõ ràng, mở Settings dialog
Git không tìm thấy	Disable Push button, tooltip "Git not found in PATH"
Push git thất bại	Vẫn sinh report với WasPushed = false, không mất data
File storage corrupt	Backup .bak, tạo mới, log Output Window
VS tắt đột ngột	Auto-save 30s + restore khi mở lại
Webhook fail	Retry 3 lần, log lỗi, KHÔNG block UI
TODO đang chạy khi Task pause	Auto-pause tất cả TODO đang Running
History thư mục rỗng	Empty state: "Chưa có task nào hoàn thành trong khoảng này"
Filter không có kết quả	"Không tìm thấy task nào — thử mở rộng khoảng thời gian"
Export CSV fail (quyền ghi)	Mở SaveFileDialog để user chọn thư mục khác



---

⚠️ LƯU Ý ĐẶC BIỆT CHO VSIX 2017

<InstallationTarget Id="Microsoft.VisualStudio.Community" Version="[15.0,16.0)" />

<!-- packages.config — KHÔNG dùng PackageReference -->
Microsoft.VSSDK.BuildTools                        15.x
Microsoft.VisualStudio.Shell.15.0                 15.x
Microsoft.VisualStudio.Shell.Interop.15.3.DesignTime
Newtonsoft.Json                                   13.x

ToolWindowPane override CreateControl() → WPF UserControl

KnownMonikers cho icons

Test: devenv /rootsuffix Exp

UI thread: await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync()

Logging: IVsOutputWindowPane

CollectionViewSource + GroupDescriptions cho grouped History list

Khi mở file .md trong VS Editor: dùng IVsUIShellOpenDocument.OpenDocumentViaProject



---

🔌 VÍ DỤ MỞ RỘNG TƯƠNG LAI

Thêm HTML report:
  → Tạo HtmlReportGenerator : IReportGenerator
  → Đăng ký vào CompositeReportGenerator
  → Xong. Không sửa gì khác.

Thêm Jira support:
  → Tạo JiraTaskProvider : ITaskProvider
  → Đăng ký vào TaskProviderFactory
  → Xong.

Tự động đóng Gitea Issue sau khi push:
  → Subscribe CommitPushedEvent
  → PATCH /api/v1/repos/{owner}/{repo}/issues/{id} state="closed"
  → Xong.

Export báo cáo tuần gửi email:
  → Subscribe ScheduledWeekEndEvent
  → HistoryQueryService.GetThisWeekAsync()
  → Tổng hợp → SmtpClient.SendAsync
  → Xong.

Thêm Pomodoro timer tích hợp TODO:
  → Subscribe TodoStartedEvent → 25-phút countdown
  → Publish PomodoroBreakEvent → VS InfoBar notification
  → Xong.

Thêm filter theo label/repo trong History:
  → Thêm LabelFilter, RepoFilter vào HistoryViewModel
  → HistoryQueryService.GetByFilterAsync(filter)
  → Xong.


---

Prompt version 3.0 — DevTaskTracker VS2017 Extension
Gitea-first · Provider Pattern · TODO Time Tracking · EventBus
Completion Report (JSON+Markdown) · History Browser (Day/Week/Month)
````
