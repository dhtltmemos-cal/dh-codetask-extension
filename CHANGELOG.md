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

**Nguyên nhân:** Yêu cầu từ `docs/tasks/task-2026-03-21-00.md`:
1. Không muốn duy trì 2 cách nhập cấu hình (form + JSON) — gây không nhất quán và khó bảo trì. Cấu hình duy nhất từ `settings.json`.
2. Thiếu visibility khi extension hoạt động — mọi thao tác user phải có trace trong Output Window để debug.
3. Cần quick-access vào file log và file config mà không cần mở Explorer.

**Thay đổi:**

### Loại bỏ Settings form
- `CommandTable.vsct`: Xóa `CmdIdSettings` (0x0400), thêm `UtilityMenuGroup` (0x1700)
- `PackageGuids.cs`: Thêm `CmdIdOpenLogFile` (0x0700), `CmdIdOpenConfigFile` (0x0800), `UtilityMenuGroup` (0x1700); xóa comment `CmdIdSettings`
- `Commands/ShowSettings.cs`: Stub rỗng — command 0x0400 đã xóa khỏi menu
- `Commands/ShowJsonSettings.cs`: Nối `CmdIdJsonSettings` → `OpenSettings()` (AppSettingsJsonDialog)
- `Commands/ShowHistoryAndSettings.cs`: `ShowTaskSettings` nối → `OpenSettings()` (AppSettingsJsonDialog)
- `ToolWindows/AppSettingsJsonDialog.xaml/.cs` (**mới**): JSON editor cho `AppSettings` — Format, Validate, Reset, Reload, Open File; validate GiteaBaseUrl + WebhookUrl; save callback → `JsonStorageService`
- `DevTaskTrackerPackage.OpenSettings()`: Mở `AppSettingsJsonDialog` thay vì form dialog; log khi mở/lưu
- `Providers/StorageProviders/JsonStorageService.cs`: Thêm `GetSettingsFilePath()` — expose path để dialog và Package dùng

### Logging mọi thao tác user
- `ViewModels/TrackerViewModel.cs`: Thêm `_log()` tại Start, Pause, Resume, Stop, Fetch OK/Fail, Add TODO, Delete TODO, Clear, CompleteFlowAsync (git push OK/fail, report saved), AutoSave, Restore
- `ToolWindows/TrackerControl.xaml.cs`: Log từng button click qua `AppLogger.Instance.Info`
- `DevTaskTrackerPackage.cs`: Log OpenSettings, OpenLogFile, OpenConfigFile, Init sequence, Restore
- `AppLogger.Instance.Init(OutputWindow)` gọi ngay sau khi `OutputWindow.InitializeAsync()` → mọi log từ sau đó xuất hiện trong Output Window

### Open Log / Open Config buttons và menu items
- `Commands/OpenLogAndConfig.cs` (**mới**): `OpenLogFileCommand` (0x0700), `OpenConfigFileCommand` (0x0800)
- `DevTaskTrackerPackage.OpenLogFile()`: Tìm file log hôm nay (`devtask_YYYYMMDD.log`), fallback newest, `Process.Start(path)`, log path vào Output Window
- `DevTaskTrackerPackage.OpenConfigFile()`: `Process.Start(settings.json)`, auto-create nếu chưa có
- `ToolWindows/TrackerControl.xaml`: Thêm utility row cuối panel — nút "📄 Open Log" + "📋 Open Config"
- `ToolWindows/TrackerControl.xaml.cs`: Handler `BtnOpenLog_Click`, `BtnOpenConfig_Click` với logging
- `TrackerViewModel`: Thêm `OpenLogFileAction`, `OpenConfigFileAction` (Action delegates, set bởi Package)
- `DhCodetaskExtension.csproj`: Đăng ký `AppSettingsJsonDialog.xaml/.cs`, `Commands/OpenLogAndConfig.cs`
- Version bump: 3.0 → 3.1

### Tuân thủ error-skill-devtasktracker.md
- Wiring: `OpenLogFileAction`/`OpenConfigFileAction` set trong Package trước khi VM được dùng
- Settings: không hard-code path — `GetStorageRoot()` + `GetSettingsFilePath()` nhất quán
- Async: không async void ngoài event handler; `TryCatch` wrap mọi button handler
