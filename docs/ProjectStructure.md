# Project Structure

## 1. Mục tiêu dự án

`DH Codetask Extension` là Visual Studio extension dành cho Visual Studio 2017, tập trung vào 4 luồng chính:

1. **Fetch task** từ Gitea hoặc nhập thủ công.
2. **Theo dõi thời gian làm việc** cho task chính và từng TODO.
3. **Hoàn tất công việc** bằng commit/push Git và sinh report.
4. **Tra cứu lịch sử** các report đã lưu.

Tài liệu này mô tả cấu trúc thư mục theo **vai trò thực tế trong code**, không chỉ liệt kê file.

---

## 2. Tổng quan cấu trúc thư mục

```text
DH Codetask Extension/
├── DhCodetaskExtension.sln        # Solution Visual Studio
├── package.json                   # Metadata phụ trợ của repo
├── README.md                      # Giới thiệu nhanh tính năng
├── CHANGELOG.md                   # Changelog chính
├── user_changelog.md              # Ghi chú thay đổi hướng người dùng
├── docs/                          # Tài liệu đặc tả, rule, task note
├── ProjectStructure.md            # Tài liệu cấu trúc dự án này
└── src/
    ├── DhCodetaskExtension.csproj
    ├── DevTaskTrackerPackage.cs   # Entry point chính của phiên bản tracker v3
    ├── DhCodetaskPackage.cs       # Package cũ/legacy cho cửa sổ main/settings
    ├── PackageGuids.cs
    ├── CommandTable.vsct
    ├── source.extension.vsixmanifest
    ├── Commands/                  # Khai báo command mở window/dialog
    ├── Core/
    │   ├── Events/                # Event contract cho vòng đời task/report
    │   ├── Interfaces/            # Interface cho provider/service
    │   ├── Models/                # Model domain: task, todo, worklog, report...
    │   └── Services/              # Service lõi: event bus, timer, logger, file helper
    ├── Providers/                 # Adapter tích hợp bên ngoài và persistence
    │   ├── GitProviders/
    │   ├── NotificationProviders/
    │   ├── ReportProviders/
    │   ├── StorageProviders/
    │   └── TaskProviders/
    ├── Resources/                 # Icon/asset đóng gói extension
    ├── Services/                  # Service cho Visual Studio shell / config UI
    ├── ToolWindows/               # XAML UI và code-behind của tool window/dialog
    └── ViewModels/                # ViewModel điều phối luồng thao tác UI
```

---

## 3. Phân tầng kiến trúc thực tế

## 3.1. Tầng khởi tạo package

### `src/DevTaskTrackerPackage.cs`

Đây là **entry point chính** của luồng DevTask Tracker mới.

Nó chịu trách nhiệm:

- khởi tạo `OutputWindowService`, `StatusBarService`;
- load `AppSettings` qua `JsonStorageService`;
- dựng các service lõi như `EventBus`, `HistoryQueryService`, `GitService`, `TimeTrackingService`;
- đăng ký các provider task/report/webhook;
- tạo `TrackerViewModel` và `HistoryViewModel`;
- khôi phục task đang làm dở từ `current-task.json` nếu tồn tại;
- đăng ký command và tool window với Visual Studio.

Nói ngắn gọn: file này là **composition root** của phiên bản tracker hiện tại.

### `src/DhCodetaskPackage.cs`

Đây là package cũ/legacy, vẫn giữ các service kiểu cấu hình chung (`ConfigurationService`, `JsonConfigService`) và `MainToolWindow`. Khi đọc code, nên xem file này là **nhánh hỗ trợ tương thích** hơn là trung tâm của luồng tracker mới.

---

## 3.2. Tầng domain lõi — `src/Core/`

Thư mục `Core` chứa các thành phần ít phụ thuộc UI nhất.

### `Core/Models/`

Các model chính:

- `TaskItem`: thông tin task fetch được.
- `TodoItem`: một đầu việc con trong task.
- `TimeSession`: phiên thời gian start/pause/stop.
- `WorkLog`: snapshot tạm thời để restore task đang làm dở.
- `CompletionReport`: dữ liệu report hoàn chỉnh sau khi hoàn tất hoặc lưu tạm.
- `AppSettings`: cấu hình Gitea, Git, storage, webhook...

### `Core/Interfaces/`

Định nghĩa hợp đồng giữa ViewModel và các adapter:

- `ITaskProvider`: fetch task từ URL.
- `IStorageService`: lưu settings, current task, archive report.
- `IGitService`: commit/push và đọc branch.
- `IReportGenerator`: sinh output report.
- `IHistoryRepository`: đọc/xóa lịch sử report.
- `IEventBus`: pub/sub sự kiện nội bộ.

Ý nghĩa kiến trúc: ViewModel phụ thuộc vào interface, còn implementation cụ thể nằm ở `Providers/` hoặc `Core/Services/`.

### `Core/Events/`

Chứa các event như:

- `TaskFetchedEvent`
- `TaskStartedEvent`
- `TaskPausedEvent`
- `TaskResumedEvent`
- `TaskCompletedEvent`
- `CommitPushedEvent`
- `ReportSavedEvent`

Các event này giúp package, ViewModel và notification provider trao đổi mà không buộc ghép cứng trực tiếp.

### `Core/Services/`

Các service hạ tầng lõi:

- `EventBus`: triển khai pub/sub trong memory.
- `TimeTrackingService`: giữ trạng thái timer, sessions, elapsed time.
- `HistoryQueryService`: đọc cache lịch sử report JSON và watch thay đổi file.
- `AtomicFile`: ghi file an toàn kiểu temp + replace.
- `CommitMessageGenerator`: sinh commit message từ task + business logic.
- `AppLogger`: logger hỗ trợ code-behind WPF.

---

## 3.3. Tầng adapter/provider — `src/Providers/`

Đây là nơi nối domain với thế giới bên ngoài.

### `Providers/TaskProviders/`

Phụ trách nhận URL task và trả về `TaskItem`.

- `GiteaTaskProvider.cs`: parse URL issue Gitea, gọi API `/api/v1/repos/{owner}/{repo}/issues/{number}`, ánh xạ JSON sang `TaskItem`.
- `ManualTaskProvider.cs`: fallback cho trường hợp nhập tay.

Luồng dùng thực tế:
`TrackerViewModel.FetchAsync()` → `FetchTaskFunc` → `TaskProviderFactory.FetchAsync()` → provider phù hợp.

### `Providers/GitProviders/`

- `GitService.cs`: kiểm tra git khả dụng, tìm repo root, lấy branch hiện tại, chạy `git add -A`, `git commit`, và `git push` nếu bật auto push.

Luồng dùng thực tế:
`Push & Complete` trong `TrackerViewModel` → `IGitService.PushAndCompleteAsync()`.

### `Providers/StorageProviders/`

- `JsonStorageService.cs`: lưu `settings.json`, `current-task.json`, và archive report JSON vào `history/YYYY/MM/`.

Vai trò rất quan trọng vì đây là lớp giúp:

- restore task dang dở khi khởi động lại VS;
- tách dữ liệu runtime ra khỏi UI;
- tạo nền cho màn hình History.

### `Providers/ReportProviders/`

- `JsonReportGenerator.cs`: sinh report JSON.
- `MarkdownReportGenerator.cs`: sinh report Markdown dễ đọc cho người dùng.
- `CompositeReportGenerator.cs`: chạy nhiều generator theo kiểu open/closed.

Luồng dùng thực tế:
`TrackerViewModel.CompleteFlowAsync()` → `_reportGenerator.GenerateAsync(report, histDir)`.

### `Providers/NotificationProviders/`

- `WebhookNotificationProvider.cs`: nhận event hoàn tất task rồi POST webhook ra ngoài.

Trong package, `TaskCompletedEvent` được subscribe để provider này chạy tự động.

---

## 3.4. Tầng điều phối UI — `src/ViewModels/`

### `TrackerViewModel.cs`

Đây là **trung tâm nghiệp vụ của extension**.

Nó quản lý gần như toàn bộ thao tác người dùng trong màn hình tracker:

- nhập URL task;
- fetch task;
- start/pause/resume/stop timer;
- thêm/xóa/cập nhật TODO;
- sinh commit message;
- autosave work log mỗi 30 giây;
- restore work log;
- thực hiện flow hoàn tất hoặc lưu tạm.

Có thể xem `TrackerViewModel` là lớp orchestration chính nối:

- UI (`TrackerControl`),
- domain (`TaskItem`, `CompletionReport`, `TodoItem`),
- storage,
- git,
- report,
- event bus.

### `TodoItemViewModel.cs`

Bọc từng `TodoItem` để cho phép:

- start/pause timer riêng cho TODO;
- complete TODO;
- phát event theo trạng thái TODO;
- cập nhật summary ngược về tracker.

### `HistoryViewModel.cs`

Phụ trách màn hình lịch sử:

- nạp report theo Today / ThisWeek / ThisMonth / Custom;
- filter theo từ khóa;
- phân trang;
- tính summary tổng task, tổng giờ, TODO done rate;
- xóa report;
- mở file markdown/json chi tiết.

---

## 3.5. Tầng giao diện — `src/ToolWindows/`

Thư mục này chia làm 2 loại:

### Tool window chính

- `TrackerControl.xaml` + `.cs`: màn hình theo dõi task đang làm.
- `HistoryControl.xaml` + `.cs`: màn hình xem lịch sử.
- `MainToolWindowControl.xaml` + `.cs`: main window cũ/legacy.
- `MainToolWindow.cs`, `DevTaskToolWindows.cs`: đăng ký và dựng tool window.

### Dialog cấu hình/chi tiết

- `TaskSettingsDialog.xaml` + `.cs`: cấu hình task/Gitea/Git/storage/webhook.
- `SettingsDialog.xaml` + `.cs`: settings cũ/legacy.
- `JsonSettingsDialog.xaml` + `.cs`: chỉnh JSON settings.
- `ReportDetailDialog.xaml` + `.cs`: xem chi tiết report.

Code-behind trong thư mục này chủ yếu làm 3 việc:

- nối event UI với command/ViewModel;
- bắt lỗi UI để tránh tool window bị treo;
- xử lý tương tác WPF đặc thù như click, double click, export CSV, mở dialog.

---

## 3.6. Tầng tích hợp Visual Studio

### `src/Commands/`

Các command menu của Visual Studio:

- mở tracker window;
- mở history window;
- mở task settings;
- mở settings/json settings/main window.

### `src/Services/`

Các service gắn với Visual Studio shell:

- `OutputWindowService`: ghi log ra Output pane riêng.
- `StatusBarService`: cập nhật status bar.
- `OptionsService`, `ConfigurationService`, `JsonConfigService`: lớp cấu hình kiểu VS/package.

Nhóm này không chứa nghiệp vụ task chính, nhưng rất quan trọng cho trải nghiệm vận hành extension trong IDE.

---

## 4. Luồng thao tác chính và file liên quan

## 4.1. Luồng khởi động extension

```text
Visual Studio load package
    → DevTaskTrackerPackage.InitializeAsync()
    → init services + settings + providers + viewmodels
    → register commands/tool windows
    → load current-task.json nếu có
    → TrackerViewModel.RestoreFromLogAsync()
```

### File tham gia

- `src/DevTaskTrackerPackage.cs`
- `src/Providers/StorageProviders/JsonStorageService.cs`
- `src/ViewModels/TrackerViewModel.cs`

### Ý nghĩa

Luồng này đảm bảo extension có thể **khôi phục bối cảnh làm việc dở dang** thay vì bắt đầu lại từ đầu khi mở lại Visual Studio.

---

## 4.2. Luồng fetch task từ Gitea

```text
Người dùng nhập URL issue
    → TrackerViewModel.FetchCommand
    → FetchAsync()
    → TaskProviderFactory chọn GiteaTaskProvider
    → gọi Gitea API
    → map dữ liệu thành TaskItem
    → đổ dữ liệu lên UI + regenerate commit message
```

### File tham gia

- `src/ViewModels/TrackerViewModel.cs`
- `src/Providers/TaskProviders/GiteaTaskProvider.cs`
- `src/Core/Models/TaskItem.cs`
- `src/Core/Services/CommitMessageGenerator.cs`

### Điểm cần lưu ý

- Provider chỉ xử lý URL phù hợp với `GiteaBaseUrl` trong settings.
- Description được strip markdown và cắt ngắn trước khi đưa vào UI.
- Sau fetch thành công, ViewModel còn thử detect Git repo root.

---

## 4.3. Luồng time tracking task chính

```text
Start
    → TimeTrackingService.Start()
Pause
    → pause toàn bộ TODO đang chạy
    → TimeTrackingService.Pause()
    → autosave current task
Resume
    → TimeTrackingService.Resume()
Stop / Complete
    → TimeTrackingService.Stop()
```

### File tham gia

- `src/ViewModels/TrackerViewModel.cs`
- `src/Core/Services/TimeTrackingService.cs`
- `src/Core/Models/TimeSession.cs`

### Điểm cần lưu ý

- `TrackerViewModel` có `DispatcherTimer` cập nhật đồng hồ UI mỗi giây.
- Khi pause hoặc autosave chu kỳ 30 giây, trạng thái hiện tại được serialize thành `WorkLog`.

---

## 4.4. Luồng TODO tracking

```text
Add TODO
    → TrackerViewModel.AddTodo()
    → tạo TodoItem + TodoItemViewModel
Start/Pause TODO
    → TodoItemViewModel quản lý timer riêng
Complete TODO
    → cập nhật trạng thái done
TrackerViewModel
    → tổng hợp TodoTotal / TodoDone / TodoRunning / TodoTotalElapsed
```

### File tham gia

- `src/ViewModels/TrackerViewModel.cs`
- `src/ViewModels/TodoItemViewModel.cs`
- `src/Core/Models/TodoItem.cs`

### Ý nghĩa

TODO trong dự án này không chỉ là checklist. Mỗi TODO còn có **thời gian làm việc riêng**, phục vụ báo cáo cuối cùng.

---

## 4.5. Luồng hoàn tất công việc / lưu tạm

```text
Push & Complete / Save & Pause
    → pause tất cả TODO đang chạy
    → stop timer task chính
    → nếu push: git add/commit/(push)
    → tạo CompletionReport
    → archive JSON
    → generate Markdown/JSON report
    → publish events
    → clear current-task.json nếu hoàn tất thật
    → clear UI nếu push complete
```

### File tham gia

- `src/ViewModels/TrackerViewModel.cs`
- `src/Providers/GitProviders/GitService.cs`
- `src/Providers/StorageProviders/JsonStorageService.cs`
- `src/Providers/ReportProviders/CompositeReportGenerator.cs`
- `src/Providers/ReportProviders/MarkdownReportGenerator.cs`
- `src/Core/Models/CompletionReport.cs`

### Kết quả đầu ra

Report được lưu theo cấu trúc:

```text
{StoragePath or %AppData%/DhCodetaskExtension}/history/YYYY/MM/
  ├── <timestamp>_<taskId>_<slug>.json
  └── <timestamp>_<taskId>_<slug>.md
```

---

## 4.6. Luồng tra cứu lịch sử

```text
Mở History window
    → HistoryControl Loaded
    → HistoryViewModel.RefreshAsync()
    → HistoryQueryService đọc cache report JSON
    → map sang CompletionReportSummary
    → filter / phân trang / summary
    → click item để xem detail hoặc mở file markdown
```

### File tham gia

- `src/ToolWindows/HistoryControl.xaml.cs`
- `src/ViewModels/HistoryViewModel.cs`
- `src/Core/Services/HistoryQueryService.cs`
- `src/ToolWindows/ReportDetailDialog.xaml.cs`

### Điểm cần lưu ý

- `HistoryQueryService` dùng `FileSystemWatcher` để đánh dấu cache dirty khi có file mới/xóa.
- Export CSV được xử lý ở `HistoryControl.xaml.cs`.

---

## 5. Ý nghĩa của việc tách thư mục hiện tại

## 5.1. `Core` tách khỏi `Providers`

Giúp phân biệt rõ:

- **cái gì là luật chơi của hệ thống** (`Models`, `Interfaces`, `Events`),
- **cái gì là cách hiện thực cho môi trường cụ thể** (Gitea, Git, JSON storage, webhook).

Nhờ vậy có thể mở rộng sang Jira/Linear/GitLab mà ít chạm vào ViewModel.

## 5.2. `ViewModels` tách khỏi `ToolWindows`

Giúp logic thao tác không bị nhúng thẳng vào code-behind WPF.

- `ToolWindows`: lo hiển thị và event UI.
- `ViewModels`: lo trạng thái và orchestration nghiệp vụ.

Đây là điểm then chốt để code không bị rối khi số lượng nút/tính năng tăng lên.

## 5.3. `Services` tách làm 2 lớp

- `Core/Services`: service độc lập với Visual Studio.
- `src/Services`: service gắn với Visual Studio shell.

Việc tách này hợp lý vì timer, event bus, history query có thể tái sử dụng; còn output window/status bar thì chỉ sống trong môi trường VS extension.

---

## 6. Những điểm dễ gây nhầm khi đọc repo

## 6.1. Có hai package class

Repo đang tồn tại đồng thời:

- `DevTaskTrackerPackage.cs` — package chính cho luồng tracker v3.
- `DhCodetaskPackage.cs` — package cũ/legacy.

Khi phân tích luồng fetch task, time tracking, report, history thì nên ưu tiên đọc `DevTaskTrackerPackage.cs` trước.

## 6.2. `src/Services/` không phải service nghiệp vụ chính

Nhiều người dễ nghĩ toàn bộ business logic nằm ở `src/Services/`, nhưng thực tế phần nghiệp vụ quan trọng lại nằm trong:

- `src/ViewModels/`
- `src/Core/Services/`
- `src/Providers/`

## 6.3. Report được ghi ở hai bước

Trong flow hoàn tất:

1. `JsonStorageService.ArchiveReportAsync()` lưu JSON archive và set file path.
2. `CompositeReportGenerator` tiếp tục sinh thêm output như Markdown/JSON format khác.

Vì vậy khi debug report thiếu file, cần kiểm tra cả storage lẫn report generator.

---

## 7. Gợi ý thứ tự đọc code cho người mới vào dự án

Nếu cần onboard nhanh, nên đọc theo thứ tự này:

1. `README.md` — hiểu mục tiêu extension.
2. `src/DevTaskTrackerPackage.cs` — hiểu hệ thống được ghép từ các thành phần nào.
3. `src/ViewModels/TrackerViewModel.cs` — hiểu luồng thao tác cốt lõi.
4. `src/Providers/TaskProviders/GiteaTaskProvider.cs` — hiểu đầu vào task.
5. `src/Providers/GitProviders/GitService.cs` — hiểu đầu ra Git completion.
6. `src/Providers/StorageProviders/JsonStorageService.cs` — hiểu persistence.
7. `src/ViewModels/HistoryViewModel.cs` + `src/Core/Services/HistoryQueryService.cs` — hiểu history/report.
8. `src/ToolWindows/*.xaml` và `.cs` — hiểu UI binding và hành vi người dùng.

---

## 8. Kết luận

Cấu trúc dự án hiện tại được tổ chức theo hướng:

- **Package** để khởi tạo và lắp ráp hệ thống.
- **ViewModel** để điều phối nghiệp vụ.
- **Core** để giữ model/interface/service nền tảng.
- **Providers** để giao tiếp với hệ thống ngoài.
- **ToolWindows/Commands/Services** để tích hợp vào môi trường Visual Studio.

Điểm mạnh của cấu trúc này là bám khá sát các luồng thao tác thật của người dùng: **fetch task → làm việc & tracking → complete/report → history**. Vì vậy nếu tiếp tục mở rộng tính năng, nên giữ nguyên trục tổ chức này thay vì dồn logic ngược lại vào code-behind hoặc package.
