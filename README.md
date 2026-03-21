# DH Codetask Extension

Extension hỗ trợ lập trình viên trong Visual Studio 2017, xây dựng trên nền `AsyncPackage` pattern.

## Tính năng

- **Output Window** — Pane log riêng với timestamp
- **Status Bar** — Set text, animation, progress bar
- **Configuration (XML)** — Cấu hình key-value, persist vào `%AppData%\DhCodetaskExtension\`
- **Configuration (JSON)** — Editor JSON với Format / Validate / Diff log khi save
- **Tool Window** — Panel dockable mở từ `View > Other Windows > DH Codetask`
- **Settings Dialog (XML)** — Form WPF mở từ `Tools > DH Codetask Extension Settings...`
- **Settings Dialog (JSON)** — Editor JSON mở từ `Tools > DH Codetask Extension Settings (JSON)...`
- **Options Page** — Tích hợp vào `Tools > Options > DH Codetask Extension > General`

## Cấu trúc thư mục

```
dh-codetask-extension/
├── DhCodetaskExtension.sln
├── LICENSE
├── README.md
├── CHANGELOG.md
├── user_changelog.md
├── .gitignore
├── .opushforce.message
├── docs/
│   └── rule.md
└── src/
    ├── DhCodetaskExtension.csproj
    ├── DhCodetaskPackage.cs          ← Entry point (AsyncPackage)
    ├── DhCodetaskPackage.resx
    ├── PackageGuids.cs
    ├── CommandTable.vsct
    ├── source.extension.vsixmanifest
    ├── packages.config
    ├── Commands/
    │   ├── ShowMainWindow.cs
    │   ├── ShowSettings.cs
    │   └── ShowJsonSettings.cs
    ├── Services/
    │   ├── OutputWindowService.cs
    │   ├── StatusBarService.cs
    │   ├── ConfigurationService.cs
    │   ├── JsonConfigService.cs
    │   └── OptionsService.cs
    ├── ToolWindows/
    │   ├── MainToolWindow.cs
    │   ├── MainToolWindowControl.xaml
    │   ├── MainToolWindowControl.xaml.cs
    │   ├── MainToolWindowState.cs
    │   ├── SettingsDialog.xaml
    │   ├── SettingsDialog.xaml.cs
    │   ├── JsonSettingsDialog.xaml
    │   └── JsonSettingsDialog.xaml.cs
    └── Properties/
        └── AssemblyInfo.cs
```

## Yêu cầu

- Visual Studio 2017 (v15.x)
- .NET Framework 4.6
- Visual Studio SDK

## Bắt đầu

1. Mở `DhCodetaskExtension.sln` trong Visual Studio 2017
2. Restore NuGet packages
3. Build và chạy (F5) để mở VS Experimental Instance
4. Vào `View > Other Windows > DH Codetask` để mở Tool Window

## Tùy chỉnh

1. **Thay GUID**: Tạo GUID mới trong `PackageGuids.cs` và `CommandTable.vsct`
2. **Thêm tính năng**: Thêm button vào `MainToolWindowControl.xaml` và handler trong `.xaml.cs`
3. **Thêm service**: Tạo class mới trong `Services/`, đăng ký trong `DhCodetaskPackage.cs`
4. **Đổi cấu hình**: Cập nhật `DefaultJson` trong `JsonConfigService.cs` hoặc `Defaults` trong `ConfigurationService.cs`

## Versioning

Format: `dh-codetask-extension.<version>.<nội-dung-thay-đổi>.zip`

Xem `CHANGELOG.md` để biết lịch sử thay đổi.
