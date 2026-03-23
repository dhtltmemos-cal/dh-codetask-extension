## 2026-03-23 14:00 — v3.10: Hiển thị thông tin creator và trích xuất kết nối từ body issue

**Yêu cầu:** Sau khi Fetch issue từ Gitea, hiển thị thêm `username create`, `date create`; trích xuất IP server, tên database, port từ body issue bằng regex/string matching; thêm control hiển thị và nút copy.

**Nguyên nhân / Mục đích:** Các task của nhóm thường chứa thông tin kết nối CSDL ngay trong body issue (được nhập tay theo nhiều format khác nhau). Cần parse và hiển thị nhanh để dev không cần mở trình duyệt tìm lại.

**Thay đổi:**

- `src/Core/Models/IssueConnectionInfo.cs` — Model mới lưu IP/Database/Port; có `HasAny`, `Summary`
- `src/Core/Services/IssueBodyParser.cs` — Parse body bằng nhiều regex pattern, hỗ trợ keyword: ip server, csdl, ten_csdl, db, database, port, port_csdl, máy chủ, cổng...; fallback DefaultServerIp
- `src/Core/Models/TaskItem.cs` — Thêm `CreatedByUser`, `CreatedAt`, `ConnectionInfo`
- `src/Core/Models/AppSettings.cs` — Thêm `DefaultServerIp` (IP mặc định của nhóm)
- `src/Providers/TaskProviders/GiteaTaskProvider.cs` — Lấy `user.login`, `created_at` từ Gitea API; gọi `IssueBodyParser.Parse(rawBody, defaultServerIp)`
- `src/ViewModels/TrackerViewModel.cs` — Thêm properties `CreatedByUser`, `CreatedAt`, `HasCreatorInfo`, `ConnIpServer`, `ConnDatabase`, `ConnPort`, `HasConnInfo`; thêm `CopyIpCommand`, `CopyDbCommand`, `CopyPortCommand`; refactor `ApplyTaskToUI()`
- `src/ToolWindows/TrackerControl.xaml` — Section creator info (👤 tạo bởi + 📅 ngày tạo) và section 🔌 Thông tin kết nối (IP/DB/Port) với nút 📋 copy, ẩn khi không có dữ liệu
- `src/ToolWindows/TrackerControl.xaml.cs` — Wire `CopyToClipboardAction` → `Clipboard.SetText()`
- `src/DhCodetaskExtension.csproj` — Thêm `IssueConnectionInfo.cs` và `IssueBodyParser.cs`

---

## 2026-03-23 10:30 — v3.9: Cập nhật tự động qua VSIX Gallery của Visual Studio

**Yêu cầu:** Dùng cơ chế cập nhật tích hợp sẵn của Visual Studio — không tự viết HTTP client.

**Cách hoạt động:**
- Extension khai báo `<GalleryUrl>` trong `source.extension.vsixmanifest`
- VS tự check feed qua **Tools > Extensions and Updates > Updates**
- VS tự tải và cài — không cần code gì thêm trong extension

**Triển khai:**

`src/source.extension.vsixmanifest` — thêm 1 dòng `<GalleryUrl>` (thay `YOUR_GITHUB_USERNAME`)

`docs/vsixfeed.template.xml` — template Atom feed với `{{PLACEHOLDER}}`, chỉ sửa khi cần đổi cấu trúc

`docs/vsixfeed.xml` — generated, do CI tự cập nhật sau mỗi release

`.github/workflows/release.yml` — YAML gọn, chỉ gọi `node release.js <command>`

`.github/workflows/release.js` — toàn bộ logic: patch-version, prepare-assets, update-feed

**Version bump:** 3.8 → 3.9

## 2026-03-22 18:30 — v3.8: Fix TodoTemplates hiển thị + TODO chỉ chạy khi task cha Running

**Yêu cầu:**

1. TodoTemplates chưa hiển thị — cần triển khai chọn từ danh sách, button Add enabled
2. TODO con phải chỉ start được khi task cha đang Running

**1. Fix TodoTemplates Expander không hiển thị**

Nguyên nhân: `BooleanToVisibilityConverter` chỉ nhận `bool`, nhưng XAML bind Visibility vào `TodoTemplates.Count` (kiểu `int`) → WPF silent-fail → Expander luôn ẩn.

Fix: Thêm `HasTodoTemplates { get => TodoTemplates.Count > 0; }` vào `TrackerViewModel`. XAML bind `Visibility="{Binding HasTodoTemplates, Converter={StaticResource BoolVis}}"`. Expander có `IsExpanded="True"` để tự mở, thêm hint text hướng dẫn user.

Files: `src/ViewModels/TrackerViewModel.cs`, `src/ToolWindows/TrackerControl.xaml`

**2. TODO ▶ chỉ enable khi task cha đang Running**

Nguyên nhân: `TodoItemViewModel.CanStart` chỉ kiểm tra state của chính item, không kiểm tra task cha.

Fix:

- `TodoItemViewModel`: thêm `Func<bool> isParentRunning` param. `CanStart` kiểm tra cả hai điều kiện. Thêm `RefreshParentStateCommands()` để re-evaluate khi task cha đổi state.
- `TrackerViewModel.CreateTodoVm()`: truyền `() => TimerState == "Running"`.
- `TrackerViewModel.TimerState` setter: gọi `RefreshTodoParentStateCommands()` → mỗi todo vm gọi `RefreshParentStateCommands()`.

Kết quả: Task Running → ▶ enable; Task Paused/Stopped/Idle → ▶ disable tức thì.

Files: `src/ViewModels/TodoItemViewModel.cs`, `src/ViewModels/TrackerViewModel.cs`

**Version bump:** 3.7 → 3.8
