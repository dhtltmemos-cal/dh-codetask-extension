# Changelog

## 2026-03-21 - Đổi tên project: Rename toàn bộ từ VS2017ExtensionTemplate sang dh-codetask-extension

**Nguyên nhân:** Code gốc lấy từ template chưa được đặt tên phù hợp với dự án thực tế.

**Thay đổi:**
- Namespace `VS2017ExtensionTemplate` → `DhCodetaskExtension`
- Class `MyPackage` → `DhCodetaskPackage`
- File `MyPackage.cs` → `DhCodetaskPackage.cs`
- File `VSCommandTable.vsct` → `CommandTable.vsct`
- File `VSPackage.resx` → `DhCodetaskPackage.resx`
- File `VS2017ExtensionTemplate.csproj` → `DhCodetaskExtension.csproj`
- File `VS2017ExtensionTemplate.sln` → `DhCodetaskExtension.sln`
- Output pane title: `"VS2017 Extension Template"` → `"DH Codetask Extension"`
- Tool window title: `"Extension Template"` → `"DH Codetask"`
- Menu items: `"VS2017 Extension Template Settings"` → `"DH Codetask Extension Settings"`
- AppData folder: `"VS2017ExtensionTemplate"` → `"DhCodetaskExtension"`
- Config files: `DhCodetaskExtension.config.xml`, `DhCodetaskExtension.json`
- VSCT symbols: `guidMyPackage` → `guidDhCodetaskPackage`, `guidMyPackageCmdSet` → `guidDhCodetaskCmdSet`
- Cập nhật `README.md` với tài liệu mới
- Cập nhật `AssemblyInfo.cs` với thông tin mới
- Cập nhật `source.extension.vsixmanifest` với tên và mô tả mới
