## 2026-03-22 - Solution File Browser: quét .sln và .csproj trong DirectoryRootDhHosCodePath với cache TTL

**Nguyên nhân:** Yêu cầu từ `docs/tasks/task-2026-03-22-00.md` — thêm panel danh sách file .sln/.csproj để mở nhanh, sắp xếp a-z, lọc theo tên, với cơ chế cache tái sử dụng.

**Files:**

- `src/Core/Models/AppSettings.cs`: thêm `DirectoryRootDhHosCodePath`, `SolutionFileCacheMinutes`
- `src/Core/Models/SolutionFileEntry.cs`: model mới cho file entry
- `src/Core/Services/SolutionFileService.cs`: scan đệ quy, cache JSON với TTL, filter in-memory
- `src/ToolWindows/TrackerControl.xaml`: Expander panel "📁 Solution / Project Files" với search, list, open, copy path
- `src/ToolWindows/TrackerControl.xaml.cs`: lazy load khi expand, click mở file, copy path

## 2026-03-22 - Report Checksum: SHA-256 để phát hiện can thiệp từ bên ngoài

**Nguyên nhân:** Yêu cầu từ `docs/tasks/task-2026-03-22-00.md` — đảm bảo toàn vẹn dữ liệu report JSON.

**Files:**

- `src/Core/Services/ChecksumHelper.cs`: compute/verify SHA-256
- `src/Core/Models/CompletionReport.cs`: thêm field `Checksum`, `ChecksumValid`, `SetChecksum()`, `VerifyChecksum()`
- `src/Providers/StorageProviders/JsonStorageService.cs`: tính checksum khi archive report
- `src/Core/Services/HistoryQueryService.cs`: xác minh checksum khi load report từ file
- `src/ViewModels/HistoryViewModel.cs`: expose `ChecksumDisplay`, `ChecksumTooltip`
- `src/ToolWindows/HistoryControl.xaml`: hiển thị trạng thái checksum ✅/⚠ trên mỗi row
- `src/Providers/ReportProviders/MarkdownReportGenerator.cs`: in SHA-256 vào section cuối file .md

## 2026-03-22 - URL Duplicate Check: kiểm tra lịch sử trước khi fetch Gitea

**Nguyên nhân:** Yêu cầu từ `docs/tasks/task-2026-03-22-00.md` — tránh fetch lại task đã có trong lịch sử.

**Files:**

- `src/ViewModels/TrackerViewModel.cs`: thêm `LoadAllHistoryFunc`, `NormalizeIssueUrl()`, kiểm tra trùng URL trước khi gọi provider
- `src/DevTaskTrackerPackage.cs`: wire `LoadAllHistoryFunc`

## 2026-03-22 - Redesign Task Timer: chỉ Bắt đầu và Tạm ngưng với lý do

**Nguyên nhân:** Yêu cầu từ `docs/tasks/task-2026-03-22-00.md` — giao diện đơn giản hơn, khi Tạm ngưng ghi nhận lý do.

**Files:**

- `src/ToolWindows/TrackerControl.xaml`: thay 4 nút Start/Pause/Resume/Stop thành 2 nút "▶ Bắt đầu" và "⏸ Tạm ngưng"
- `src/ToolWindows/PauseReasonDialog.xaml/.cs`: dialog mới chọn lý do tạm ngưng
- `src/Core/Models/TimeSession.cs`: thêm field `PauseReason`
- `src/Core/Services/TimeTrackingService.cs`: `Pause(reason)` lưu lý do vào session
- `src/Core/Models/AppSettings.cs`: thêm `TaskPauseReasons` list
- `src/ViewModels/TrackerViewModel.cs`: `PauseCommand` → `PauseTaskAsync()` gọi `ShowPauseReasonDialog`

## 2026-03-22 - Redesign TODO: Bắt đầu / Tạm ngưng / Kết thúc

**Nguyên nhân:** Yêu cầu từ `docs/tasks/task-2026-03-22-00.md` — task con cần rõ ràng hơn, đặc biệt nút Kết thúc.

**Files:**

- `src/ViewModels/TodoItemViewModel.cs`: thêm `StopCommand` (Kết thúc), `StopTodo()` đánh dấu done, lưu session
- `src/ToolWindows/TrackerControl.xaml`: TODO item row dùng ▶/⏸/✓/🗑

## 2026-03-22 - TODO Templates: chọn nhanh task con từ cấu hình

**Nguyên nhân:** Yêu cầu từ `docs/tasks/task-2026-03-22-00.md` — danh sách TODO mẫu để thêm nhanh.

**Files:**

- `src/Core/Models/AppSettings.cs`: thêm `TodoTemplates` list
- `src/ViewModels/TrackerViewModel.cs`: expose `TodoTemplates` collection, `AddTodoFromTemplateCommand`
- `src/ToolWindows/TrackerControl.xaml`: Expander "📋 Chọn nhanh từ mẫu" hiện khi có template

**Version bump:** 3.2 → 3.3

# Changelog

## 2026-03-21 - Đổi tên project: Rename toàn bộ từ VS2017ExtensionTemplate sang dh-codetask-extension

**Nguyên nhân:** Code gốc lấy từ template chưa được đặt tên phù hợp với dự án thực tế.

**Thay đổi:**

- Namespace `VS2017ExtensionTemplate` → `DhCodetaskExtension`
- Class `MyPackage` → `DhCodetaskPackage`

## 2026-03-21 - Thêm top-level menu: Bổ sung menu "DH Codetask Extension" trên menu bar Visual Studio

**Nguyên nhân:** Người dùng yêu cầu có menu cha riêng trên menu bar VS.

## 2026-03-21 - Triển khai DevTaskTracker v3.0: Gitea Task Tracking, Time Tracking, TODO, Reports, History Browser

**Nguyên nhân:** Theo yêu cầu tại docs/Instructions.md.

**Thay đổi:** Provider Pattern, EventBus, TimeTrackingService, CompletionReport immutable, HistoryBrowser, AppLogger, AtomicFile write.

## 2026-03-21 - Sửa lỗi build CS8179/CS8137 ValueTuple không hỗ trợ .NET 4.6

**File:** `src\Providers\GitProviders\GitService.cs`

## 2026-03-21 - Sửa lỗi build CS8314 pattern matching generic type C# 7.0

**File:** `src\Providers\NotificationProviders\WebhookNotificationProvider.cs`

## 2026-03-21 - Sửa warning VSTHRD103 sync block ManualTaskProvider

**File:** `src\Providers\TaskProviders\ManualTaskProvider.cs`

## 2026-03-21 - Sửa warning VSTHRD101 async lambda trên void delegate

**Files:** HistoryViewModel, TrackerViewModel, HistoryControl

## 2026-03-21 - Sửa lỗi runtime tool window hiển thị "Working on it..."

**Files:** DevTaskToolWindows.cs, DevTaskTrackerPackage.cs

## 2026-03-21 - Sửa lỗi runtime InvalidOperationException WPF binding TwoWay trên readonly property

**File:** TrackerControl.xaml — thêm `Mode=OneWay` cho computed properties.

## 2026-03-21 - Thêm AppLogger: ghi log đồng thời ra file và VS Output Window

**File mới:** `src\Core\Services\AppLogger.cs`

## 2026-03-21 - Loại bỏ Settings form, chỉ giữ Settings JSON: AppSettingsJsonDialog + logging + Open Log/Config buttons

**Nguyên nhân:** Yêu cầu từ `docs/tasks/task-2026-03-21-00.md`

**Thay đổi:** AppSettingsJsonDialog thay TaskSettingsDialog, mọi thao tác log ra Output Window, thêm Open Log/Config buttons và menu items. Version 3.1.

## 2026-03-21 - Sửa lỗi NullReferenceException trong HistoryControl Filter_Week

**Nguyên nhân:** RadioButton với `IsChecked="True"` trong XAML bắn event `Checked` trong `InitializeComponent()` trước khi `CustomRangePanel` được gán. Tương tự cho Filter_Today, Filter_Month, Filter_Custom.

**File:** `src\ToolWindows\HistoryControl.xaml.cs`

**Fix:** Thêm flag `private bool _isInitialized` — set `true` sau khi `InitializeComponent()` hoàn tất. Tất cả Filter\_\* handlers kiểm tra `if (!_isInitialized) return;` trước khi truy cập named elements.

## 2026-03-21 - Thêm tính năng Resume task từ lịch sử

**Nguyên nhân:** Yêu cầu từ `docs/tasks/task-2026-03-21-01.md`

**Files:**

- `src\ViewModels\HistoryViewModel.cs`: thêm `ResumeFromHistoryAction`, `ResumeCommand`
- `src\ToolWindows\HistoryControl.xaml`: thêm nút "▶ Resume" mỗi row
- `src\ToolWindows\HistoryControl.xaml.cs`: handler `BtnResume_Click`
- `src\DevTaskTrackerPackage.cs`: wire `ResumeFromHistoryAction` → `BuildWorkLogFromReport()` → `RestoreFromLogAsync()` → `ShowTrackerWindowAsync()`

## 2026-03-21 - Thêm tính năng Open URL trong cả 2 panel và ReportDetailDialog

**Nguyên nhân:** Yêu cầu từ `docs/tasks/task-2026-03-21-01.md`

**Files:**

- `src\ToolWindows\TrackerControl.xaml`: nút "🔗" trong URL bar
- `src\ToolWindows\TrackerControl.xaml.cs`: handler `BtnOpenUrl_Click`
- `src\ToolWindows\HistoryControl.xaml`: nút "🔗 URL" mỗi row
- `src\ToolWindows\HistoryControl.xaml.cs`: handler `BtnOpenUrl_Click`
- `src\ViewModels\HistoryViewModel.cs`: thêm `OpenUrlAction`, `OpenUrlCommand`
- `src\ToolWindows\ReportDetailDialog.xaml`: nút "🔗 URL" action row
- `src\ToolWindows\ReportDetailDialog.xaml.cs`: handler `BtnOpenUrl_Click`
- `src\DevTaskTrackerPackage.cs`: wire `_historyVm.OpenUrlAction`

## 2026-03-21 - Bật Topmost cho các dialog cấu hình và chi tiết report

**Nguyên nhân:** Yêu cầu từ `docs/tasks/task-2026-03-21-01.md` — dialog mất focus khi chuyển window

**Files:**

- `src\ToolWindows\AppSettingsJsonDialog.xaml`: thêm `Topmost="True"`
- `src\ToolWindows\ReportDetailDialog.xaml`: thêm `Topmost="True"`

**Version bump:** 3.1 → 3.2
