## 2026-03-22 17:30 — Task task-2026-03-22-04.md (v3.7)

**Yêu cầu:** 4 vấn đề từ `docs/tasks/task-2026-03-22-04.md`

**1. Fix ripgrep không có kết quả + bổ sung log đầy đủ**

Thêm logging chi tiết ra Output Window: lệnh đầy đủ, thư mục tìm, thời gian thực thi (ms), exit code, số kết quả. Quan trọng nhất: **log toàn bộ stderr** của ripgrep — đây là nguồn debug chính khi không có kết quả. Fix thêm: thêm `--` trước pattern (ngăn pattern bị parse là flag), sửa `RelativePath` tính đúng từ scan root.

**Files:** `src/ViewModels/ProjectHelperViewModel.cs`

**2. Fix Task History refresh không hoạt động**

Nguyên nhân: `HistoryQueryService` cache không bị invalidate khi user nhấn nút 🔄, dẫn đến dữ liệu cũ được trả về. Fix: thêm `InvalidateCache()` method, `BtnRefresh_Click` gọi `RefreshAsync(forceInvalidate: true)`, package wire `() => _historyRepo.InvalidateCache()` vào HistoryViewModel.

**Files:** `src/Core/Services/HistoryQueryService.cs`, `src/ViewModels/HistoryViewModel.cs`, `src/ToolWindows/HistoryControl.xaml.cs`, `src/DevTaskTrackerPackage.cs`

**3. Fix Checksum báo "thay đổi" sai**

Nguyên nhân: Archive dùng `JsonConvert.SerializeObject` (có thể có `"Checksum": null`), còn verify dùng `JObject.Remove("Checksum") → ToString()` — hai chuỗi JSON khác nhau nên hash khác nhau. Fix: cả hai phía dùng cùng JObject round-trip: `Serialize → Parse → Remove("Checksum") → ToString`.

**Files:** `src/Providers/StorageProviders/JsonStorageService.cs`, `src/Core/Services/HistoryQueryService.cs`

**4. Fix TODO List buttons + Templates**

- **TODO button states:** `RaiseStateChanged()` chỉ raise PropertyChanged nhưng không raise `CanExecuteChanged` → WPF không re-evaluate CanExecute → buttons không disable đúng. Fix: thêm `RaiseCanExecuteChanged()` cho Start/Pause/Stop/Complete commands trong `RaiseStateChanged()`.
- **TodoTemplate:** Trước đây click template auto-add ngay, không enable nút Add. Đổi thành: click template chỉ điền text vào input box, nút Add tự enable (nhờ `NewTodoText` setter raise `AddTodoCommand.CanExecuteChanged`).

**Files:** `src/ViewModels/TodoItemViewModel.cs`, `src/ViewModels/TrackerViewModel.cs`

**Version bump:** 3.6 → 3.7

## 2026-03-22 16:30 — Task task-2026-03-22-03.md

**Yêu cầu:** 4 vấn đề từ `docs/tasks/task-2026-03-22-03.md`:

1. **Fix duplicate TaskPauseReasons / TodoTemplates** — Nguyên nhân: Newtonsoft.Json mặc định APPEND vào List đã có giá trị sẵn khi deserialize, nên mỗi lần mở Settings rồi Save là danh sách bị nhân bản. Fix: thêm `[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]` trên 2 property `TaskPauseReasons` và `TodoTemplates` trong `AppSettings.cs`.

2. **Fix Win32Exception khi chạy ripgrep** — Nguyên nhân: VS2017 devenv.exe là tiến trình 32-bit; nếu rg.exe là bản 64-bit, Windows trả về error 193 (ERROR_BAD_EXE_FORMAT). Fix: thử chạy trực tiếp trước; nếu nhận Win32Exception với code 193/216/14001 thì fallback sang cmd.exe (vốn là 64-bit trên Windows x64) để spawn rg.exe.

3. **Filter file .sln/.csproj chậm** — Bỏ auto-search khi gõ. Thêm nút **Tìm** và Enter key binding để kích hoạt filter. Sort/type vẫn auto-apply vì không cần gõ text.

4. **Đưa Content Search lên top-level tab** — Thay Expander ở đáy panel bằng 2 RadioButton mode toggle ở đầu: `📄 Danh sách File` | `🔍 Tìm nội dung`. Mỗi mode chiếm toàn bộ chiều cao panel. ViewModel thêm `IsFileMode`/`IsSearchMode` property. Code-behind xử lý `Mode_Files_Checked` / `Mode_Search_Checked`.

**Files thay đổi:**
- `src/Core/Models/AppSettings.cs`: thêm `[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]`
- `src/ViewModels/ProjectHelperViewModel.cs`: `IsFileMode`/`IsSearchMode`, `FilterCommand`, `FilterKeyword` không auto-apply, `RunRipgrep` với fallback cmd.exe
- `src/ToolWindows/ProjectHelperControl.xaml`: mode toggle, file panel, search panel top-level
- `src/ToolWindows/ProjectHelperControl.xaml.cs`: mode toggle handlers, `SearchResult_Click`

**Version bump:** 3.5 → 3.6

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

**Yêu cầu:** Triển khai 6 yêu cầu từ `docs/tasks/task-2026-03-22-00.md`.

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
