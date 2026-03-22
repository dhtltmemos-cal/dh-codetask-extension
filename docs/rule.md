# RULE

- Tạo user yêu cầu trên file `USER_CHANGELOG.md` ghi nhận lại thông tin user yêu cầu thay đổi theo ngày, giờ. (những thay đổi sau cùng nằm ở đầu file)
- Cập nhật `USER_CHANGELOG.md` khi chỉnh lỗi, thêm tính năng, theo định dạng <YYYY-MM-DD> - <tên tính năng, chỉnh lỗi>: <Diễn giải cụ thể nguyên nhân, mục đích> (những thay đổi sau cùng nằm ở đầu file)
- Tạo file `.opushforce.message` để ghi message commit
- Cập nhật `README.md`
- Thay đổi code phải zip lại toàn bộ thông tin, tăng version lên, cấu trúc <dh-codetask-extension>.<version>.<nội dung thay đổi>. Chú ý zip file phải đảm bảo đúng cấu trúc (không chứa {} các file), download về và giải nén là có thể chép đè vào thư mục code hiện tại.
- Khi thêm các cấu trúc mới lưu ý dotnet hỗ trợ hiện là 4.5, 4.6 thôi.
- Khi có thay đổi cấu trúc nghiệp vụ, lưu trữ, cấu trúc data.... thì phải cập nhật `README.md`, `Instructions.md`, `ProjectStructure.md`, `DeveloperGuide.vi.md` phù hợp với cái mới (Đảm bảo lúc nào cũng mới, để khi agent khác nhìn vào là thực hiện được)
