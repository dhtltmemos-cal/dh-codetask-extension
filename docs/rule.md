# RULE

- Tạo user yêu cầu trên file user_changelog.md ghi nhận lại thông tin user yêu cầu thay đổi theo ngày, giờ.
- Tạo changelog.md khi chỉnh lỗi, thêm tính năng, theo định dạng <YYYY-MM-DD> - <tên tính năng, chỉnh lỗi>: <Diễn giải cụ thể nguyên nhân, mục đích>
- Tạo file `.opushforce.message` để ghi message commit
- Cập nhật Readme.md
- Thay đổi code phải zip lại toàn bộ thông tin, tăng version lên, cấu trúc <dh-codetask-extension>.<version>.<nội dung thay đổi>. Chú ý zip file phải đảm bảo đúng cấu trúc (không chứa {} các file), download về và giải nén là có thể chép đè vào thư mục code hiện tại.
