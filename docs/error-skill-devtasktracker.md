# error-skill-devtasktracker.md

## Mục đích

Tài liệu này là danh sách **các lỗi agent phải tránh lặp lại** khi sinh/chỉnh sửa project `DevTaskTracker` cho VS2017 VSIX.  
Chỉ liệt kê lỗi cần tránh. Không lặp lại cùng một lỗi ở nhiều mục.

---

## 1) Wiring / Dependency

- Khởi tạo sai constructor giữa `Package`, `Service`, `ViewModel`, `Provider`
- Gán sai kiểu giữa interface và implementation
- Đăng ký service/provider/generator nhưng không thực sự `register` vào factory/composite
- Attach event/webhook nhiều lần gây subscribe trùng
- Re-init dependency graph mà không detach/cleanup instance cũ

---

## 2) Compile / Project System

- Dùng API không tồn tại trong .NET Framework 4.6.1 / VS2017
- Gọi method/property/class không tồn tại trong project
- Thiếu reference bắt buộc như `EnvDTE`, VS SDK assemblies
- Khai báo attribute/package resource nhưng thiếu file tương ứng
- Sai signature giữa interface và class implement

---

## 3) VSIX Packaging

- Manifest tham chiếu icon/preview/resource không tồn tại
- Đăng ký command/menu nhưng thiếu `.vsct` / resource command
- Thiếu asset cần thiết để build VSIX sạch trên VS2017
- Dùng cấu hình/package style không phù hợp VS2017 (`PackageReference` thay vì `packages.config` nếu project đang target kiểu cũ)

---

## 4) UI / XAML / Binding

- XAML bind tới property không tồn tại trong ViewModel
- Grouping/sorting/filtering bind tới field không có trong row model
- Event UI gọi method package/viewmodel không tồn tại
- Designer/runtime phụ thuộc tên property “ảo”, không có compile-time safety
- Double-click / command / dialog mở file nhưng không nối đúng handler thực tế

---

## 5) Time Tracking

- Cho phép transition state sai thứ tự (`Start`, `Pause`, `Resume`, `Stop`)
- Cho phép nhiều session mở cùng lúc cho cùng một task/todo
- TODO đã `Done` nhưng vẫn còn session đang chạy
- Pause task mà không auto-pause các TODO đang chạy
- Tính elapsed không nhất quán giữa runtime timer và session data

---

## 6) Git Flow

- Tìm repo root từ sai context thay vì solution/repo hiện tại
- Thực thi git khi không kiểm tra `git` có trong PATH
- Commit/push fail nhưng flow vẫn giả định thành công
- Không lấy và lưu commit hash sau commit/push
- Không publish commit event sau git action

---

## 7) Report / Archive

- Sinh report ở nhiều nơi khác nhau, làm flow archive bị phân tán
- Không dùng một entry point thống nhất cho lưu report
- `CompletionReport` vẫn mutable sau khi archive
- Không sinh đồng thời JSON + Markdown theo đúng flow
- Không publish `ReportSavedEvent` sau khi lưu thành công

---

## 8) File I/O / Storage

- Ghi file kiểu trực tiếp gây corrupt khi app/VS crash
- Dùng `File.Move(temp, path)` khi file đích đã tồn tại mà không xử lý overwrite
- Không backup/xử lý file JSON corrupt
- Không phân tách rõ `current-task.json`, `settings.json`, `history`
- Không invalidate cache khi history thay đổi

---

## 9) EventBus / Notification

- Publish event theo kiểu blocking làm subscriber ảnh hưởng publisher
- Để exception từ subscriber làm hỏng flow chính
- Không retry webhook khi lỗi tạm thời
- Không giới hạn/guard việc subscribe lặp
- Không log lỗi event/webhook đủ để truy vết

---

## 10) Settings / Config

- Hard-code URL, token, path, format thay vì đọc settings
- Có setting trong model domain không đúng trách nhiệm
- Không validate input settings trước khi apply
- Save settings nhưng không re-apply đúng các dependency liên quan
- Có setting khai báo nhưng không được dùng thực tế

---

## 11) History Browser

- `HistoryViewModel` và `HistoryControl.xaml` không khớp nhau
- Group theo ngày nhưng không có field ngày nhóm theo local timezone
- Pagination/search/filter chỉ có UI nhưng không có logic thật
- Summary bar bind sai field hoặc field không tồn tại
- Delete/export/open detail/open file không nối đủ command flow

---

## 12) Async / Threading

- Dùng `async void` ngoài event handler
- Không chuyển về UI thread khi thao tác VS UI
- Fire-and-forget task mà không kiểm soát lỗi
- Boundary HTTP/Git/File I/O không có `try/catch`
- Không truyền `CancellationToken` cho các thao tác async quan trọng

---

## 13) Gitea Provider

- Parse issue URL sai pattern owner/repo/number
- Không truyền token đúng header `Authorization: token ...`
- Không timeout rõ ràng cho request
- Không strip markdown trước khi map description
- Throw exception ra ngoài thay vì trả `(null, errorMessage)`

---

## 14) Chất lượng kiến trúc

- Vi phạm Open/Closed: thêm provider mới nhưng phải sửa class cũ
- Trộn domain logic vào UI code-behind
- Để nhiều nguồn sự thật cho cùng một flow
- Không có một service chịu trách nhiệm cuối cùng cho completion flow
- Để model, settings, storage, UI rò rỉ trách nhiệm lẫn nhau

## 15) Cú pháp C# không tương thích .NET 4.6 / C# 7.0

### ValueTuple / Tuple syntax

- **KHÔNG** dùng tuple return type `(bool Valid, JObject Obj, string Error)` — .NET 4.6 không có `System.ValueTuple`, gây CS8179 / CS8137.
- **KHÔNG** dùng `var (a, b) = SomeMethod()` (deconstruction).
- **THAY THẾ bắt buộc**: khai báo `private sealed class XxxResult { public bool Valid; public T Obj; public string Error; }` và trả về instance của class đó.

### Pattern matching

- **KHÔNG** dùng `if (x is SomeType y)` trên generic type parameter — gây CS8314 trong C# 7.0.
- **THAY THẾ**: `var y = x as SomeType; if (y != null)`.

### Các tính năng C# cần tránh trên .NET 4.6 / C# 7.0

| Tính năng                            | Lỗi            | Thay thế                             |
| ------------------------------------ | -------------- | ------------------------------------ |
| `(T1, T2) Method()` return tuple     | CS8179, CS8137 | `private sealed class ResultXxx { }` |
| `var (a, b) = expr`                  | CS8179         | Truy cập `.PropertyA`, `.PropertyB`  |
| `if (x is T y)` trên generic         | CS8314         | `var y = x as T; if (y != null)`     |
| `switch (x) { case T y: }`           | CS8314         | `if / else if` với `as` cast         |
| `string interpolation` dạng `$"..."` | OK — C# 6      | (cho phép)                           |
| `?.` null-conditional                | OK — C# 6      | (cho phép)                           |

## 16) Tránh lỗi CS1026

```
⚠ Lỗi thường gặp — CS1026
Không dùng "smart quotes" trong C#
❌ Sai
"Dang tim: "{0}"..."
✅ Đúng
"Dang tim: \"{0}\"..."
Ký tự " " (curly/smart quote) là Unicode — C# không nhận. Chỉ dùng \" để escape dấu ngoặc kép bên trong string.
```

### Quy tắc kiểm tra trước khi sinh code

1. Mỗi method trả về nhiều giá trị → **phải dùng class kết quả riêng**, không dùng tuple.
2. Mỗi lần dùng `is` hoặc `switch` với type → kiểm tra có phải generic type parameter không, nếu có thì dùng `as`.
3. Tìm kiếm `ValueTuple`, `(bool `, `(string `, `(int ` trong return type → **flag ngay**.

---

## Quy tắc bắt buộc cho agent

- Không lặp lại bất kỳ lỗi nào ở trên
- Mỗi lần sửa phải kiểm tra lại theo từng nhóm lỗi
- Ưu tiên: build pass → wiring đúng → flow đúng → hardening
