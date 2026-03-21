# DH Codetask Extension — DevTask Tracker v3.2

Extension theo dõi task, time tracking và TODO trực tiếp trong Visual Studio 2017, tích hợp Gitea.

## Tính năng v3.2 (mới)

| Tính năng | Mô tả |
|-----------|-------|
| **Fix: History filter null ref** | Sửa lỗi `NullReferenceException` trong Filter_Week/Today/Month khi khởi động HistoryControl |
| **Resume từ lịch sử** | Nút "▶ Resume" trong History panel — load task cũ vào Tracker, timer ở trạng thái Paused |
| **Open URL (Tracker)** | Nút "🔗" trong URL bar — mở URL task trong browser mặc định |
| **Open URL (History)** | Nút "🔗 URL" mỗi row lịch sử — mở URL task trong browser |
| **Open URL (Report Detail)** | Nút "🔗 URL" trong dialog chi tiết report |
| **Dialogs Topmost** | AppSettingsJsonDialog và ReportDetailDialog luôn hiển thị trên cùng (`Topmost=True`) |

## Tính năng v3.1

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
│       ├── task-2026-03-21-00.md  ✅ done
│       └── task-2026-03-21-01.md  ✅ done
└── src/
    ├── DhCodetaskExtension.csproj
    ├── DevTaskTrackerPackage.cs        ← v3.2: BuildWorkLogFromReport, ResumeFromHistory/OpenUrl wiring
    ├── PackageGuids.cs
    ├── CommandTable.vsct
    ├── Core/
    │   ├── Interfaces/
    │   ├── Models/
    │   ├── Events/
    │   └── Services/                   ← AppLogger (log to file + Output Window)
    ├── Providers/
    │   ├── TaskProviders/
    │   ├── StorageProviders/           ← GetSettingsFilePath()
    │   ├── GitProviders/
    │   ├── ReportProviders/
    │   └── NotificationProviders/
    ├── ViewModels/
    │   ├── TrackerViewModel.cs
    │   ├── TodoItemViewModel.cs
    │   └── HistoryViewModel.cs         ← v3.2: ResumeFromHistoryAction, OpenUrlAction, ResumeCommand, OpenUrlCommand
    ├── Commands/
    └── ToolWindows/
        ├── AppSettingsJsonDialog.xaml/.cs   ← v3.2: Topmost=True
        ├── TrackerControl.xaml/.cs          ← v3.2: 🔗 Open URL button
        ├── HistoryControl.xaml/.cs          ← v3.2: fix null ref, ▶ Resume + 🔗 URL buttons
        ├── ReportDetailDialog.xaml/.cs      ← v3.2: Topmost=True, 🔗 URL button
        └── DevTaskToolWindows.cs
```

## Cài đặt & Build

```bash
# 1. Mở VS2017, open DhCodetaskExtension.sln
# 2. Restore NuGet packages
# 3. F5 để debug trong Experimental Instance
# 4. View > DevTask Tracker để mở panel
```

## Cấu hình (JSON only)

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

## Hướng dẫn sử dụng v3.2

### Luồng cơ bản

```
URL issue → [Fetch] → [🔗] mở URL trong browser
→ [▶ Start] → work... → [⏸ Pause]
→ Thêm TODO, ghi Notes
→ [🚀 Push & Complete] → report sinh tự động
```

### Resume task từ lịch sử

1. Mở **Task History** panel (`View > Task History` hoặc nút `📚 Xem Lịch sử`)
2. Tìm task cần tiếp tục
3. Click nút **▶ Resume** trên row đó
4. Extension tự động load task vào Tracker panel, timer ở trạng thái **Paused**
5. Click **▶ Resume** trong Tracker để tiếp tục tính giờ

### Mở URL task

- **Tracker**: nhấn nút **🔗** bên cạnh URL bar
- **History**: nhấn nút **🔗 URL** trên mỗi row
- **Report Detail**: nhấn nút **🔗 URL** trong dialog

## Log Files

Log: `%APPDATA%\DhCodetaskExtension\logs\devtask_YYYYMMDD.log`

## Versioning

Format: `dh-codetask-extension.<version>.<nội-dung-thay-đổi>.zip`

---

_DhCodetaskExtension v3.2 — Fix History Filter · Resume from History · Open URL · Topmost Dialogs · JSON-only Settings · Action Logging_
