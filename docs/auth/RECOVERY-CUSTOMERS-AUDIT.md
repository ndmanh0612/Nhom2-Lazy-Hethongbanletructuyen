# Khôi phục mật khẩu, quản lý khách hàng và audit

Đợt bổ sung trên `tricong@d43054e65bf882c0716347c26ae17734512c7606`.
Không sửa module đơn hàng, không xóa tài khoản có quan hệ nghiệp vụ.

## Khôi phục mật khẩu

| Method | API | Body | Kết quả |
|---|---|---|---|
| POST | `/api/v1/auth/forgot-password` | `email`, `channel`: `email` hoặc `sms` | `challengeId`, thông báo chung |
| POST | `/api/v1/auth/verify-reset-otp` | `challengeId`, `code` 6 chữ số | `resetToken` |
| POST | `/api/v1/auth/reset-password` | `challengeId`, `resetToken`, `newPassword` | Thành công, phải đăng nhập lại |
| POST | `/api/v1/me/phone-verification` | `currentPassword` | Challenge xác minh số trong hồ sơ; cần đăng nhập |
| POST | `/api/v1/me/phone-verification/confirm` | `challengeId`, `code` | Xác minh số thuộc tài khoản đang đăng nhập |

OTP được sinh bằng CSPRNG, tồn tại 5 phút, tối đa 5 lần thử. Database chỉ lưu HMAC-SHA256
gắn với ID và mục đích challenge; key HMAC nằm ngoài database. Xác minh OTP thành công
phát token ngẫu nhiên 256 bit có hạn 5 phút, database chỉ lưu SHA-256 của token.
OTP và token dùng một lần; reset đồng thời được tuần tự hóa bằng row lock.
Mật khẩu mới áp dụng cùng chính sách hiện tại: tối thiểu 8 ký tự, chữ và số, tối đa 72 byte UTF-8.
Mọi phiên và challenge còn mở bị thu hồi khi reset/đổi mật khẩu, khóa hoặc thu hồi phiên.

Không trả OTP trong API, không ghi OTP/token/password vào audit. Client giữ token trong bộ nhớ
của trang, không đưa lên URL/localStorage/sessionStorage. Không dùng refresh để thử lại lỗi recovery.

Email không tồn tại, tài khoản bị khóa hoặc số chưa xác minh nhận cùng cấu trúc thông báo chung
và ID ngẫu nhiên; không phát challenge có thể sử dụng. Yêu cầu lại trong 60 giây không gửi thêm mã.
UI chặn gửi lại quá sớm; yêu cầu bị cooldown không thay thế challenge hiện hữu.
Các endpoint recovery dùng chung giới hạn IP với đăng ký/đăng nhập, theo từng process.

SMS chỉ gửi đến `VerifiedPhone`, không tin trường Phone chưa xác minh. Để đăng ký SMS,
người dùng phải cung cấp mật khẩu hiện tại và OTP gửi tới số trong hồ sơ. Số chuẩn E.164
được bảo vệ bằng unique index; hai tài khoản không thể cùng xác minh một số.
Đổi số trong hồ sơ xóa trạng thái xác minh và vô hiệu các challenge đang mở.
Số Việt Nam 10 chữ số bắt đầu bằng 0 được chuẩn hóa sang +84.

## Cấu hình gửi thật

Cấu hình qua biến môi trường của process API hoặc secret manager. Không ghi secret vào Git.
File `.env` không tự được ASP.NET đọc; khi dùng Docker phải đưa các biến này vào environment
của container API (qua cấu hình triển khai/compose override).

```text
Recovery__HashKey=<key ngẫu nhiên, tối thiểu 32 byte UTF-8; dùng chung giữa các replica>
Recovery__Smtp__Host=<SMTP host>
Recovery__Smtp__Port=587
Recovery__Smtp__From=<địa chỉ gửi được phép>
Recovery__Smtp__Username=<SMTP username>
Recovery__Smtp__Password=<SMTP secret>
Recovery__Sms__AccountSid=<Twilio Account SID>
Recovery__Sms__AuthToken=<Twilio secret>
Recovery__Sms__From=<số Twilio E.164 được phép gửi SMS>
```

SMTP bắt buộc STARTTLS, không hỗ trợ implicit TLS cổng 465. SMS sử dụng Twilio Messages API,
không đi theo redirect; chỉ coi các trạng thái accepted/queued/sending/sent/delivered là nhà cung cấp
đã chấp nhận. Điều này không chứng minh tin đã đến điện thoại/hộp thư.
Timeout gửi là 10 giây. Không cấu hình hoặc thiếu key trả 503 `RECOVERY_NOT_CONFIGURED`;
lỗi nhà cung cấp trả 503 `RECOVERY_DELIVERY_FAILED`, không trả nội dung lỗi/secret nhà cung cấp.
Transaction rollback challenge nếu dịch vụ từ chối gửi. Nếu nhà cung cấp đã nhận nhưng DB commit
thất bại, mã nhận được sẽ không dùng được; người dùng cần yêu cầu lại.
Lỗi gửi thật trả 503 cho tài khoản đủ điều kiện, nên thông báo chung không bảo đảm chống suy đoán
tài khoản theo thời gian phản hồi/lúc nhà cung cấp gặp lỗi. Không tuyên bố chống enumeration tuyệt đối.

Tài liệu giao thức: [SMTP STARTTLS](https://learn.microsoft.com/dotnet/api/system.net.mail.smtpclient.enablessl),
[Twilio Messages](https://www.twilio.com/docs/messaging/api/message-resource).

## Quản lý khách hàng — chỉ ADMIN

| Method | API | Chức năng |
|---|---|---|
| GET | `/api/v1/admin/customers` | search tên/email/điện thoại, isActive, sort, descending, page/pageSize |
| POST | `/api/v1/admin/customers` | Tạo CUSTOMER, body giống đăng ký; không phát token đăng nhập |
| GET | `/api/v1/admin/customers/{id}` | Hồ sơ và các địa chỉ đã lưu |
| PATCH | `/api/v1/admin/customers/{id}` | fullName, phone, expectedUpdatedAt, reason |
| PATCH | `/api/v1/admin/customers/{id}/lock` | isLocked, reason bắt buộc |
| POST | `/api/v1/admin/customers/{id}/reset-password` | newPassword; thu hồi phiên/challenge |
| POST | `/api/v1/admin/customers/{id}/revoke-sessions` | Thu hồi mọi phiên và challenge |

Không chấp nhận nâng quyền qua payload. API khách hàng chỉ thao tác CUSTOMER; ID nhân viên/quản trị
trả 404. Email là định danh đăng nhập, không thay email qua chỉnh hồ sơ; việc thay email cần luồng
xác minh riêng. Không hard delete; khóa giữ nguyên địa chỉ và tham chiếu đơn hàng.
Địa chỉ do khách hàng tự quản lý qua `/me/addresses`, quản trị được xem trong chi tiết.
Không mở rộng module này thành quản lý đơn hàng hoặc quyền STAFF/ADMIN.

`expectedUpdatedAt` lấy nguyên từ hồ sơ mới nhất, thiếu trả 400, phiên bản cũ trả 409
`CUSTOMER_CHANGED`. Giá trị trả về theo độ chính xác microsecond của PostgreSQL.
Sort chỉ nhận createdAt/fullName/email, phân trang tối đa 100, chặn overflow offset.
Các thao tác ghi kiểm tra lại actor sau khi lấy advisory lock, giữ cùng quy tắc với quản lý nhân viên.

## Audit database — chỉ ADMIN đọc

`GET /api/v1/admin/audit` hỗ trợ actorId, targetId, action, from/to (ISO 8601), page/pageSize.
Sắp xếp mới nhất trước, có khóa phụ ID để phân trang ổn định. Không có API sửa/xóa.

Bản ghi gồm ID, actor, target, action, reason và thời gian. Ghi trong cùng transaction với thay đổi
và thu hồi phiên; lỗi ghi audit phải rollback thao tác. Các luồng nhân viên trước đây đã được nối
vào bảng audit. Có thêm sự kiện đổi/khôi phục mật khẩu và xác minh số.
Reason giới hạn 500 ký tự; người vận hành không được nhập mật khẩu/OTP/thông tin nhạy cảm vào reason.
Audit không sao chép password, token, địa chỉ hoặc toàn bộ request/response.

Trigger PostgreSQL từ chối UPDATE/DELETE/TRUNCATE. Đây không phải sổ chống can thiệp trước DBA:
chủ sở hữu database vẫn có thể thay schema/tắt trigger. Cần backup và phân quyền DB phù hợp.
Không đặt FK audit sang user để lịch sử không bị cascade xóa. Không backfill lịch sử trước migration.

## Migration và kiểm tra

Migration `20261119005959_AccountRecoveryAndAudit` thêm cột VerifiedPhone nullable (tài khoản cũ
chưa xác minh), hai bảng, FK challenge và indexes. Không cập nhật/xóa dữ liệu user hiện có.
Không tự chạy migration trên production. Review SQL và dùng quy trình triển khai hiện có:

```powershell
dotnet ef migrations script --idempotent --project src/Modules/Identity/PhoneStore.Modules.Identity --context IdentityDbContext --configuration Release
dotnet build PhoneStore.slnx --configuration Release --no-restore
dotnet test PhoneStore.slnx --configuration Release --no-build --no-restore -m:1
node --test tests/frontend/identity-api.test.cjs tests/frontend/recovery-ui.test.cjs
```

Down migration xóa hai bảng và cột mới, làm mất lịch sử/challenge; không chạy trên production.
Challenge đã hết hạn vẫn không thể dùng, nhưng chưa có tác vụ tự dọn dữ liệu. Khi triển khai cần
quy định retention/cleanup challenge và backup audit; không tùy tiện xóa audit.

UI: `#/quen-mat-khau`, `#/xac-minh-so`, `#/quan-ly-khach-hang`, `#/nhat-ky-quan-tri`.
Kiểm tra bằng CUSTOMER/STAFF/ADMIN; gọi trực tiếp API phải bị từ chối nếu thiếu quyền.
Thử OTP sai 5 lần, hết hạn, dùng lại, reset đồng thời, mật khẩu trống, đổi số sau khi nhận OTP,
khóa tài khoản, cập nhật hồ sơ với phiên bản cũ và lỗi audit làm rollback.

## Bằng chứng hiện tại

- Build Release mới nhất thành công: 0 lỗi, 5 cảnh báo có sẵn ở test ngoài phần sửa; model không có thay đổi thiếu migration.
- Runner xUnit chạy trong process: 204/204 unit và 7/7 architecture đạt, không bỏ qua.
- Node: 15/15 đạt, gồm 6 bài kiểm tra luồng recovery UI với DOM giả lập.
- Bộ integration bổ sung 15 test; chưa xác nhận chạy đạt PostgreSQL trong môi trường hạn chế.
- PostgreSQL test khởi động được nhưng kết nối bị từ chối bởi socket 10013 trong sandbox.
- VSTest bị hủy do không kết nối testhost (timeout 15 giây); không tính là test đạt.
- Chưa chạy browser E2E, chưa gửi thật bằng SMTP/Twilio của môi trường triển khai.
- Chưa commit/push đợt này tại thời điểm chuẩn bị tài liệu; không dùng kết quả 390 test của đợt trước thay thế.

Script local `PUBLISH-RECOVERY-AUDIT.ps1` kiểm tra lại nguồn, restore/build/model, 15 test JavaScript
và toàn bộ suite .NET ngoài sandbox. Chỉ khi ba TRX mới đều Completed, 425 test đạt (gồm 15 test
integration mới và 20 trường hợp unit mới), script mới clone riêng, stage các file đã review,
tạo thêm báo cáo từ kết quả thực tế và commit/push fast-forward lên tricong.
Nếu đã xuất bản bằng script này, xem `RECOVERY-PUBLISH-VALIDATION.md` cho kết quả local sau cùng;
trạng thái CI phải kiểm tra riêng trên GitHub cho đúng SHA.
