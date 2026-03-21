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

**Fix:** Thêm flag `private bool _isInitialized` — set `true` sau khi `InitializeComponent()` hoàn tất. Tất cả Filter_* handlers kiểm tra `if (!_isInitialized) return;` trước khi truy cập named elements.

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
