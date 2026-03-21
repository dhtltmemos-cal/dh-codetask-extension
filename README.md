# DH Codetask Extension — DevTask Tracker v3.1

Extension theo dõi task, time tracking và TODO trực tiếp trong Visual Studio 2017, tích hợp Gitea.

## Tính năng v3.1 (mới)

| Tính năng | Mô tả |
|-----------|-------|
| **Settings JSON only** | Chỉ dùng JSON editor cho cấu hình — không còn form dialog |
| **Action Logging** | Mọi thao tác (Start, Pause, Fetch, Push, TODO...) được ghi ra Output Window |
| **Open Log Button** | Nút "📄 Open Log" trong panel + menu mở file log hôm nay |
| **Open Config Button** | Nút "📋 Open Config" trong panel + menu mở settings.json trong editor ngoài |

## Tính năng v3.0

| Tính năng | Mô tả |
|-----------|-------|
| **Gitea Integration** | Fetch task từ URL issue Gitea, auto-populate title/labels/description |
| **Task Timer** | Start / Pause / Resume / Stop với đồng hồ real-time |
| **TODO Time Tracking** | Mỗi TODO item có timer độc lập Start/Pause/Complete |
| **Completion Report** | Sinh JSON + Markdown tự động, lưu `history\YYYY\MM\` |
| **History Browser** | Hôm nay / Tuần / Tháng / Tùy chỉnh, phân trang, export CSV |
| **Git Integration** | Auto commit message generation, push & complete một thao tác |
| **Webhook** | POST event ra ngoài (Slack, CI/CD…), retry 3 lần |
| **Restore** | Khôi phục task dở khi VS khởi động lại |
| **Provider Pattern** | Thêm provider mới không cần sửa code hiện có |

## Cấu trúc thư mục

```
dh-codetask-extension/
├── DhCodetaskExtension.sln
├── LICENSE / README.md / CHANGELOG.md / user_changelog.md
├── .opushforce.message / .gitignore
├── docs/
│   ├── Instructions.md
│   ├── error-skill-devtasktracker.md
│   ├── rule.md
│   └── tasks/
│       └── task-2026-03-21-00.md
└── src/
    ├── DhCodetaskExtension.csproj
    ├── DevTaskTrackerPackage.cs        ← v3.1: OpenSettings→JSON, OpenLogFile, OpenConfigFile
    ├── PackageGuids.cs                 ← v3.1: CmdIdOpenLogFile 0x0700, CmdIdOpenConfigFile 0x0800
    ├── CommandTable.vsct               ← v3.1: Remove CmdIdSettings form, add UtilityMenuGroup
    ├── Core/
    │   ├── Interfaces/
    │   ├── Models/
    │   ├── Events/
    │   └── Services/                   ← AppLogger (log to file + Output Window)
    ├── Providers/
    │   ├── TaskProviders/
    │   ├── StorageProviders/           ← v3.1: GetSettingsFilePath()
    │   ├── GitProviders/
    │   ├── ReportProviders/
    │   └── NotificationProviders/
    ├── ViewModels/
    │   ├── TrackerViewModel.cs         ← v3.1: comprehensive action logging + OpenLogFileAction/OpenConfigFileAction
    │   ├── TodoItemViewModel.cs
    │   └── HistoryViewModel.cs
    ├── Commands/
    │   ├── ShowTrackerWindow.cs
    │   ├── ShowHistoryAndSettings.cs   ← v3.1: ShowTaskSettings → AppSettingsJsonDialog
    │   ├── ShowJsonSettings.cs         ← v3.1: → AppSettingsJsonDialog
    │   ├── ShowSettings.cs             ← v3.1: stub (0x0400 removed from menu)
    │   ├── ShowMainWindow.cs
    │   └── OpenLogAndConfig.cs         ← v3.1: NEW — OpenLogFileCommand, OpenConfigFileCommand
    ├── Services/
    └── ToolWindows/
        ├── AppSettingsJsonDialog.xaml/.cs  ← v3.1: NEW — JSON editor for AppSettings
        ├── TrackerControl.xaml/.cs         ← v3.1: utility row + Open Log/Config buttons
        ├── HistoryControl.xaml/.cs
        ├── ReportDetailDialog.xaml/.cs
        └── DevTaskToolWindows.cs
```

## Cài đặt & Build

```bash
# 1. Mở VS2017, open DhCodetaskExtension.sln
# 2. Restore NuGet packages
# 3. F5 để debug trong Experimental Instance
# 4. View > DevTask Tracker để mở panel
```

## Cấu hình (v3.1 — JSON only)

Mọi cấu hình đều đọc/ghi từ **một nguồn duy nhất**: `%APPDATA%\DhCodetaskExtension\settings.json`

**Cách mở:**
- Nút **⚙ Settings (JSON)** trong TrackerControl
- Menu **DH Codetask Extension > ⚙ Settings (JSON)...**
- Nút **📋 Open Config** → mở file trong OS editor

**Ví dụ settings.json:**
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
  "Extensions": {}
}
```

## Log Files

Log được ghi vào: `%APPDATA%\DhCodetaskExtension\logs\devtask_YYYYMMDD.log`

**Cách mở:**
- Nút **📄 Open Log** trong TrackerControl
- Menu **DH Codetask Extension > 📄 Open Log File**

**Những gì được log:**
- Mọi thao tác user: Start, Pause, Resume, Stop, Fetch, Add/Delete TODO, Push & Complete, Save & Pause
- Git operations: commit hash, branch, error
- Report saved: elapsed time, todo count
- Auto-save events
- Settings open/save
- Extension init / restore

## Luồng cơ bản

```
URL issue → [Fetch] → [▶ Start] → work... → [⏸ Pause]... → [▶ Resume]
→ Thêm TODO items, start/pause từng item
→ Ghi Work Notes & Business Logic
→ [🚀 Push & Complete] → git commit+push → report JSON+MD → clear
```

## Mở rộng

```csharp
// Thêm provider task mới
public class JiraTaskProvider : ITaskProvider {
    public bool CanHandle(string url) => url.Contains("atlassian.net");
    ...
}
_taskFactory.Register(new JiraTaskProvider(() => Settings));
```

## Versioning

Format: `dh-codetask-extension.<version>.<nội-dung-thay-đổi>.zip`

---

_DhCodetaskExtension v3.1 — JSON-only Settings · Action Logging · Open Log/Config · Gitea-first · Provider Pattern · TODO Time Tracking_
