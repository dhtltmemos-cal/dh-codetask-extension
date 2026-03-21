# User Changelog

## 2026-03-21 10:00 — Đổi tên project

**Yêu cầu:** Đổi tên toàn bộ project từ `VS2017ExtensionTemplate` sang `dh-codetask-extension`.
- Đổi tên namespace, assembly, class, file, GUID string display
- Cập nhật tất cả references bên trong code
- Tạo zip theo quy tắc versioning

## 2026-03-21 11:00 — Thêm top-level menu "DH Codetask Extension"

**Yêu cầu:** Bổ sung menu cha là "DH Codetask Extension" trên menu bar Visual Studio, với menu con "Settings" và "Settings (JSON)".

## 2026-03-21 14:00 — Triển khai DevTaskTracker v3.0

**Yêu cầu:** Đọc docs/Instructions.md, docs/error-skill-devtasktracker.md, docs/rule.md và triển khai toàn bộ theo Instructions.md — DevTaskTracker v3.0 với:
- Fetch task từ Gitea (Provider Pattern)
- Time tracking cấp task và TODO item độc lập
- Completion Report JSON + Markdown (immutable, atomic write)
- History Browser (ngày/tuần/tháng, phân trang, export CSV)
- EventBus non-blocking, WebhookNotificationProvider
- Settings dialog 12 trường
- Restore task dở khi khởi động lại VS

## 2026-03-21 17:00 — Task task-2026-03-21-00.md

**Yêu cầu:** Triển khai 3 yêu cầu từ `docs/tasks/task-2026-03-21-00.md`:

1. **Loại bỏ menu Setting (form), chỉ giữ lại Settings (JSON)**
   - Xóa `CmdIdSettings` (0x0400) khỏi menu
   - Tất cả settings đọc/ghi từ `settings.json` (AppSettings) duy nhất
   - Tạo `AppSettingsJsonDialog` thay thế hoàn toàn `TaskSettingsDialog`
   - Nút "⚙ Settings" trong TrackerControl → mở JSON editor

2. **Log mọi thao tác user ra Output Window**
   - `TrackerViewModel`: log Start, Pause, Resume, Stop, Fetch OK/Fail, Add TODO, Delete TODO, Clear, Complete Flow, Git push, Report saved, Auto-save, Restore
   - `TrackerControl`: log mỗi button click qua AppLogger
   - `DevTaskTrackerPackage`: log OpenSettings, OpenLogFile, OpenConfigFile
   - `AppLogger.Instance.Init(OutputWindow)` được gọi ngay sau khi OutputWindow sẵn sàng

3. **Bổ sung nút và menu mở file log / file cấu hình**
   - Menu "DH Codetask Extension > 📄 Open Log File" (CmdIdOpenLogFile 0x0700)
   - Menu "DH Codetask Extension > 📋 Open Config File" (CmdIdOpenConfigFile 0x0800)
   - Nút "📄 Open Log" trong TrackerControl (utility row cuối panel)
   - Nút "📋 Open Config" trong TrackerControl (utility row cuối panel)
   - Mở bằng `Process.Start(path)` — OS mặc định editor
