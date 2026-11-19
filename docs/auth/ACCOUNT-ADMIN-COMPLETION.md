# Hoàn thiện Tài khoản và Quản trị — FR-03 / FR-15

## Phạm vi và nguồn đối chiếu

Bản triển khai dựa trên nhánh `tricong`, commit `352de8065d202073aa9d504b0837fe261602bb5c`.
Đối chiếu `docs/JIRA_TASKS.md`, sơ đồ chức năng, IdentityService, các controller, storefront và test hiện có.
Đây là backend .NET 10 có storefront JavaScript thuần. Không thay kiến trúc Modular Monolith và không thay schema database.

| Nghiệp vụ | Bằng chứng trước sửa | Thiếu sót | Thay đổi |
|---|---|---|---|
| Quản lý địa chỉ FR-03 | Address.Update đã có; MeController chỉ GET/POST | Không sửa/xóa được địa chỉ | PUT/DELETE, lọc đồng thời Id và UserId; giao diện sửa/xóa |
| Quản lý nhân viên FR-15 | StaffController có API, storefront chỉ dashboard | Không có giao diện tạo/lọc/khóa/đổi quyền/reset | Bổ sung thao tác quản trị trong tab Quản trị, chỉ ADMIN |
| Đổi mật khẩu | Backend thu hồi mọi phiên; UI thông báo chỉ phiên khác | Phiên cục bộ còn token cũ | Xóa trạng thái đăng nhập và về trang đăng nhập |
| Refresh token trên client | request tự gọi lại không giới hạn; mỗi request tự refresh | Có thể refresh lặp, cạnh tranh xoay token, mất lỗi đăng nhập | Tối đa một lần thử lại, chia sẻ refresh đang chạy, giữ lỗi xác thực gốc |
| Tạo nhân viên đồng thời | Pre-check email rồi SaveChanges | Unique violation có thể trả 500 | Chuyển đúng constraint email thành 409 |
| Đổi quyền | Enum.TryParse và User.ChangeRole chưa chặn enum không định nghĩa/khách hàng nguồn | Có thể nhận enum số qua service hoặc đưa khách vào luồng nhân viên | Whitelist STAFF/ADMIN tại service/domain; chặn CUSTOMER đổi quyền qua API nhân viên |
| Thay đổi quyền đồng thời | Kiểm tra JWT trước transaction; chỉ khóa tài khoản đích | Quyền actor có thể thay đổi trong lúc chờ; hai admin có thể vô hiệu hóa nhau | Advisory transaction lock, kiểm tra lại actor và bảo vệ admin cuối |
| Mật khẩu Unicode | Giới hạn 72 ký tự | BCrypt giới hạn theo byte | Giới hạn tối đa 72 byte UTF-8 cho các input mật khẩu |
| Phân trang nhân viên | Skip dùng phép nhân int | Tràn số với page rất lớn | Validate offset bằng long trước khi query |
| Báo cáo quản trị | Giao diện cộng một trang COD, hiển thị cửa hàng mock, nuốt lỗi | Dashboard dễ hiểu sai tổng và lỗi thành dữ liệu trống | Dùng API dashboard FR-16 có sẵn; hiển thị lỗi thật |
| Chống thử mật khẩu | Không có rate limit | Đăng nhập/đăng ký không giới hạn tần suất | Fixed-window limiter, response 429 chuẩn và Retry-After |
| Truy vết quản trị | Chưa log các thao tác tài khoản tại service | Khó truy actor/target | Log cấu trúc sau commit, không ghi mật khẩu/token/OTP |

## API và tương thích

Các endpoint hiện có được giữ nguyên. Bổ sung:

| Method | Endpoint | Quyền | Body / kết quả |
|---|---|---|---|
| PUT | /api/v1/me/addresses/{addressId} | Đã đăng nhập, chủ sở hữu | receiverName, receiverPhone, addressText; 200 |
| DELETE | /api/v1/me/addresses/{addressId} | Đã đăng nhập, chủ sở hữu | Không có body; 200 |
| GET | /api/v1/admin/reports/dashboard | STAFF / ADMIN, endpoint đã có | Storefront sử dụng tổng từ backend |

Địa chỉ không tồn tại hoặc thuộc người khác đều trả 404 `ADDRESS_NOT_FOUND`.
Xóa địa chỉ sổ địa chỉ không thay đổi địa chỉ đã được chụp trong đơn hàng.
Thông tin hồ sơ chỉ nhận họ tên và điện thoại; role, IsActive và passwordHash không phải trường có thể cập nhật.

API nhân viên không được dùng để đưa CUSTOMER thành STAFF/ADMIN.
Khóa/reset mật khẩu khách hàng qua endpoint quản trị hiện có vẫn được giữ tương thích với test và hành vi trước đó.
Đổi mật khẩu, reset, khóa và đổi quyền tiếp tục thu hồi phiên như thiết kế hiện tại.

Hợp đồng nội bộ `IIdentityService.CreateStaffAsync` và `ResetPasswordAsync` bổ sung actorId.
Các controller đã truyền actorId lấy từ JWT; không nhận actorId do client gửi.

## Transaction và truy vết

Các thao tác tạo nhân viên, đổi quyền, khóa/mở và reset lấy cùng PostgreSQL transaction advisory lock trước các row lock.
Sau khi lấy lock, service đọc lại actor bằng truy vấn không tracking, kiểm tra còn hoạt động và còn ADMIN.
Mọi bước cập nhật user/thu hồi phiên nằm trong cùng transaction.

Bảo vệ này áp dụng các đường ghi qua service; thao tác SQL thủ công phải tự tuân thủ các ràng buộc tương đương.

Log `IdentityAdmin` ghi ActorId, TargetId và loại thao tác sau commit thành công.
Đây là log ứng dụng, không phải bảng audit bất biến hoặc giao diện tra cứu audit.
Cần cấu hình thu gom/lưu log của môi trường triển khai để lưu bền vững.
Reason khóa tài khoản được giới hạn độ dài; chưa bổ sung lưu reason vào database.

## Cấu hình giới hạn tần suất và reverse proxy

```text
IdentityRateLimit__PermitLimit=20
IdentityRateLimit__WindowSeconds=60
```

Đăng ký và đăng nhập chia sẻ hạn mức theo địa chỉ IP trong từng process.
Limiter này không thay thế rate limit phân tán/WAF khi chạy nhiều replica.
Integration fixture nâng hạn mức cho bộ hồi quy; test limiter riêng đặt hạn mức 1 để kiểm tra 429.

Không còn tin mọi X-Forwarded-* từ mọi máy. Giữ mặc định loopback của framework.
Khi triển khai sau reverse proxy khác loopback, cấu hình chính xác IP proxy tin cậy bằng:
`ReverseProxy__KnownProxies__0=<IP-proxy-tin-cay>`.
Không điền IP của người dùng cuối vào danh sách này. Nếu chưa cấu hình proxy ngoài loopback,
các request qua proxy dùng chung IP của proxy và có thể chia sẻ hạn mức.

## Kiểm tra thủ công

1. Đăng nhập CUSTOMER → Tài khoản → Địa chỉ: thêm, sửa, hủy sửa, xóa.
2. Dùng tài khoản khác gọi PUT/DELETE địa chỉ ở bước 1: phải 404 và dữ liệu gốc không đổi.
3. Cập nhật hồ sơ: tên hiển thị ở trạng thái đăng nhập phải được cập nhật.
4. Đổi mật khẩu: client về trang đăng nhập, mật khẩu mới dùng được, token cũ bị từ chối.
5. Đăng nhập ADMIN → Tài khoản → Quản trị: xem tổng từ API báo cáo.
6. Tạo STAFF, tìm/lọc/phân trang, đổi quyền, khóa/mở và đặt lại mật khẩu.
7. Kiểm tra tài khoản bị tác động phải đăng nhập lại; ADMIN không tự khóa hoặc tự đổi quyền.
8. Đăng nhập STAFF: xem báo cáo nhưng không thấy điều khiển nhân viên; gọi API nhân viên trực tiếp phải 403.
9. Đăng nhập sai: giữ thông báo lỗi backend; không tự refresh cho endpoint login.
10. Gửi quá hạn mức đăng ký/đăng nhập: 429 với code và Retry-After.

## Kiểm tra tự động

```powershell
dotnet restore PhoneStore.slnx
dotnet build PhoneStore.slnx --configuration Release --no-restore
dotnet test PhoneStore.slnx --configuration Release --no-build
node --test tests/frontend/identity-api.test.cjs
```

PostgreSQL test phải sẵn sàng. Override `PHONESTORE_TEST_ADMIN_CONNECTION` nếu dùng cổng khác;
fixture chỉ tạo/xóa database test tên ngẫu nhiên.

Bổ sung:
- IdentityValidationRegressionTests: domain, enum, UTF-8 password, validation và phân trang.
- AccountAdministrationRegressionTests: ownership, payload nâng quyền, email đồng thời,
  actor bị thu hồi quyền, hai admin khóa nhau, rate limit.
- identity-api.test.cjs: 9 bài hồi quy client và parse JavaScript.

## Phần chưa mở rộng

Quên mật khẩu tự phục vụ qua email/SMS, OTP, bảng audit bền vững và quản lý khách hàng đầy đủ
chưa được xác định thành luồng hoàn chỉnh trong các yêu cầu đối chiếu.
Không thêm endpoint giả lập gửi email/SMS hoặc báo tính năng này đã hoạt động.
Các module hàng hóa, tồn kho và vòng đời đơn hàng giữ nguyên ngoài phần gọi báo cáo đã có.

Kết quả thực thi, trở ngại môi trường và trạng thái Git được ghi riêng trong `ACCOUNT-ADMIN-VALIDATION.md`.
