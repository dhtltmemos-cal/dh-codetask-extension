# DhCodetaskExtension — VS2017 Extension v3.0

> **Track development tasks, time, and TODOs directly inside Visual Studio 2017 — with Gitea integration, completion reports, and a full history browser.**

---

## Mục lục

- [Tổng quan](#tổng-quan)
- [Tính năng](#tính-năng)
- [Yêu cầu hệ thống](#yêu-cầu-hệ-thống)
- [Cài đặt & Build](#cài-đặt--build)
- [Cấu hình](#cấu-hình)
- [Hướng dẫn sử dụng](#hướng-dẫn-sử-dụng)
- [Cấu trúc dữ liệu](#cấu-trúc-dữ-liệu)
- [Kiến trúc](#kiến-trúc)
- [Mở rộng](#mở-rộng)
- [Troubleshooting](#troubleshooting)

---

## Tổng quan

DhCodetaskExtension là một VSIX extension cho **Visual Studio 2017** giúp developer:

- Fetch thông tin task/issue trực tiếp từ **Gitea** (hoặc nhập thủ công)
- **Time tracking** cấp task và cấp TODO item riêng biệt
- Quản lý **danh sách TODO** với bộ đếm thời gian độc lập cho từng item
- Sinh **commit message** tự động theo convention `type(scope): description`
- **Push git** và hoàn thành task trong một thao tác
- Sinh **báo cáo hoàn thành** (JSON + Markdown) lưu tự động theo cấu trúc năm/tháng
- Xem **lịch sử task** theo ngày / tuần / tháng với thống kê tổng hợp
- **Webhook** gửi event ra hệ thống ngoài (Slack, CI/CD, v.v.)
- **Cấu hình linh hoạt** qua Form UI hoặc chỉnh sửa JSON trực tiếp

---

## Tính năng

### Tracker Panel (Tool Window chính)

| Khu vực        | Chức năng                                                     |
| -------------- | ------------------------------------------------------------- |
| URL bar        | Nhập URL issue Gitea → Fetch tự động, hoặc nhập thủ công      |
| Task info      | Title, Labels, Description (có thể chỉnh sửa)                 |
| Task timer     | Start / Pause / Resume / Stop với đồng hồ real-time           |
| Work Notes     | Ghi chú quá trình làm việc                                    |
| TODO list      | Thêm/xóa item, mỗi item có timer độc lập, đánh dấu hoàn thành |
| Business Logic | Mô tả nghiệp vụ đã implement                                  |
| Commit message | Tự sinh + chỉnh sửa, nút Regenerate                           |
| Actions        | 🚀 Push & Complete · ⏸ Save & Pause · 📚 History · ⚙ Settings |

### History Browser (Tool Window thứ 2)

- Lọc theo **Hôm nay / Tuần này / Tháng này / Tùy chỉnh**
- Tìm kiếm theo tiêu đề hoặc Task ID
- Summary bar: tổng task, tổng giờ, giờ trung bình, tỉ lệ TODO hoàn thành
- Danh sách nhóm theo ngày (tiếng Việt)
- Click → xem chi tiết · Double-click → mở file `.md` trong VS Editor
- Phân trang 20 items/trang
- Export CSV

### Settings Dialog

Hai chế độ nhập cấu hình:

**Tab Form** — giao diện trực quan với 12 trường đầy đủ:

| Nhóm    | Trường                                                |
| ------- | ----------------------------------------------------- |
| Gitea   | Base URL, Personal Token, Username                    |
| Git CLI | Auto Push, user.name override, user.email override    |
| Storage | Đường dẫn lưu trữ tùy chỉnh                           |
| Reports | Định dạng (JSON/Markdown/cả hai), định dạng thời gian |
| History | Chế độ xem mặc định                                   |
| Webhook | Bật/tắt, URL                                          |

**Tab JSON** — chỉnh sửa trực tiếp chuỗi JSON cấu hình:

- Nút **Validate**: kiểm tra từng field với thông báo lỗi cụ thể
- Nút **Reset**: sync lại từ Form sang JSON
- Tự động sync 2 chiều khi chuyển tab
- Hỗ trợ trường `Extensions` (key/value tự do) để mở rộng không cần sửa code

### Completion Report

Mỗi task hoàn thành tự động sinh 2 file vào `%APPDATA%\DhCodetaskExtension\history\YYYY\MM\`:

**`.json`** — snapshot đầy đủ, immutable sau khi archive:

```json
{
  "ReportId": "...",
  "TaskId": "142",
  "TaskTitle": "Add user authentication",
  "StartedAt": "2024-01-15T09:00:00Z",
  "CompletedAt": "2024-01-15T11:23:41Z",
  "TotalElapsed": "02:23:41",
  "Sessions": [...],
  "Todos": [...],
  "CommitMessage": "feat(backend): add-user-authentication-flow",
  "GitBranch": "main",
  "GitCommitHash": "a3f9d12...",
  "WasPushed": true
}
```

**`.md`** — báo cáo Markdown có thể đọc trực tiếp trong VS:

```
# ✅ HOÀN THÀNH — #142: Add user authentication

## ⏱ Thời gian
| Mốc | Thời điểm |
...

## ✅ Danh sách TODO
...

## 🔀 Commit
feat(backend): add-user-authentication-flow
Branch: main | Hash: a3f9d12 | ✅ Pushed
```

---

## Yêu cầu hệ thống

| Yêu cầu        | Phiên bản                                               |
| -------------- | ------------------------------------------------------- |
| Visual Studio  | **2017** (15.x) — Community / Professional / Enterprise |
| .NET Framework | **4.6.1** trở lên                                       |
| VSSDK          | Microsoft.VSSDK.BuildTools **15.x**                     |
| Git            | Bất kỳ phiên bản nào có trong `PATH` (tùy chọn)         |
| Gitea          | Bất kỳ phiên bản nào có REST API v1 (tùy chọn)          |

---

## Cài đặt & Build

### Bước 1 — Clone và chuẩn bị

```bash
git clone <repo-url>
cd DhCodetaskExtension
```

### Bước 2 — Mở trong VS2017

1. Mở **Visual Studio 2017**
2. `File → Open → Project/Solution` → chọn `DhCodetaskExtension.csproj`
3. VS sẽ tự phát hiện đây là VSIX project

### Bước 3 — Restore NuGet packages

```
Tools → NuGet Package Manager → Package Manager Console
PM> Update-Package -reinstall
```

Hoặc chuột phải project → **Restore NuGet Packages**.

**Packages cần thiết** (trong `packages.config`):

| Package                                              | Version |
| ---------------------------------------------------- | ------- |
| Microsoft.VSSDK.BuildTools                           | 15.9.x  |
| Microsoft.VisualStudio.Shell.15.0                    | 15.0.x  |
| Microsoft.VisualStudio.Shell.Interop.15.3.DesignTime | 15.0.x  |
| Newtonsoft.Json                                      | 13.0.x  |

> ⚠️ **Lưu ý**: Dự án dùng `packages.config`, **không dùng** `PackageReference`. Không chuyển đổi sang PackageReference.

### Bước 4 — Build

```
Build → Build Solution   (hoặc Ctrl+Shift+B)
```

### Bước 5 — Debug trong Experimental Instance

```
Debug → Start Debugging   (hoặc F5)
```

VS sẽ mở một instance thứ hai (Experimental Instance) với extension được load. Hoặc chạy thủ công:

```bash
devenv.exe /rootsuffix Exp
```

### Bước 6 — Tạo file `.vsix` để phân phối

```
Build → Build Solution (Release mode)
```

File `.vsix` xuất hiện tại `bin\Release\DhCodetaskExtension.vsix`.

Cài đặt bằng cách double-click file `.vsix`, hoặc:

```
Tools → Extensions and Updates → Install from file...
```

---

## Cấu hình

### Mở Settings

- Trong Tracker Panel: nhấn nút **⚙ Settings**

### Tab JSON — Ví dụ cấu hình đầy đủ

```json
{
  "GiteaBaseUrl": "http://gitea.company.com",
  "GiteaToken": "your-personal-access-token",
  "GiteaUser": "john.doe",
  "GitAutoPush": false,
  "GitUserName": "John Doe",
  "GitUserEmail": "john@company.com",
  "StoragePath": "",
  "ReportFormat": "json+markdown",
  "TimeFormat": "hh:mm",
  "HistoryDefaultView": "week",
  "WebhookEnabled": false,
  "WebhookUrl": "",
  "Extensions": {
    "JiraBaseUrl": "https://jira.company.com",
    "PomodoroMinutes": "25"
  }
}
```

> **Trường `Extensions`**: key/value tự do, dùng cho plugin hoặc cấu hình tùy chỉnh mà không cần sửa `AppSettings.cs`.

### Lấy Gitea Personal Access Token

1. Đăng nhập Gitea → **Settings** (avatar góc trên phải)
2. **Applications** → mục **Manage Access Tokens**
3. Nhập tên token → chọn scope `repo` + `issues`
4. Copy token và dán vào Settings của DhCodetaskExtension

### Cấu hình Webhook

Khi `WebhookEnabled = true`, mỗi sự kiện sẽ gửi POST JSON đến `WebhookUrl`:

```json
{
  "event": "task.completed",
  "timestamp": "2024-01-15T11:23:41Z",
  "task": { "id": "142", "title": "...", "url": "..." },
  "elapsed_seconds": 8621,
  "elapsed_display": "2h 23m 41s",
  "todos": { "total": 3, "done": 3, "elapsed_seconds": 2580 },
  "commit": { "message": "feat(backend)...", "hash": "a3f9d12", "pushed": true }
}
```

Retry tự động 3 lần với exponential backoff (1s → 2s → 4s). Lỗi webhook không chặn UI.

---

## Hướng dẫn sử dụng

### Luồng cơ bản

```
1. Nhập URL issue Gitea → [Fetch]
      ↓ (hoặc nhập Title thủ công)
2. [▶ Start] — bắt đầu tính giờ
      ↓
3. Thêm TODO items → Start/Pause từng item
      ↓
4. Ghi Work Notes & Business Logic
      ↓
5. [↺ Regenerate] commit message (hoặc tự sửa)
      ↓
6. [🚀 Push & Complete] → git commit + push → sinh report
   hoặc
   [⏸ Save & Pause] → lưu lại, tiếp tục sau
```

### Fetch task từ Gitea

Dán URL issue vào URL bar, ví dụ:

```
http://localhost:3000/owner/repo/issues/142
```

Extension tự phân tích URL, gọi API `/api/v1/repos/{owner}/{repo}/issues/{number}`, và điền thông tin task.

**Lỗi 401**: Token sai hoặc hết hạn → mở Settings và cập nhật token.

**URL không phải Gitea**: Extension fallback sang đọc `<title>` từ HTML.

### TODO Time Tracking

Mỗi TODO item có timer độc lập:

- **[▶]** — bắt đầu / resume item đó
- **[⏸]** — tạm dừng item đó
- **[✓]** — đánh dấu hoàn thành, tự dừng timer
- **[🗑]** — xóa item

Khi task bị Pause, tất cả TODO đang chạy tự động bị pause theo.

### Commit Message tự động

Format: `type(scope): title_slug`

```
feat(my-repo): add-user-authentication-flow
  Implement JWT token refresh and session management
  Ref: #142
```

- **type**: detect từ labels và keywords trong title (`fix`/`feat`/`chore`)
- **scope**: tên repo từ URL Gitea
- **Ref**: task ID (bỏ qua nếu không parse được)

### Restore task dở

Khi mở lại VS sau khi đóng giữa chừng, InfoBar hiện:

```
DhCodetaskExtension: Task #142 — Add user authentication đang dở. Resume?
[▶ Resume]  [✕ Dismiss]
```

Click **Resume** → mở Tracker Panel và tải lại trạng thái cuối.

---

## Cấu trúc dữ liệu

### Thư mục lưu trữ

```
%APPDATA%\DhCodetaskExtension\
├── settings.json              ← Cấu hình ứng dụng
├── current-task.json          ← Task đang làm (xóa sau khi hoàn thành)
└── history\
    └── 2024\
        └── 01\
            ├── 20240115_112341_142_add-user-auth.json   ← Report JSON
            └── 20240115_112341_142_add-user-auth.md     ← Report Markdown
```

### settings.json

Xem phần [Tab JSON](#tab-json--ví-dụ-cấu-hình-đầy-đủ) ở trên.

### current-task.json

Lưu `WorkLog` — trạng thái task đang thực hiện. Ghi tự động mỗi 30 giây và khi Pause. Đây là cơ sở để restore khi VS khởi động lại.

---

## Kiến trúc

DhCodetaskExtension áp dụng **Provider Pattern** xuyên suốt: mọi integration point đều là interface + default implementation. Thêm provider mới không cần sửa code hiện có.

### Cấu trúc thư mục

```
DhCodetaskExtension/
├── Core/                          ← Không phụ thuộc VS SDK
│   ├── Interfaces/                ← Contracts của mọi service
│   ├── Models/                    ← Domain objects (immutable CompletionReport)
│   ├── Events/                    ← Event args cho EventBus
│   └── Services/                  ← Business logic thuần
│
├── Providers/                     ← Implementations có thể swap/extend
│   ├── TaskProviders/             ← Gitea, Manual, ...
│   ├── ReportProviders/           ← JSON, Markdown, Composite
│   ├── StorageProviders/          ← JSON file-based
│   ├── GitProviders/              ← Git CLI wrapper
│   └── NotificationProviders/    ← Webhook
│
├── ViewModels/                    ← MVVM, không biết VS SDK
├── Views/                         ← XAML + code-behind
└── Commands/                      ← VS menu/toolbar commands
```

### Interfaces chính

| Interface               | Mục đích                          |
| ----------------------- | --------------------------------- |
| `ITaskProvider`         | Fetch task từ URL bất kỳ          |
| `IReportGenerator`      | Sinh report theo định dạng bất kỳ |
| `IStorageService`       | Lưu/tải data                      |
| `IGitService`           | Git CLI operations                |
| `IEventBus`             | In-process pub/sub, non-blocking  |
| `IHistoryRepository`    | Query báo cáo lịch sử             |
| `INotificationProvider` | Gửi notification ra ngoài         |

### EventBus

Publish non-blocking: mỗi subscriber chạy trên `Task.Run()` độc lập. Subscriber lỗi không crash publisher.

**Events có sẵn:**

| Event                | Khi nào                                 |
| -------------------- | --------------------------------------- |
| `TaskFetchedEvent`   | Fetch task thành công                   |
| `TaskStartedEvent`   | Bắt đầu tính giờ                        |
| `TaskPausedEvent`    | Tạm dừng                                |
| `TaskResumedEvent`   | Resume                                  |
| `TaskCompletedEvent` | Hoàn thành (chứa full CompletionReport) |
| `TodoStartedEvent`   | TODO item bắt đầu                       |
| `TodoPausedEvent`    | TODO item tạm dừng                      |
| `TodoCompletedEvent` | TODO item hoàn thành                    |
| `CommitPushedEvent`  | Sau khi git push                        |
| `ReportSavedEvent`   | Sau khi lưu báo cáo                     |

### Atomic File Write

Tất cả ghi file đều qua `AtomicFile.WriteAllTextAsync()`:

```
1. Ghi nội dung vào {path}.tmp
2. Nếu {path} tồn tại → File.Replace(tmp, path, path.bak)  ← OS-level swap
3. Nếu chưa tồn tại  → File.Move(tmp, path)
```

### CompletionReport — Immutable

`CompletionReport` chỉ được tạo qua builder. Sau khi archive, không field nào có thể thay đổi:

```csharp
var report = CompletionReport.CreateBuilder()
    .TaskId("142")
    .TaskTitle("Add user authentication")
    .StartedAt(startTime)
    // ...
    .Build();
```

---

## Mở rộng

### Thêm provider task mới (ví dụ: Jira)

```csharp
// 1. Tạo file Providers/TaskProviders/JiraTaskProvider.cs
public class JiraTaskProvider : ITaskProvider
{
    public bool CanHandle(string url)
        => url.Contains("atlassian.net") && url.Contains("/browse/");

    public async Task<(TaskItem task, string errorMessage)> FetchAsync(
        string url, CancellationToken ct)
    {
        // Gọi Jira REST API...
        return (task, null);
    }
}

// 2. Đăng ký trong DhCodetaskExtensionPackage.InitializeAsync()
ProviderFactory.Register(new JiraTaskProvider(() => Settings));
// Xong. Không sửa gì khác.
```

### Thêm định dạng report mới (ví dụ: HTML)

```csharp
// 1. Tạo file Providers/ReportProviders/HtmlReportGenerator.cs
public class HtmlReportGenerator : IReportGenerator
{
    public async Task<string> GenerateAsync(CompletionReport report, string dir)
    {
        var html = BuildHtml(report);
        var path = Path.Combine(dir, $"{slug}.html");
        await AtomicFile.WriteAllTextAsync(path, html);
        return path;
    }
}

// 2. Đăng ký trong Package
ReportGenerator.Register(new HtmlReportGenerator());
// Xong.
```

### Thêm notification provider (ví dụ: Slack)

```csharp
// 1. Tạo class implements INotificationProvider
public class SlackNotificationProvider : INotificationProvider
{
    public void OnTaskCompleted(TaskCompletedEvent e)
        => _ = PostToSlackAsync(e);
}

// 2. Subscribe vào EventBus
var slack = new SlackNotificationProvider(slackWebhookUrl);
EventBus.Subscribe<TaskCompletedEvent>(slack.OnTaskCompleted);
// Xong.
```

### Thêm cấu hình mở rộng không cần sửa code

Dùng `Extensions` dictionary trong settings.json:

```json
{
  "Extensions": {
    "PomodoroMinutes": "25",
    "SlackChannel": "#dev-updates",
    "AutoCloseGiteaIssue": "true"
  }
}
```

Đọc trong code:

```csharp
var settings = DhCodetaskExtensionPackage.Settings;
if (settings.Extensions.TryGetValue("PomodoroMinutes", out var mins))
    int.TryParse(mins, out pomodoroMinutes);
```

### Thêm event mới

```csharp
// 1. Định nghĩa event args trong Core/Events/
public class PomodoroBreakEvent : EventArgs
{
    public int SessionNumber { get; set; }
    public TimeSpan SessionDuration { get; set; }
}

// 2. Publish từ bất kỳ đâu
EventBus.Publish(new PomodoroBreakEvent { SessionNumber = 1, ... });

// 3. Subscribe
EventBus.Subscribe<PomodoroBreakEvent>(e => ShowInfoBar("Nghỉ 5 phút!"));
```

---

## Troubleshooting

### Extension không load

- Kiểm tra VS version: phải là **15.x** (VS2017)
- Xem **Output Window → DhCodetaskExtension** để tìm lỗi khởi tạo
- Thử: `Tools → Extensions and Updates` → tìm DhCodetaskExtension → Enable

### Không fetch được task từ Gitea

| Triệu chứng        | Nguyên nhân                      | Xử lý                                                                |
| ------------------ | -------------------------------- | -------------------------------------------------------------------- |
| Lỗi 401            | Token sai hoặc hết hạn           | Tạo lại PAT trong Gitea Settings                                     |
| Lỗi 404            | URL sai hoặc issue không tồn tại | Kiểm tra URL format: `{base}/{owner}/{repo}/issues/{number}`         |
| Timeout            | Server không phản hồi            | Kiểm tra `GiteaBaseUrl` có đúng không                                |
| "Cannot parse URL" | URL không khớp pattern           | Đảm bảo `GiteaBaseUrl` trong Settings match với domain của URL issue |

### Git push thất bại

- Nút Push bị disable + tooltip "Git not found in PATH": cài Git và thêm vào PATH
- Lỗi authentication: kiểm tra SSH key hoặc HTTPS credential
- Push thất bại nhưng report vẫn được lưu với `WasPushed = false`

### File history bị corrupt

Extension tự động backup sang `.bak` và tạo mới. Xem log trong Output Window.

### Settings không lưu

- Kiểm tra quyền ghi vào `%APPDATA%\DhCodetaskExtension\`
- Thử đổi `StoragePath` sang thư mục khác trong Settings

### Webhook không gửi được

- Lỗi webhook được log vào Output Window và retry 3 lần
- Webhook lỗi **không** block UI — task vẫn được hoàn thành
- Kiểm tra `WebhookUrl` có đúng không, server có nhận POST không

---

## Changelog

### v3.0

- Provider Pattern cho mọi integration point
- TODO Time Tracking với timer độc lập từng item
- Completion Report (JSON + Markdown) lưu theo cấu trúc năm/tháng
- History Browser với lọc ngày/tuần/tháng, phân trang, export CSV
- EventBus non-blocking (Task.Run per subscriber)
- Settings Dialog 2 tab: Form + JSON Editor với validate và diff logging
- CompletionReport immutable (Builder pattern + private setters)
- Atomic file write dùng `File.Replace` (NTFS OS-level swap)
- Webhook notification với retry exponential backoff
- Restore in-progress task khi mở lại VS (InfoBar với Resume/Dismiss)
- GitService đọc `GitUserName`/`GitUserEmail` từ settings (không hardcode)

---

_DhCodetaskExtension v3.0 — Gitea-first · Provider Pattern · TODO Time Tracking · EventBus · Completion Report · History Browser_
