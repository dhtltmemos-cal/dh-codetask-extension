## 2026-03-22 15:30 — Task task-2026-03-22-02.md

**Yêu cầu:** Cache file .sln/.csproj hoạt động chưa; tối ưu load chậm; thêm tìm kiếm nội dung bằng ripgrep.

**Triển khai:**

**Cache verification:** `SolutionFileService` đã có cache TTL đúng theo `SolutionFileCacheMinutes` (default 20). Bổ sung `GetCacheAgeDisplay()` để status bar hiển thị tuổi cache ("5m 20s trước").

**Tối ưu scan:** Đổi từ `Directory.GetFiles` (eager, allocates toàn bộ danh sách trước) sang `Directory.EnumerateFiles` (lazy) + `Parallel.ForEach` với `ConcurrentBag<SolutionFileEntry>` — tận dụng đa nhân CPU, giảm thời gian scan đáng kể trên thư mục lớn.

**Ripgrep content search:**
- `AppSettings`: thêm field `RipgrepPath` (path tới `rg.exe`)  
- `Core/Models/RipgrepSearchResult.cs`: model mới cho một dòng kết quả tìm kiếm
- `ProjectHelperViewModel`: constructor nhận thêm `Func<AppSettings>`, thêm `ContentQuery`, `SearchResults`, `IsSearching`, `SearchStatus`; commands `SearchContentCommand` / `CancelSearchCommand` / `ClearSearchCommand`; hàm `RunRipgrep()` chạy `rg --json --line-number --max-count 500`, parse JSON output, hỗ trợ `CancellationToken` để hủy search real-time
- `ProjectHelperControl.xaml`: thêm Expander "🔍 Tìm kiếm nội dung (ripgrep)" ở cuối panel — input regex + Enter/button Tìm, nút Hủy khi đang search, nút ✕ xóa kết quả, danh sách kết quả hiển thị file:dòng + content với nút 📂 Mở / 📋 Copy
- `DevTaskTrackerPackage.cs`: truyền `() => Settings` vào `ProjectHelperViewModel` constructor

**Cấu hình ripgrep trong settings.json:**
```json
{
  "RipgrepPath": "C:\\tools\\rg.exe"
}
```

**Version bump:** 3.4 → 3.5

## 2026-03-22 14:00 — Task task-2026-03-22-01.md

**Yêu cầu:** Tách nghiệp vụ list file .sln/.csproj ra khỏi Tracker panel, tạo panel chuyên biệt `Project Helper`.

**Triển khai:**

- Tool window mới `Project Helper` (View > Other Windows > Project Helper, hoặc menu DH Codetask Extension)
- Filter loại file: Tất cả / .sln / .csproj (RadioButton toggle)
- Sort: Tên A→Z / Tên Z→A / Mới nhất / Cũ nhất
- Search real-time theo tên file hoặc đường dẫn, nút ✕ xóa filter
- Mỗi item hiển thị: badge màu (tím = SLN, xanh = PROJ), tên file, thư mục, ngày sửa cuối
- 3 nút hành động: 📂 Mở VS (Process.Start với UseShellExecute), 📋 Copy path, 📁 Folder (mở Explorer)
- Click trực tiếp vào row → mở file
- Nút 🔄 trong header = force rescan bỏ qua cache
- Load lần đầu dùng cache nếu còn hạn (SolutionFileCacheMinutes)
- Xóa toàn bộ SolutionFiles logic khỏi TrackerViewModel và TrackerControl
- Tracker panel: thêm nút "📁 Projects" → mở Project Helper

## 2026-03-22 09:00 — Task task-2026-03-22-00.md

**Yêu cầu:** Triển khai 6 yêu cầu từ `docs/tasks/task-2026-03-22-00.md`:

1. **Solution File Browser**
   - Panel "📁 Solution / Project Files" trong Tracker panel (Expander, lazy load)
   - Quét đệ quy từ `DirectoryRootDhHosCodePath` (cấu hình JSON)
   - Sắp xếp a-z theo tên file, lọc real-time theo tên/path
   - Cache kết quả vào `solution-file-cache.json`, hết hạn theo `SolutionFileCacheMinutes` (default 20)
   - Click mở file, nút 📋 copy đường dẫn

2. **Report Checksum (SHA-256)**
   - Mỗi report JSON được tính SHA-256 khi lưu (trước khi có field Checksum), lưu vào field `Checksum`
   - Khi load history, xác minh checksum và hiển thị ✅ OK / ⚠ Thay đổi trên từng row
   - File .md in SHA-256 trong section "🔒 Toàn vẹn dữ liệu"

3. **URL Duplicate Check**
   - Khi fetch URL, kiểm tra lịch sử — nếu URL đã tồn tại thì lấy thông tin từ lịch sử thay vì fetch lại
   - Chuẩn hóa URL: bỏ phần #anchor trước khi so sánh

4. **Redesign Task Timer (Bắt đầu / Tạm ngưng)**
   - Chỉ còn 2 nút: "▶ Bắt đầu" (start/resume) và "⏸ Tạm ngưng"
   - Khi Tạm ngưng hiện dialog chọn lý do từ `TaskPauseReasons` trong settings
   - Lý do ngưng được lưu vào `TimeSession.PauseReason`
   - Tạm ngưng auto-pause toàn bộ TODO đang chạy

5. **Redesign TODO (Bắt đầu / Tạm ngưng / Kết thúc)**
   - 3 nút hành động rõ ràng: ▶ Bắt đầu, ⏸ Tạm ngưng, ✓ Kết thúc
   - Kết thúc đánh dấu done và lưu session time
   - Tổng thời gian cộng dồn theo sessions

6. **TODO Templates**
   - Field `TodoTemplates: []` trong settings.json
   - Expander "📋 Chọn nhanh từ mẫu" xuất hiện khi có template
   - Click template → tự động thêm TODO

# User Changelog

## 2026-03-21 10:00 — Đổi tên project

**Yêu cầu:** Đổi tên toàn bộ project từ `VS2017ExtensionTemplate` sang `dh-codetask-extension`.

## 2026-03-21 11:00 — Thêm top-level menu "DH Codetask Extension"

**Yêu cầu:** Bổ sung menu cha là "DH Codetask Extension" trên menu bar Visual Studio, với menu con "Settings" và "Settings (JSON)".

## 2026-03-21 14:00 — Triển khai DevTaskTracker v3.0

**Yêu cầu:** Đọc docs/Instructions.md, docs/error-skill-devtasktracker.md, docs/rule.md và triển khai toàn bộ theo Instructions.md — DevTaskTracker v3.0.

## 2026-03-21 17:00 — Task task-2026-03-21-00.md

**Yêu cầu:** Loại bỏ menu Setting (form), chỉ giữ lại Settings (JSON); Log mọi thao tác user ra Output Window; Bổ sung nút và menu mở file log / file cấu hình.

## 2026-03-21 20:00 — Task task-2026-03-21-01.md

**Yêu cầu:** Chỉnh lỗi Filter_Week; Thêm Resume task từ lịch sử; Open URL trong cả 2 panel; Dialogs Topmost.
