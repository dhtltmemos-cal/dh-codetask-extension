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

## 2026-03-22 17:30 — Task task-2026-03-22-04.md (v3.7)

(xem CHANGELOG.md)
