# Tài liệu bàn giao kỹ thuật cho team phát triển

> Mục tiêu của tài liệu này là giúp các thành viên có nền tảng C# nhưng **chưa từng làm Visual Studio extension / WPF tool window** vẫn có thể đọc code, debug và tiếp tục phát triển ứng dụng này một cách an toàn.

## 1. Ứng dụng này thực chất là gì?

Đây là một **Visual Studio 2017 extension**. Khi extension được cài vào Visual Studio, package của extension sẽ được Visual Studio load, sau đó package sẽ:

1. khởi tạo service nội bộ;
2. load cấu hình và dữ liệu tạm;
3. đăng ký menu command;
4. tạo các tool window/panel như **DevTask Tracker** và **Task History**;
5. nối UI WPF với ViewModel để xử lý logic.

Nếu team quen với WinForms/WPF C#, hãy hiểu ngắn gọn như sau:

- `DevTaskTrackerPackage.cs` = composition root / bootstrap của extension;
- `Commands/*.cs` = menu command của Visual Studio;
- `ToolWindows/*.xaml` + `.cs` = giao diện panel/dialog;
- `ViewModels/*.cs` = logic thao tác chính khi user click UI;
- `Providers/*.cs` = lớp tích hợp file, git, webhook, Gitea API;
- `Core/*.cs` = model, event bus, timer, service lõi.

---

## 2. File nào là entry point thật sự?

### 2.1. Entry point đang dùng cho luồng hiện tại

File quan trọng nhất là:

- `src/DevTaskTrackerPackage.cs`

Đây là package chính của tracker v3.1. Khi Visual Studio load extension, method `InitializeAsync(...)` trong file này sẽ chạy đầu tiên cho luồng DevTask Tracker.

### 2.2. Package legacy còn tồn tại

Ngoài ra còn có:

- `src/DhCodetaskPackage.cs`

File này là package cũ/legacy. Nó vẫn tồn tại để giữ các command/tool window đời cũ (`MainToolWindow`, settings cũ...), nhưng **luồng nghiệp vụ chính hiện nay nằm ở `DevTaskTrackerPackage.cs`**.

### 2.3. Cần đọc file nào đầu tiên nếu mới vào dự án?

Khuyến nghị thứ tự đọc:

1. `src/DevTaskTrackerPackage.cs`
2. `src/Commands/ShowTrackerWindow.cs`
3. `src/Commands/ShowHistoryAndSettings.cs`
4. `src/ToolWindows/DevTaskToolWindows.cs`
5. `src/ToolWindows/TrackerControl.xaml`
6. `src/ToolWindows/TrackerControl.xaml.cs`
7. `src/ViewModels/TrackerViewModel.cs`
8. `src/ViewModels/TodoItemViewModel.cs`
9. `src/ToolWindows/HistoryControl.xaml`
10. `src/ToolWindows/HistoryControl.xaml.cs`
11. `src/ViewModels/HistoryViewModel.cs`
12. `src/Providers/*`

Lý do: chuỗi này đi đúng từ **Visual Studio load package → menu command → mở panel → UI → ViewModel → provider/service**.

---

## 3. Luồng khởi động tổng thể của extension

## 3.1. Khi Visual Studio load extension

Luồng chính diễn ra trong `src/DevTaskTrackerPackage.cs`:

### Bước 1 - Khởi tạo service nền

Trong `InitializeAsync(...)`, package tạo:

- `OutputWindowService`
- `StatusBarService`
- `JsonStorageService`
- `EventBus`
- `HistoryQueryService`
- `GitService`
- `TimeTrackingService`

Ý nghĩa:

- `OutputWindowService`: ghi log ra Output Window của Visual Studio.
- `StatusBarService`: cập nhật status bar.
- `JsonStorageService`: đọc/ghi `settings.json`, `current-task.json`, `history/...`.
- `EventBus`: pub/sub event nội bộ.
- `HistoryQueryService`: đọc lịch sử report.
- `GitService`: chạy lệnh git.
- `TimeTrackingService`: quản lý start/pause/resume/stop timer task chính.

### Bước 2 - Load settings

Package gọi `await _storage.LoadSettingsAsync();` để đọc cấu hình từ JSON. Nếu file chưa tồn tại, service sẽ tự tạo file mặc định.

### Bước 3 - Tạo provider

Package đăng ký:

- `ManualTaskProvider`
- `GiteaTaskProvider`
- `JsonReportGenerator`
- `MarkdownReportGenerator`
- `WebhookNotificationProvider`

Tức là sau khi khởi động xong, hệ thống đã có đủ thành phần để:

- fetch task từ Gitea;
- nhập task thủ công;
- generate JSON report;
- generate Markdown report;
- bắn webhook khi task hoàn tất.

### Bước 4 - Tạo ViewModel

Package tạo hai ViewModel chính:

- `_trackerVm` từ `TrackerViewModel`
- `_historyVm` từ `HistoryViewModel`

Đây là hai object được truyền vào panel/tool window khi panel được mở.

### Bước 5 - Nối callback giữa UI và package

Package set các callback cho `TrackerViewModel`:

- `FetchTaskFunc`
- `OpenSettingsAction`
- `OpenHistoryAction`
- `OpenLogFileAction`
- `OpenConfigFileAction`

Điểm rất quan trọng: `TrackerViewModel` **không tự biết mở dialog/tool window**. Nó chỉ gọi các `Action`/`Func` do package bơm vào.

### Bước 6 - Đăng ký menu command

Trong đoạn cuối của `InitializeAsync(...)`, package gọi các hàm `InitializeAsync(...)` của command class để đăng ký menu vào Visual Studio.

### Bước 7 - Restore task đang làm dở

Sau khi đăng ký command, package thử gọi `LoadCurrentTaskAsync()`.

- Nếu có `current-task.json`, package gọi `_trackerVm.RestoreFromLogAsync(existing)`.
- Tức là lần mở Visual Studio tiếp theo có thể khôi phục task đang làm dở.

---

## 4. Menu nào mở panel nào? command nào chạy file nào?

Phần menu được khai báo ở:

- `src/CommandTable.vsct`
- `src/PackageGuids.cs`

### 4.1. Menu top-level

Extension tạo menu:

- `DH Codetask Extension`

Trong menu này có các command chính:

- `DevTask Tracker...`
- `Task History...`
- `⚙ Settings (JSON)...`
- `DevTask Settings...`
- `📄 Open Log File`
- `📋 Open Config File`

### 4.2. Mapping command → file xử lý

#### Mở panel DevTask Tracker

- Menu id: `ShowTrackerWindowId`
- File đăng ký command: `src/Commands/ShowTrackerWindow.cs`
- Hàm chạy khi click menu: `ShowTrackerWindow.Execute(...)`
- Hàm thực sự mở panel: `_package.ShowTrackerWindowAsync()` trong `src/DevTaskTrackerPackage.cs`

#### Mở panel Task History

- Menu id: `ShowHistoryWindowId`
- File đăng ký command: `src/Commands/ShowHistoryAndSettings.cs`
- Hàm chạy khi click menu: `ShowHistoryWindow.Execute(...)`
- Hàm thực sự mở panel: `_package.ShowHistoryWindowAsync()` trong `src/DevTaskTrackerPackage.cs`

#### Mở Settings JSON

Có 2 đường vào đang cùng dẫn về JSON settings:

1. Menu `⚙ Settings (JSON)...`
2. Menu `DevTask Settings...`

Các command này cuối cùng đều gọi:

- `_package.OpenSettings()` trong `src/DevTaskTrackerPackage.cs`

Dialog thực sự được mở ở:

- `src/ToolWindows/AppSettingsJsonDialog.xaml`
- `src/ToolWindows/AppSettingsJsonDialog.xaml.cs`

#### Mở file log

- File command: `src/Commands/OpenLogAndConfig.cs`
- Hàm command: `OpenLogFileCommand.Execute(...)`
- Hàm package được gọi: `_package.OpenLogFile()` trong `src/DevTaskTrackerPackage.cs`

#### Mở file config

- File command: `src/Commands/OpenLogAndConfig.cs`
- Hàm command: `OpenConfigFileCommand.Execute(...)`
- Hàm package được gọi: `_package.OpenConfigFile()` trong `src/DevTaskTrackerPackage.cs`

---

## 5. Khi mở panel thì class nào được tạo, sự kiện nào chạy?

## 5.1. Panel DevTask Tracker

### Chuỗi mở panel

Khi user click menu **DevTask Tracker...**, luồng là:

1. `src/Commands/ShowTrackerWindow.cs`
2. `ShowTrackerWindow.Execute(...)`
3. `_package.ShowTrackerWindowAsync()` trong `src/DevTaskTrackerPackage.cs`
4. `ShowToolWindowAsync(typeof(TrackerToolWindow), ...)`
5. Visual Studio tạo `TrackerToolWindow` trong `src/ToolWindows/DevTaskToolWindows.cs`
6. Constructor `TrackerToolWindow(object state)` nhận `state` chính là `TrackerViewModel`
7. `TrackerToolWindow` tạo `new TrackerControl(vm)`
8. `TrackerControl` gọi `InitializeComponent()` và gán `DataContext = _vm`

### Điều cần hiểu với người quen C# desktop

- `TrackerControl.xaml` là UI layout.
- `TrackerControl.xaml.cs` là code-behind cho các click event kiểu WPF.
- `TrackerViewModel.cs` mới là nơi chứa logic nghiệp vụ chính.
- Các control có `Command="{Binding ...}"` sẽ gọi `ICommand` trong ViewModel.
- Các control có `Click="..."` sẽ gọi hàm trong code-behind `.xaml.cs`.

## 5.2. Panel Task History

Khi user click menu **Task History...**, luồng là:

1. `src/Commands/ShowHistoryAndSettings.cs`
2. `ShowHistoryWindow.Execute(...)`
3. `_package.ShowHistoryWindowAsync()` trong `src/DevTaskTrackerPackage.cs`
4. `ShowToolWindowAsync(typeof(HistoryToolWindow), ...)`
5. Visual Studio tạo `HistoryToolWindow` trong `src/ToolWindows/DevTaskToolWindows.cs`
6. Constructor nhận `HistoryViewModel`
7. `HistoryToolWindow` tạo `new HistoryControl(vm)`
8. `HistoryControl` gán `DataContext = _vm`
9. Sự kiện `Loaded` của `HistoryControl` gọi `OnLoaded(...)`
10. `OnLoaded(...)` gọi `_vm.RefreshAsync()` để nạp dữ liệu lịch sử lần đầu

Tức là **History panel tự load dữ liệu ngay khi panel được mở**.

---

## 6. Bản đồ chi tiết DevTask Tracker: control nào click thì chạy đâu?

Phần UI nằm ở:

- `src/ToolWindows/TrackerControl.xaml`

Phần event code-behind nằm ở:

- `src/ToolWindows/TrackerControl.xaml.cs`

Phần nghiệp vụ nằm ở:

- `src/ViewModels/TrackerViewModel.cs`
- `src/ViewModels/TodoItemViewModel.cs`

## 6.1. Ô nhập Task URL + nút Fetch

### UI

Trong `TrackerControl.xaml`:

- `TextBox` bind `TaskUrl`
- `Button Fetch` bind `FetchCommand`
- `Button Clear` bind `ClearCommand`

### Khi click Fetch

Luồng chạy:

1. User click `Fetch`
2. `FetchCommand` trong `TrackerViewModel`
3. command gọi `FetchFireAndForget()`
4. `FetchFireAndForget()` gọi `FetchAsync()`
5. `FetchAsync()` dùng `FetchTaskFunc(TaskUrl)`
6. `FetchTaskFunc` do package gán: `url => _taskFactory.FetchAsync(url)`
7. `TaskProviderFactory` chọn provider phù hợp:
   - `GiteaTaskProvider` nếu URL đúng Gitea
   - `ManualTaskProvider` nếu không phải Gitea hoặc nhập tay
8. Nếu fetch thành công:
   - gán `_currentTask`
   - fill `TaskTitle`, `TaskDescription`, `LabelsDisplay`
   - gọi `RegenerateCommit()`
   - publish `TaskFetchedEvent`
   - gọi `DetectRepoRoot()` để dò git repo

### File/hàm chính cần debug nếu Fetch lỗi

- `src/ViewModels/TrackerViewModel.cs` → `FetchAsync()`
- `src/Providers/TaskProviders/GiteaTaskProvider.cs` → `CanHandle(...)`, `FetchAsync(...)`, `ParseIssueUrl(...)`

## 6.2. Nút Clear

### Khi click Clear

Luồng chạy:

1. `ClearCommand`
2. `TrackerViewModel.ClearTask()`
3. reset `_currentTask`, text field, TODO list, timer state
4. gọi `_storage.ClearCurrentTaskAsync()` để xóa file `current-task.json`

Nếu sau này team muốn thêm xác nhận trước khi clear, nên thêm ở UI hoặc command này.

## 6.3. Nhóm Task Timer: Start / Pause / Resume / Stop

### UI

Các nút trong `TrackerControl.xaml` bind tới:

- `StartCommand`
- `PauseCommand`
- `ResumeCommand`
- `StopCommand`

### Khi click Start

1. `StartCommand`
2. `TrackerViewModel.StartTask()`
3. gọi `_timer.Start()` trong `TimeTrackingService`
4. set `TimerState = "Running"`
5. publish `TaskStartedEvent`

### Khi click Pause

1. `PauseCommand`
2. `TrackerViewModel.PauseTask()`
3. gọi `AutoPause()` cho toàn bộ TODO đang chạy
4. gọi `_timer.Pause()`
5. set `TimerState = "Paused"`
6. publish `TaskPausedEvent`
7. gọi auto-save để lưu `current-task.json`

### Khi click Resume

1. `ResumeCommand`
2. `TrackerViewModel.ResumeTask()`
3. gọi `_timer.Resume()`
4. set `TimerState = "Running"`
5. publish `TaskResumedEvent`

### Khi click Stop

1. `StopCommand`
2. `TrackerViewModel.StopTask()`
3. pause tất cả TODO đang chạy
4. gọi `_timer.Stop()`
5. set `TimerState = "Stopped"`

### File/hàm chính của timer

- `src/ViewModels/TrackerViewModel.cs`
  - `StartTask()`
  - `PauseTask()`
  - `ResumeTask()`
  - `StopTask()`
- `src/Core/Services/TimeTrackingService.cs`
  - `Start()`
  - `Pause()`
  - `Resume()`
  - `Stop()`
  - `GetElapsed()`

## 6.4. Work Notes

`TextBox` của Work Notes bind trực tiếp tới property:

- `TrackerViewModel.WorkNotes`

Không có click event riêng. Giá trị này sẽ được lấy khi:

- auto-save current task;
- save/pause task;
- push & complete task.

## 6.5. TODO List

### Khi thêm TODO mới

Trong `TrackerControl.xaml`:

- `TextBox` bind `NewTodoText`
- `Button Add` bind `AddTodoCommand`
- phím Enter cũng bind `AddTodoCommand`

Luồng chạy:

1. `AddTodoCommand`
2. `TrackerViewModel.AddTodo()`
3. tạo `TodoItem`
4. gọi `CreateTodoVm(item)`
5. thêm `TodoItemViewModel` vào `Todos`

### Khi click control trên từng TODO item

Mỗi item trong `ItemsControl` bind trực tiếp đến `TodoItemViewModel`.

Các nút trên từng TODO chạy như sau:

#### Nút ▶ Start

- `TodoItemViewModel.StartCommand`
- gọi `TodoItemViewModel.StartTodo()`
- tạo `TimeSession`
- set `Model.Status = Running`
- start `DispatcherTimer`
- publish `TodoStartedEvent`

#### Nút ⏸ Pause

- `TodoItemViewModel.PauseCommand`
- gọi `PauseTodo()`
- finalize session hiện tại
- set status `Paused`
- stop timer
- publish `TodoPausedEvent`

#### Nút ✓ Complete

- `TodoItemViewModel.CompleteCommand`
- gọi `CompleteTodo()`
- nếu đang chạy thì finalize session
- set `IsDone = true`, status `Done`
- publish `TodoCompletedEvent`

#### Nút 🗑 Delete

- `TodoItemViewModel.DeleteCommand`
- command này chỉ raise event `DeleteRequested`
- `TrackerViewModel.CreateTodoVm(...)` có đăng ký handler cho `DeleteRequested`
- handler đó sẽ remove item khỏi `Todos`

### Điểm quan trọng cần nhớ

- Logic item-level của TODO nằm trong `TodoItemViewModel.cs`
- Logic danh sách TODO nằm trong `TrackerViewModel.cs`
- UI item chỉ bind command, gần như không có code-behind riêng

## 6.6. Business Logic

`TextBox` bind tới:

- `TrackerViewModel.BusinessLogic`

Giá trị này được dùng khi regenerate commit message và khi sinh report.

## 6.7. Commit Message + nút Regenerate

### UI

- `TextBox` bind `CommitMessage`
- nút `↺` bind `RegenerateCommitCommand`

### Khi click Regenerate

1. `RegenerateCommitCommand`
2. `TrackerViewModel.RegenerateCommit()`
3. gọi `CommitMessageGenerator.Generate(_currentTask, BusinessLogic)`
4. cập nhật `CommitMessage`

Nếu sau này muốn đổi format commit message, hãy sửa:

- `src/Core/Services/CommitMessageGenerator.cs`

## 6.8. Nút 🚀 Push & Complete

Đây là luồng nghiệp vụ quan trọng nhất.

### Khi click

1. `PushAndCompleteCommand`
2. `PushAndCompleteFireAndForget()`
3. `CompleteFlowAsync(push: true)`

### Bên trong `CompleteFlowAsync(true)`

Hàm này làm lần lượt:

1. pause tất cả TODO đang chạy;
2. stop timer task chính;
3. lấy cấu hình hiện tại;
4. nếu có git repo thì chạy git commit/push;
5. build `CompletionReport`;
6. archive report JSON;
7. generate report Markdown/JSON;
8. publish `ReportSavedEvent`;
9. publish `TaskCompletedEvent`;
10. clear `current-task.json` nếu push flow;
11. `ClearTask()` để reset UI.

### Git chạy ở đâu?

- `src/Providers/GitProviders/GitService.cs`
- Hàm chính: `PushAndCompleteAsync(...)`

Bên trong git service hiện đang chạy:

- `git config user.name ...` nếu có setting
- `git config user.email ...` nếu có setting
- `git add -A`
- `git commit -m ...`
- `git push` nếu `GitAutoPush = true`
- `git rev-parse HEAD` để lấy commit hash

### Report lưu ở đâu?

- Storage JSON: `src/Providers/StorageProviders/JsonStorageService.cs`
  - `ArchiveReportAsync(...)`
- Generator report:
  - `src/Providers/ReportProviders/JsonReportGenerator.cs`
  - `src/Providers/ReportProviders/MarkdownReportGenerator.cs`
  - `src/Providers/ReportProviders/CompositeReportGenerator.cs`

## 6.9. Nút ⏸ Save & Pause

### Khi click

1. `SaveAndPauseCommand`
2. `SaveAndPauseFireAndForget()`
3. `CompleteFlowAsync(push: false)`

Khác với Push & Complete ở chỗ:

- không commit/push git;
- vẫn build report và lưu dữ liệu;
- không clear hẳn task như luồng complete push;
- dùng như một kiểu “lưu trạng thái công việc”.

## 6.10. Nút 📚 Xem Lịch sử

### UI event

Nút này **không bind command**, mà dùng code-behind:

- `TrackerControl.xaml.cs` → `BtnHistory_Click(...)`

### Luồng chạy

1. `BtnHistory_Click(...)`
2. `_vm.OpenHistoryAction?.Invoke()`
3. action này được package gán thành `() => JoinableTaskFactory.RunAsync(ShowHistoryWindowAsync)`
4. package mở `HistoryToolWindow`

Tức là nút này đang dùng **code-behind → callback → package**.

## 6.11. Nút ⚙ Settings (JSON)

### UI event

- `TrackerControl.xaml.cs` → `BtnSettings_Click(...)`

### Luồng chạy

1. `BtnSettings_Click(...)`
2. `_vm.OpenSettingsAction?.Invoke()`
3. package gọi `OpenSettings()`
4. package tạo `AppSettingsJsonDialog`
5. dialog mở bằng `ShowDialog()`

## 6.12. Nút 📄 Open Log

### UI event

- `TrackerControl.xaml.cs` → `BtnOpenLog_Click(...)`

### Luồng chạy

1. `BtnOpenLog_Click(...)`
2. `_vm.OpenLogFileAction?.Invoke()`
3. package gọi `OpenLogFile()`
4. package tìm file log hôm nay trong `%APPDATA%\DhCodetaskExtension\logs\`
5. dùng `Process.Start(logFile)` để mở bằng editor mặc định của OS

## 6.13. Nút 📋 Open Config

### UI event

- `TrackerControl.xaml.cs` → `BtnOpenConfig_Click(...)`

### Luồng chạy

1. `BtnOpenConfig_Click(...)`
2. `_vm.OpenConfigFileAction?.Invoke()`
3. package gọi `OpenConfigFile()`
4. nếu `settings.json` đã tồn tại thì mở file
5. nếu chưa tồn tại thì save default settings trước rồi mới mở

---

## 7. Bản đồ chi tiết Task History: panel này hoạt động thế nào?

UI:

- `src/ToolWindows/HistoryControl.xaml`
- `src/ToolWindows/HistoryControl.xaml.cs`

ViewModel:

- `src/ViewModels/HistoryViewModel.cs`

## 7.1. Khi panel History vừa mở

- `HistoryControl` gắn event `Loaded += OnLoaded`
- `OnLoaded(...)` gọi `_vm.RefreshAsync()`

Tức là dữ liệu được load ngay khi panel render xong.

## 7.2. Filter Hôm nay / Tuần này / Tháng này / Tùy chỉnh

Các RadioButton trong `HistoryControl.xaml` gọi code-behind:

- `Filter_Today(...)`
- `Filter_Week(...)`
- `Filter_Month(...)`
- `Filter_Custom(...)`

### Luồng thực thi

#### Hôm nay / Tuần này / Tháng này

1. code-behind set `_vm.ViewMode = ...`
2. setter của `ViewMode` trong `HistoryViewModel` tự gọi `RefreshFireAndForget()`
3. `RefreshFireAndForget()` gọi `RefreshAsync()`
4. `RefreshAsync()` gọi repository tương ứng:
   - `GetTodayAsync()`
   - `GetThisWeekAsync()`
   - `GetThisMonthAsync()`

#### Tùy chỉnh

1. `Filter_Custom(...)` chỉ hiện `CustomRangePanel`
2. user chọn `CustomFrom`, `CustomTo`
3. nút `Lọc` bind `CustomFilterCommand`
4. command set `ViewMode = Custom` và gọi refresh
5. `RefreshAsync()` gọi `GetByDateRangeAsync(...)`

## 7.3. Ô Search

`TextBox` bind `SearchKeyword`.

Khi user gõ:

1. setter `SearchKeyword`
2. gọi `ApplyFilter()`
3. filter theo `TaskTitle` hoặc `TaskId`
4. đồng thời áp dụng phân trang

## 7.4. Click vào một item history

Trong item template có:

- `MouseLeftButtonDown="Item_MouseDown"`
- `MouseLeftButtonUp="Item_Click"`

### Single click

1. `Item_MouseDown(...)` lưu vị trí click để tránh hiểu nhầm drag
2. `Item_Click(...)` kiểm tra khoảng lệch chuột
3. nếu là single click: `_vm.OpenDetailAction?.Invoke(s)`
4. action này được package gán thành `OpenReportDetail(...)`
5. package mở `ReportDetailDialog`

### Double click

1. vẫn vào `Item_Click(...)`
2. nếu `e.ClickCount == 2`
3. gọi `_vm.OpenFileAction?.Invoke(s.MarkdownFilePath)`
4. action package gán là `OpenFileInShell(path)`
5. file Markdown report mở bằng editor mặc định

## 7.5. Nút xóa report

- event ở code-behind: `BtnDelete_Click(...)`
- sau xác nhận `MessageBox`, hàm gọi `_vm.DeleteAsync(s)`
- ViewModel gọi repository `_repo.DeleteAsync(s.ReportId)`
- rồi remove item khỏi `_allItems`, refresh lại list

## 7.6. Nút Export CSV

- event code-behind: `BtnExportCsv_Click(...)`
- dùng `SaveFileDialog`
- đọc `_vm.Items`
- build CSV bằng `StringBuilder`
- `File.WriteAllText(...)`

Tức là Export CSV hiện đang đặt ở code-behind chứ chưa đẩy xuống service riêng.

---

## 8. Dialog Settings JSON hoạt động thế nào?

File:

- `src/ToolWindows/AppSettingsJsonDialog.xaml`
- `src/ToolWindows/AppSettingsJsonDialog.xaml.cs`

## 8.1. Khi dialog mở

Trong constructor của dialog, event `Loaded` sẽ:

- serialize `_current` thành JSON;
- đổ vào `TxtJson.Text`;
- show đường dẫn file settings;
- cập nhật counter;
- hiển thị trạng thái loaded.

## 8.2. Nút trong toolbar của dialog

### Format

- `BtnFormat_Click(...)`
- parse JSON bằng `TryParseJson(...)`
- nếu hợp lệ thì format indent lại

### Validate

- `BtnValidate_Click(...)`
- parse JSON
- gọi `ValidateAppSettings(...)`
- kiểm tra tối thiểu:
  - `GiteaBaseUrl` phải bắt đầu bằng `http://` hoặc `https://`
  - nếu `WebhookEnabled = true` thì `WebhookUrl` không được rỗng

### Reset

- `BtnReset_Click(...)`
- thay nội dung bằng `new AppSettings()` serialized

### Reload

- `BtnReload_Click(...)`
- nạp lại từ `_current`

### Open File

- `BtnOpenFile_Click(...)`
- mở file settings thật trên OS nếu đã tồn tại

### Save

- `BtnSave_Click(...)`
- parse JSON
- validate
- convert sang `AppSettings`
- gọi `_saveCallback(settings)`
- callback này do package truyền vào, sẽ update `Settings` và gọi `_storage.SaveSettingsAsync(newSettings)`

---

## 9. Event bus: event nào đang được bắn, ai có thể subscribe?

File event contract:

- `src/Core/Events/AppEvents.cs`

Triển khai pub/sub:

- `src/Core/Services/EventBus.cs`

## 9.1. Event hiện có

Các event chính:

- `TaskFetchedEvent`
- `TaskStartedEvent`
- `TaskPausedEvent`
- `TaskResumedEvent`
- `TaskCompletedEvent`
- `CommitPushedEvent`
- `ReportSavedEvent`
- `TodoStartedEvent`
- `TodoPausedEvent`
- `TodoCompletedEvent`

## 9.2. Ai đang subscribe?

Hiện trong package có subscribe rõ ràng:

- `_eventBus.Subscribe<TaskCompletedEvent>(e => _webhook.OnEvent(e));`

Tức là khi task hoàn tất, webhook provider sẽ tự POST event ra ngoài.

## 9.3. Khi nào nên dùng event bus?

Nếu sau này team thêm:

- telemetry;
- audit log;
- sync ra hệ thống khác;
- popup thông báo;
- refresh panel phụ;

thì nên subscribe vào event bus thay vì gọi cứng trực tiếp từ `TrackerViewModel`, để giữ code ít coupling hơn.

---

## 10. Dữ liệu được lưu ở đâu?

Lớp chịu trách nhiệm chính:

- `src/Providers/StorageProviders/JsonStorageService.cs`

## 10.1. Root storage

Package lấy root bằng `GetStorageRoot()` trong `src/DevTaskTrackerPackage.cs`:

- nếu `Settings.StoragePath` có giá trị và folder tồn tại → dùng folder đó;
- nếu không → dùng `%APPDATA%\DhCodetaskExtension`

## 10.2. Các file quan trọng

### settings.json

- đường dẫn: `<root>\settings.json`
- đọc/ghi bởi:
  - `LoadSettingsAsync()`
  - `SaveSettingsAsync()`

### current-task.json

- đường dẫn: `<root>\current-task.json`
- dùng để restore task đang làm dở
- đọc/ghi bởi:
  - `LoadCurrentTaskAsync()`
  - `SaveCurrentTaskAsync()`
  - `ClearCurrentTaskAsync()`

### history report

- thư mục: `<root>\history\YYYY\MM\`
- JSON archive được ghi bởi `ArchiveReportAsync(...)`
- file Markdown được sinh thêm bởi report generator

---

## 11. Khi fetch task Gitea, code chạy như thế nào?

File chính:

- `src/Providers/TaskProviders/GiteaTaskProvider.cs`

## 11.1. Điều kiện để provider này xử lý URL

`CanHandle(string url)` sẽ check:

1. URL không rỗng;
2. `settings.GiteaBaseUrl` không rỗng;
3. URL bắt đầu bằng base URL;
4. `ParseIssueUrl(...)` parse được theo pattern:
   - `{base}/{owner}/{repo}/issues/{number}`

## 11.2. Khi fetch

`FetchAsync(...)` sẽ:

1. parse owner/repo/number từ URL;
2. build API URL:
   - `/api/v1/repos/{owner}/{repo}/issues/{number}`
3. nếu có token thì set header Authorization;
4. gọi HTTP GET;
5. parse JSON response;
6. lấy `labels`, `body`, `title`, `html_url`;
7. map sang `TaskItem`

## 11.3. Các lỗi thường gặp

- 401 Unauthorized → token sai
- 404 Not Found → URL issue sai hoặc issue không tồn tại
- timeout → request quá lâu
- parse URL fail → base URL hoặc format URL không đúng

Nếu team muốn hỗ trợ Jira/GitLab/Azure DevOps, hãy tạo provider mới theo cùng pattern `ITaskProvider`.

---

## 12. Khi chạy Git, code thực thi ở đâu?

File:

- `src/Providers/GitProviders/GitService.cs`

## 12.1. Cách service này hoạt động

`GitService` không dùng thư viện Git .NET; nó gọi trực tiếp executable `git` qua `ProcessStartInfo`.

Điều đó có nghĩa là máy chạy extension cần có `git` trong PATH.

## 12.2. Các hàm chính

### `IsAvailable()`

- chạy `git --version`
- dùng để biết máy có git hay không

### `FindRepoRoot(startPath)`

- duyệt ngược thư mục cha cho đến khi thấy folder `.git`
- dùng để xác định repo hiện tại

### `GetCurrentBranchAsync(repoRoot)`

- chạy `git rev-parse --abbrev-ref HEAD`

### `PushAndCompleteAsync(repoRoot, commitMessage, autoPush)`

- config user nếu có setting
- `git add -A`
- `git commit -m ...`
- `git push` nếu `autoPush = true`
- lấy hash bằng `git rev-parse HEAD`

Nếu cần đổi strategy commit/push, sửa tại đây, không nên sửa rải rác ở ViewModel.

---

## 13. Nếu muốn thêm tính năng mới thì nên sửa ở đâu?

## 13.1. Thêm button mới trong Tracker

Ví dụ thêm nút “Copy report path”:

1. thêm Button vào `src/ToolWindows/TrackerControl.xaml`
2. quyết định dùng:
   - `Command` nếu logic thuộc ViewModel;
   - `Click` nếu chỉ là UI shell action đơn giản
3. nếu logic nghiệp vụ → thêm `ICommand` vào `TrackerViewModel`
4. nếu cần mở dialog/file/VS window → package nên truyền `Action` xuống ViewModel giống pattern hiện tại

## 13.2. Thêm panel mới

1. tạo `ToolWindowPane` mới trong `src/ToolWindows/`
2. tạo XAML control tương ứng
3. tạo ViewModel tương ứng
4. thêm `[ProvideToolWindow(typeof(...))]` ở package
5. trả state trong `InitializeToolWindowAsync(...)`
6. tạo command trong `src/Commands/`
7. thêm button/menu trong `src/CommandTable.vsct`

## 13.3. Thêm nguồn task mới

1. tạo file mới trong `src/Providers/TaskProviders/`
2. implement `ITaskProvider`
3. đăng ký provider tại `DevTaskTrackerPackage.InitializeAsync(...)`

## 13.4. Thêm loại report mới

1. tạo generator mới trong `src/Providers/ReportProviders/`
2. implement interface report generator đang dùng
3. đăng ký `.Register(...)` vào `_reportGen` trong package

## 13.5. Thêm phản ứng khi task complete

Không nên gọi cứng trong `TrackerViewModel`.

Nên:

1. tạo service/provider mới
2. subscribe `TaskCompletedEvent` trong package
3. xử lý ở subscriber

---

## 14. Những chỗ dễ gây nhầm cho người mới

## 14.1. Vì sao có cả command binding lẫn click event?

Vì code hiện tại dùng **2 phong cách song song**:

- `Command` cho nghiệp vụ ViewModel-centric;
- `Click` code-behind cho thao tác UI/shell-centric như mở History, Settings, file log/config.

Không phải bug. Đây là lựa chọn kiến trúc hiện tại.

## 14.2. Vì sao có cả `DevTaskTrackerPackage.cs` và `DhCodetaskPackage.cs`?

- `DevTaskTrackerPackage.cs` = package chính của tracker hiện tại
- `DhCodetaskPackage.cs` = package cũ, giữ tương thích và window legacy

Khi thêm tính năng mới cho tracker chính, gần như luôn nên bắt đầu từ `DevTaskTrackerPackage.cs`.

## 14.3. Vì sao `TrackerViewModel` biết rất nhiều thứ?

Hiện tại `TrackerViewModel` đang đóng vai trò orchestration lớn. Đây là trung tâm nghiệp vụ của app.

Nếu dự án tiếp tục mở rộng, có thể tách dần thành:

- task workflow service
- report application service
- todo coordinator
- git completion service

Nhưng ở trạng thái hiện tại, việc debug từ `TrackerViewModel` là nhanh nhất.

---

## 15. Checklist debug nhanh theo tình huống

## 15.1. Mở panel không lên

Kiểm tra theo thứ tự:

1. `src/CommandTable.vsct` có khai báo button/menu chưa?
2. `src/Commands/*.cs` có đăng ký command trong package chưa?
3. package có gọi `InitializeAsync(...)` của command đó chưa?
4. package có `[ProvideToolWindow(typeof(...))]` chưa?
5. `InitializeToolWindowAsync(...)` có trả đúng state không?
6. constructor `ToolWindowPane` có set `Content = new ...Control(...)` không?

## 15.2. Click button mà không có tác dụng

Kiểm tra:

1. button dùng `Command` hay `Click`?
2. nếu `Command` → xem ViewModel đã tạo command chưa, `CanExecute` có đang false không?
3. nếu `Click` → xem đúng hàm trong `.xaml.cs` chưa?
4. hàm đó có gọi callback/action xuống package không?
5. package có gán callback/action chưa?

## 15.3. Fetch Gitea lỗi

Kiểm tra:

1. `settings.json` có đúng `GiteaBaseUrl` và `GiteaToken` không?
2. URL issue có đúng format `/issues/{number}` không?
3. vào `GiteaTaskProvider.FetchAsync(...)`
4. xem HTTP status code

## 15.4. Push & Complete lỗi

Kiểm tra:

1. `GitAvailable` có true không?
2. `_repoRoot` có detect được không?
3. vào `GitService.PushAndCompleteAsync(...)`
4. lệnh nào fail: `git add`, `git commit`, hay `git push`
5. commit message có ký tự gây lỗi shell không

## 15.5. History không hiển thị dữ liệu

Kiểm tra:

1. `JsonStorageService.ArchiveReportAsync(...)` có ghi JSON chưa?
2. report generator có chạy xong không?
3. thư mục `history/YYYY/MM` có file không?
4. `HistoryControl.OnLoaded(...)` có gọi `_vm.RefreshAsync()` không?
5. `HistoryViewModel.RefreshAsync()` có vào đúng branch filter không?

---

## 16. Đề xuất quy ước làm việc cho team về sau

Để code dễ bảo trì hơn, team nên giữ các quy ước sau:

1. **Mọi logic nghiệp vụ mới ưu tiên đặt trong ViewModel/service**, không nhồi thêm vào code-behind nếu không cần thiết.
2. **Mọi thao tác mở dialog/file/tool window nên đi qua package** bằng `Action` callback, giữ ViewModel không phụ thuộc Visual Studio shell quá trực tiếp.
3. **Tích hợp hệ thống ngoài nên đi qua provider** (`TaskProviders`, `NotificationProviders`, `ReportProviders`, `GitProviders`).
4. **Phản ứng chéo giữa các module nên dùng EventBus**, tránh gọi chéo trực tiếp.
5. **Khi debug flow UI, luôn đi theo thứ tự**: XAML → code-behind hoặc Command → ViewModel → Provider/Service.

---

## 17. Tóm tắt cực ngắn cho người mới vào dự án

Nếu chỉ cần nhớ 10 dòng:

1. Extension khởi động từ `src/DevTaskTrackerPackage.cs`.
2. Menu command được khai báo ở `src/CommandTable.vsct` và đăng ký trong `src/Commands/`.
3. Panel DevTask Tracker được dựng bởi `src/ToolWindows/DevTaskToolWindows.cs`.
4. UI tracker nằm ở `src/ToolWindows/TrackerControl.xaml`.
5. Click/command của tracker chủ yếu chạy vào `src/ViewModels/TrackerViewModel.cs`.
6. TODO item chạy ở `src/ViewModels/TodoItemViewModel.cs`.
7. Panel History nằm ở `src/ToolWindows/HistoryControl.xaml` và logic ở `src/ViewModels/HistoryViewModel.cs`.
8. Storage JSON nằm ở `src/Providers/StorageProviders/JsonStorageService.cs`.
9. Fetch Gitea nằm ở `src/Providers/TaskProviders/GiteaTaskProvider.cs`.
10. Git commit/push nằm ở `src/Providers/GitProviders/GitService.cs`.

---

## 18. Tài liệu nên đọc kèm

Ngoài file này, nên đọc thêm:

- `README.md`
- `docs/ProjectStructure.md`
- `src/DevTaskTrackerPackage.cs`
- `src/ViewModels/TrackerViewModel.cs`
- `src/ViewModels/HistoryViewModel.cs`

Nếu cần, bước tiếp theo tôi có thể viết thêm **một tài liệu dạng sơ đồ sequence chi tiết** cho từng flow riêng:

1. Fetch task
2. Start/Pause/Resume timer
3. Push & Complete
4. History load / filter / open detail
5. Settings JSON save flow
