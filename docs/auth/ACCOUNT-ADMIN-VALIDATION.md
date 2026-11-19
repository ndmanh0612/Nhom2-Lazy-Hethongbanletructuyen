# Kiểm chứng Tài khoản và Quản trị — 19/11/2026

Phạm vi: FR-03 / FR-15, dựa trên `tricong@352de8065d202073aa9d504b0837fe261602bb5c`.
Không thay schema database hoặc phiên bản dependency của dự án.

## Kết quả xác nhận

| Kiểm tra | Kết quả |
|---|---|
| Restore toàn solution | Thành công với package đúng phiên bản từ NuGet chính thức và cache local |
| Build Release toàn solution | Thành công, 0 lỗi; 7 cảnh báo ở Catalog/test có sẵn ngoài phần sửa |
| Unit tests | 184/184 đạt, 0 bỏ qua |
| Architecture tests | 7/7 đạt, 0 bỏ qua |
| Integration tests, PostgreSQL thật | 199/199 đạt, 0 bỏ qua |
| Tổng dotnet test | 390/390 đạt |
| JavaScript regression | 9/9 đạt |
| Kiểm tra patch | Áp dụng/đảo ngược kiểm tra thành công; không có dòng thêm mới dư khoảng trắng cuối dòng |
| Bảo toàn phạm vi | 213 file ngoài phạm vi khớp hash blob của nhánh nền |

Integration test bao gồm kiểm tra sở hữu địa chỉ, payload cố nâng quyền, tạo nhân viên đồng thời,
actor mất quyền quản trị, hai admin khóa nhau và giới hạn tần suất xác thực.
Các bài hồi quy hiện có về đăng nhập, phiên, CSRF và các luồng bán hàng cũng đã chạy đạt.

## Bằng chứng chạy

Lệnh build:
```powershell
dotnet build PhoneStore.slnx --configuration Release --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false --tl:off
```

Bộ kiểm thử chuẩn được người dùng chạy qua script local ngoài môi trường hạn chế socket.
Kết quả được đọc lại từ log và ba file TRX mới, có outcome Completed:

| File TRX | Tổng | Đạt | Lỗi |
|---|---:|---:|---:|
| account-admin_net10.0_20261119011639.trx | 7 | 7 | 0 |
| account-admin_net10.0_20261119011845.trx | 199 | 199 | 0 |
| account-admin_net10.0_20261119011906.trx | 184 | 184 | 0 |

Log và TRX nằm dưới `.verification/` của bản sao local, không đưa vào commit.

Lệnh test tương đương:
```powershell
dotnet test PhoneStore.slnx --configuration Release --no-build --no-restore -m:1
node --test tests/frontend/identity-api.test.cjs
```

PostgreSQL test dùng cổng riêng 55439; fixture tạo/xóa database tên ngẫu nhiên.
Không sử dụng database ứng dụng hoặc database production.
Các lần VSTest bị timeout/test bị chặn socket trong môi trường hạn chế trước đó
không được tính là kết quả đạt; chúng được thay bằng bằng chứng chạy chuẩn ở trên.

## Git và CI

Chỉ cập nhật nhánh `tricong` của:
https://github.com/ndmanh0612/Nhom2-Lazy-Hethongbanletructuyen

Việc xuất bản dùng parent commit đã xác minh, giữ nguyên các file ngoài phạm vi,
cập nhật nhánh theo fast-forward và kiểm tra head mong đợi. Không force push hoặc merge nhánh khác.
Workspace gốc ở main và ba file báo cáo untracked có sẵn được giữ nguyên.

CI bổ sung trigger push cho tricong và bước chạy test JavaScript.
Các kết quả trong bảng trên là kiểm chứng local; trạng thái CI phải kiểm tra trên GitHub Actions
gắn với đúng SHA sau khi xuất bản, không suy ra từ kết quả local.

## Giới hạn

- Chưa chạy browser E2E cho toàn bộ màn hình; bộ JavaScript kiểm tra API client và parse script.
- Limiter theo IP trong từng process; triển khai nhiều replica cần cơ chế giới hạn phân tán.
- Cấu hình proxy tin cậy phải phù hợp môi trường triển khai.
- Mật khẩu bị giới hạn 72 byte UTF-8; tài khoản cũ dùng mật khẩu dài hơn cần quy trình reset phù hợp.
- Audit hiện là log ứng dụng; chưa có bảng audit bất biến.
- Quên mật khẩu tự phục vụ qua email/SMS/OTP chưa thuộc phần đã triển khai.

Chi tiết chức năng/API và kịch bản demo: [ACCOUNT-ADMIN-COMPLETION.md](ACCOUNT-ADMIN-COMPLETION.md).
