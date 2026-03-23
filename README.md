# DH Codetask Extension — DevTask Tracker v3.10

Extension theo dõi task, time tracking và TODO trực tiếp trong Visual Studio 2017, tích hợp Gitea.

## Tính năng v3.10 (mới) — Issue Creator Info & Connection Info

Sau khi Fetch issue từ Gitea, panel Tracker hiển thị thêm:

| Thông tin | Mô tả |
|-----------|-------|
| 👤 Tạo bởi | Username người tạo issue trên Gitea |
| 📅 Ngày tạo | Thời điểm issue được tạo (local time) |
| 🖥 IP Server | IP server/host trích xuất từ body issue |
| 🗄 DB | Tên database/CSDL trích xuất từ body issue |
| 🔌 Port | Port kết nối trích xuất từ body issue |

Mỗi thông tin kết nối có nút **📋 copy** để copy nhanh vào clipboard.

### Các format body issue được hỗ trợ

```
ten_csdl tra_minhquang_full_13032026
IP Server 192.168.50.79
port_csdl 5437
```

```
🗄️ Thông tin dữ liệu
🧩 DB: emr_omon
🔌 Port: 5433
```

Và nhiều format khác với các keyword: `ip`, `ip server`, `host`, `hostname`, `máy chủ`, `csdl`, `database`, `db`, `ten_csdl`, `tên_csdl`, `port`, `port_csdl`, `cổng`...

### Cấu hình DefaultServerIp

Nếu body issue không ghi rõ IP (vì cả nhóm dùng chung một server), thêm vào `settings.json`:

```json
{
  "DefaultServerIp": "192.168.50.79"
}
```

---

## Tính năng v3.9 — Cập nhật tự động

VS2017 có cơ chế **VSIX Gallery** tích hợp sẵn. Extension khai báo một feed URL, VS tự kiểm tra và thông báo khi có bản mới qua **Tools > Extensions and Updates > Updates**.

| Thành phần | Mô tả |
|-----------|-------|
| `GalleryUrl` trong vsixmanifest | VS đọc Atom feed từ URL này để phát hiện bản mới |
| `docs/vsixfeed.xml` | Feed chuẩn VSIX Gallery Schema, host GitHub Pages |
| `.github/workflows/release.yml` | GitHub Actions tự build + release + cập nhật feed |

## Hướng dẫn thiết lập auto-update (một lần duy nhất)

### Bước 1 — Push repo lên GitHub

```bash
git remote add origin https://github.com/YOUR_USERNAME/dh-codetask-extension.git
git push -u origin main
```

### Bước 2 — Bật GitHub Pages

Vào **Settings > Pages > Source** → chọn `Deploy from branch: main / docs`

### Bước 3 — Cập nhật GalleryUrl trong vsixmanifest

Mở `src/source.extension.vsixmanifest`, thay `YOUR_GITHUB_USERNAME`:

```xml
<GalleryUrl>https://YOUR_USERNAME.github.io/dh-codetask-extension/vsixfeed.xml</GalleryUrl>
```

### Bước 4 — Tạo release mới

```bash
git tag v3.10.0
git push origin v3.10.0
```

GitHub Actions sẽ tự động build, release và cập nhật feed.

---

## Tính năng chính

### Tracker Panel (Tool Window chính)

| Khu vực | Chức năng |
|---------|-----------|
| URL bar | Nhập URL issue Gitea → Fetch tự động, hoặc nhập thủ công |
| Creator info | **v3.10**: Hiển thị username và ngày tạo issue |
| Connection info | **v3.10**: IP/DB/Port trích xuất từ body, nút copy từng field |
| Task info | Title, Labels, Description (có thể chỉnh sửa) |
| Task timer | Start / Pause (với lý do) / Resume với đồng hồ real-time |
| Work Notes | Ghi chú quá trình làm việc |
| TODO list | Thêm/xóa item, mỗi item có timer độc lập, đánh dấu hoàn thành |
| Business Logic | Mô tả nghiệp vụ đã implement |
| Commit message | Tự sinh + chỉnh sửa, nút Regenerate |
| Actions | 🚀 Push & Complete · ⏸ Save & Pause · 📚 History · ⚙ Settings |

### History Browser (Tool Window thứ 2)

- Lọc theo Hôm nay / Tuần này / Tháng này / Tùy chỉnh
- Tìm kiếm theo tiêu đề hoặc Task ID
- Summary bar: tổng task, tổng giờ, giờ trung bình, tỉ lệ TODO hoàn thành
- Danh sách nhóm theo ngày
- Click → xem chi tiết · Double-click → mở file `.md`
- Resume task từ lịch sử vào Tracker
- Export CSV · Xem checksum toàn vẹn dữ liệu

### Project Helper (Tool Window thứ 3)

- Scan và liệt kê file `.sln` và `.csproj` trong thư mục cấu hình
- Sort theo tên / ngày sửa, filter theo tên, filter theo loại
- Mở trong VS, copy path, mở folder
- Tìm kiếm nội dung bằng ripgrep (regex)
- Cache với TTL cấu hình được

---

## Cài đặt & Build

```bash
# 1. Mở VS2017, open DhCodetaskExtension.sln
# 2. Restore NuGet packages: Tools > NuGet Package Manager > Restore
# 3. F5 để debug trong Experimental Instance
# 4. View > DevTask Tracker để mở panel
```

## Cấu hình settings.json (đầy đủ)

```json
{
  "GiteaBaseUrl": "http://gitea.company.com",
  "GiteaToken": "your-personal-access-token",
  "GiteaUser": "john.doe",
  "DefaultServerIp": "192.168.50.79",
  "GitAutoPush": false,
  "GitUserName": "John Doe",
  "GitUserEmail": "john@company.com",
  "StoragePath": "",
  "ReportFormat": "json+markdown",
  "TimeFormat": "hh:mm",
  "HistoryDefaultView": "week",
  "WebhookEnabled": false,
  "WebhookUrl": "",
  "DirectoryRootDhHosCodePath": "C:\\Projects",
  "SolutionFileCacheMinutes": 20,
  "RipgrepPath": "C:\\ripgrep\\rg.exe",
  "TaskPauseReasons": ["Hết giờ làm việc", "Chuyển việc khác", "Lý do khác"],
  "TodoTemplates": ["Viết unit test", "Review code", "Cập nhật docs"],
  "Extensions": {}
}
```

## Cấu trúc dữ liệu lưu trữ

```
%APPDATA%\DhCodetaskExtension\
├── settings.json              ← Cấu hình ứng dụng
├── current-task.json          ← Task đang làm (xóa sau khi hoàn thành)
├── solution-file-cache.json   ← Cache danh sách .sln/.csproj
└── history\
    └── 2024\
        └── 01\
            ├── 20240115_112341_142_add-user-auth.json
            └── 20240115_112341_142_add-user-auth.md
```

---

_DhCodetaskExtension v3.10 — Gitea-first · Issue Creator Info · Connection Info Parser · VSIX Gallery Auto-Update_
