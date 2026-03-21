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
2. **Log mọi thao tác user ra Output Window**
3. **Bổ sung nút và menu mở file log / file cấu hình**

## 2026-03-21 20:00 — Task task-2026-03-21-01.md

**Yêu cầu:** Triển khai 4 yêu cầu từ `docs/tasks/task-2026-03-21-01.md`:

1. **Chỉnh lỗi Filter_Week NullReferenceException**
   - Lỗi: `Object reference not set to an instance of an object` tại `HistoryControl.xaml.cs` Filter_Week
   - Nguyên nhân: RadioButton `IsChecked="True"` bắn event `Checked` trong `InitializeComponent()` trước khi `CustomRangePanel` được gán
   - Fix: thêm flag `_isInitialized`, tất cả Filter_* handlers kiểm tra flag trước khi xử lý

2. **Resume task từ lịch sử**
   - Nút "▶ Resume" trong mỗi row của History panel
   - Click → load task vào Tracker panel, timer ở trạng thái Paused sẵn sàng Resume
   - Package wire `ResumeFromHistoryAction` → `BuildWorkLogFromReport()` → `RestoreFromLogAsync()`

3. **Open URL trong cả 2 panel**
   - Tracker: nút "🔗" trong URL bar → `Process.Start(url)`
   - History: nút "🔗 URL" mỗi row → `Process.Start(url)`
   - ReportDetailDialog: nút "🔗 URL" trong action row

4. **Dialogs on top**
   - `AppSettingsJsonDialog.xaml`: `Topmost="True"`
   - `ReportDetailDialog.xaml`: `Topmost="True"`
