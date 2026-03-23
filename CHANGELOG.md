## 2026-03-23 - v3.10: Issue Creator Info + Connection Info Parser

**Nguyên nhân:** Yêu cầu từ `docs/tasks/task-2026-03-23-02.md` — hiển thị thông tin người tạo issue và trích xuất thông tin kết nối CSDL (IP, database, port) từ body issue.

### Thay đổi

**`src/Core/Models/IssueConnectionInfo.cs`** — Model mới:
- Properties: `IpServer`, `Database`, `Port`
- Computed: `HasAny` (bool), `Summary` (string dạng `host:port/db`)

**`src/Core/Services/IssueBodyParser.cs`** — Class parse body issue:
- `Parse(body, defaultServerIp)` dùng nhiều Regex pattern ưu tiên theo thứ tự
- IP: match keyword `ip server`, `host`, `hostname`, `máy chủ` → sau đó fallback tìm IP bất kỳ
- Database: match keyword `ten_csdl`, `database`, `csdl`, `db`, `tên_csdl` → tên không dấu, không ký tự đặc biệt
- Port: match keyword `port`, `port_csdl`, `port_db`, `cổng` → chỉ nhận số 1–65535
- Fallback `DefaultServerIp` từ settings khi body không ghi rõ IP

**`src/Core/Models/TaskItem.cs`** — Thêm fields:
- `CreatedByUser` (string) — login của người tạo issue
- `CreatedAt` (string) — ngày tạo định dạng `dd/MM/yyyy HH:mm`
- `ConnectionInfo` (IssueConnectionInfo)

**`src/Core/Models/AppSettings.cs`** — Thêm `DefaultServerIp`:
- Dùng làm fallback IP khi body không ghi rõ
- Ví dụ cấu hình: `"DefaultServerIp": "192.168.50.79"`

**`src/Providers/TaskProviders/GiteaTaskProvider.cs`** — Bổ sung:
- Lấy `obj["user"]["login"]` → `CreatedByUser`
- Parse `obj["created_at"]` → `CreatedAt` (local time)
- Gọi `IssueBodyParser.Parse(rawBody, settings.DefaultServerIp)` → `ConnectionInfo`

**`src/ViewModels/TrackerViewModel.cs`** — Bổ sung:
- Properties: `CreatedByUser`, `CreatedAt`, `HasCreatorInfo`, `ConnIpServer`, `ConnDatabase`, `ConnPort`, `HasConnInfo`
- Commands: `CopyIpCommand`, `CopyDbCommand`, `CopyPortCommand` (delegate sang `CopyToClipboardAction`)
- `CopyToClipboardAction` (Action&lt;string&gt;) — set bởi code-behind
- Helper `ApplyTaskToUI(TaskItem)` — áp dụng toàn bộ task data lên UI properties

**`src/ToolWindows/TrackerControl.xaml`** — Thêm 2 section:
- Creator info (ẩn khi không có): `👤 Tạo bởi: [user]  📅 [ngày]`
- Connection info (ẩn khi không parse được): `🖥 IP`, `🗄 DB`, `🔌 Port` — mỗi dòng có nút 📋 copy

**`src/ToolWindows/TrackerControl.xaml.cs`** — Wire `CopyToClipboardAction` → `Clipboard.SetText()`

**`src/DhCodetaskExtension.csproj`** — Thêm 2 Compile entry mới

**Version bump:** 3.9 → 3.10

---

## 2026-03-23 - v3.9: Cập nhật tự động qua VSIX Gallery + GitHub Actions

**Nguyên nhân:** Yêu cầu từ `docs/tasks/task-2026-03-23-01.md` — dùng cơ chế `GalleryUrl` tích hợp sẵn của VS2017 (đúng chuẩn VSIX), không tự viết HTTP client.

### Thay đổi

**`src/source.extension.vsixmanifest`:**
- Thêm `<GalleryUrl>` trỏ về GitHub Pages feed
- VS đọc feed qua `Tools > Extensions and Updates > Updates`

**`docs/vsixfeed.xml`** — generated file, do CI cập nhật tự động

**`docs/vsixfeed.template.xml`** — template gốc với `{{PLACEHOLDER}}`, chỉ sửa file này khi cần thay cấu trúc feed

**`.github/workflows/release.yml`** — YAML gọn, chỉ gọi node script:
- Trigger: push tag `v*.*.*` hoặc `workflow_dispatch`
- Patch version → Build VSIX → Prepare assets → Update feed → Create Release → Commit feed

**`.github/workflows/release.js`** — toàn bộ logic xử lý:
- `patch-version`: sửa `AssemblyInfo.cs` + `vsixmanifest`
- `prepare-assets`: rename .vsix, tạo `release_notes.md` từ CHANGELOG.md
- `update-feed`: đọc template, replace `{{VERSION}}` / `{{DOWNLOAD_URL}}` / `{{REPO_URL}}` / `{{UPDATED}}`, ghi ra `vsixfeed.xml`

**Version bump:** 3.8 → 3.9

---

## 2026-03-22 - v3.8: Fix TodoTemplates + TODO Start gated by parent task state

**Version bump:** 3.7 → 3.8

## 2026-03-22 - v3.7: Fix ripgrep logging; fix Checksum canonical form; fix History refresh; fix TODO button states; fix TodoTemplate UX

**Version bump:** 3.6 → 3.7

## 2026-03-22 - Project Helper: panel chuyên biệt quản lý .sln và .csproj

**Version bump:** 3.3 → 3.4

## 2026-03-22 - Solution File Browser: quét .sln và .csproj trong DirectoryRootDhHosCodePath với cache TTL

## 2026-03-22 - Report Checksum: SHA-256 để phát hiện can thiệp từ bên ngoài

## 2026-03-22 - URL Duplicate Check: kiểm tra lịch sử trước khi fetch Gitea

## 2026-03-22 - Redesign Task Timer: chỉ Bắt đầu và Tạm ngưng với lý do

## 2026-03-22 - Redesign TODO: Bắt đầu / Tạm ngưng / Kết thúc

## 2026-03-22 - TODO Templates: chọn nhanh task con từ cấu hình

**Version bump:** 3.2 → 3.3

# Changelog

## 2026-03-21 - Đổi tên project: Rename toàn bộ từ VS2017ExtensionTemplate sang dh-codetask-extension

## 2026-03-21 - Thêm top-level menu: Bổ sung menu "DH Codetask Extension" trên menu bar Visual Studio

## 2026-03-21 - Triển khai DevTaskTracker v3.0: Gitea Task Tracking, Time Tracking, TODO, Reports, History Browser

## 2026-03-21 - Sửa lỗi build CS8179/CS8137 ValueTuple không hỗ trợ .NET 4.6

## 2026-03-21 - Sửa lỗi build CS8314 pattern matching generic type C# 7.0

## 2026-03-21 - Loại bỏ Settings form, chỉ giữ Settings JSON

## 2026-03-21 - Thêm AppLogger: ghi log đồng thời ra file và VS Output Window

## 2026-03-21 - Thêm tính năng Resume task từ lịch sử

## 2026-03-21 - Thêm tính năng Open URL trong cả 2 panel và ReportDetailDialog

## 2026-03-21 - Bật Topmost cho các dialog cấu hình và chi tiết report

**Version bump:** 3.1 → 3.2
