# Bằng chứng kiểm thử FR-01 / FR-02

Ngày chạy: 28/10/2026. Nền: `origin/toandev` tại `fe41419867be282518242302292f60c501a0d3b6`.
Nhánh local: `codex/n2lhtbltt-8-9-toandev-auth`. Chưa commit/push/merge.

## Môi trường thực tế

- Windows, .NET SDK 10.0.300, target `net10.0`.
- PostgreSQL 17.9, cluster local tách riêng, chỉ lắng nghe `127.0.0.1:55441` trong lúc test.
- Fixture `PhoneStoreApiFactory` tự tạo DB `phonestore_test_<guid>`, chạy migration của năm module và xóa DB khi hoàn tất. Cluster thử nghiệm đã được dừng sau test.
- Build/test Release, dùng code và package của repository, không thêm dependency mới.
- HTTP integration dùng ASP.NET Core `WebApplicationFactory`/TestServer và PostgreSQL thật. Không coi đây là kiểm thử Kestrel/HTTPS/reverse proxy hoặc trình duyệt thật.

## Trước khi sửa

Nhánh nền restore/build thành công. Bổ sung bảy regression test, chạy trên bản gốc:

| Trường hợp | Kết quả trên bản gốc |
|---|---|
| Logout rồi dùng lại access token | Thất bại: JWT vẫn được chấp nhận |
| Logout một thiết bị, kiểm tra hai thiết bị | Thất bại: token của thiết bị đã logout vẫn dùng được |
| Cấp token lỗi sau khi tạo user | Thất bại: user vẫn tồn tại |
| Cấp token mới lỗi trong refresh | Thất bại: token cũ đã bị thu hồi |
| Hai refresh cùng token | Thất bại: không bảo đảm chỉ một request thành công |
| Hai đăng ký cùng email | Thất bại: phản hồi không đạt một `201`, một `409` |
| Origin không tin cậy và cache auth | Thất bại: không từ chối như tiêu chí kiểm thử |

TRX baseline: `tests/PhoneStore.IntegrationTests/TestResults/identity-baseline.trx`, tổng 7, đạt 0, lỗi 7. Sau sửa, chạy lại đúng bảy ca: đạt 7, lỗi 0 (`identity-fixed.trx`).

## Sau khi sửa và bổ sung các ca liên quan

Lệnh đã chạy, với `PHONESTORE_TEST_ADMIN_CONNECTION` trỏ tới PostgreSQL test:

```powershell
dotnet test PhoneStore.slnx --configuration Release --no-restore --verbosity quiet --logger 'trx;LogFileName=full-auth.trx' -m:1 /p:UseSharedCompilation=false
```

| Project | Tổng | Đạt | Lỗi | Bỏ qua |
|---|---:|---:|---:|---:|
| PhoneStore.UnitTests | 80 | 80 | 0 | 0 |
| PhoneStore.ArchitectureTests | 7 | 7 | 0 | 0 |
| PhoneStore.IntegrationTests | 188 | 188 | 0 | 0 |
| **Tổng** | **275** | **275** | **0** | **0** |

Lệnh kết thúc với exit code `0`. Integration suite báo thời gian thực thi khoảng 2 phút 1 giây; không dùng số này làm benchmark hiệu năng API.

TRX đầy đủ ở `tests/<tên project>/TestResults/full-auth.trx`. `TestResults/` được Git ignore; đây là bằng chứng local, chưa có kết quả CI trên GitHub cho thay đổi này.

Các ca mới:

- `IdentitySessionRegressionTests`: 19 ca, bao gồm bảy ca ban đầu, logout lặp lại, logout thiếu/JWT sai với cookie + CSRF, logout bằng Bearer và CSRF khi không có refresh cookie, token cũ/mới sau refresh, từ chối thiếu CSRF, Origin hợp lệ/không hợp lệ, phiên DB hết hạn, thu hồi mọi thiết bị khi đổi/reset mật khẩu, đổi role, khóa tài khoản.
- `JwtOptionsTests`: 7 ca, kiểm tra thiếu/khóa ngắn, khóa cấu hình, khóa tạm nhất quán trong process và thời hạn không dương.
- Ca refresh đồng thời dùng trigger trễ tạm cho đúng token test, chỉ trong DB random; trigger/function được xóa trong `finally`. Ca rollback dùng lỗi cấp token được chủ động gây ra trong test, không làm hỏng DB người dùng.
- Các test cũ của Cart, Catalog, Order, Inventory và Identity cũng nằm trong lần chạy toàn solution nêu trên.

## Build warnings và giới hạn

Cập nhật trong lượt hướng dẫn test: chạy lại riêng ba lớp `IdentityApiTests`, `AuthAndStaffApiTests`, `IdentitySessionRegressionTests`, tổng **46/46 đạt**, 0 lỗi, 0 bỏ qua, exit code 0. TRX: `tests/PhoneStore.IntegrationTests/TestResults/auth-guided.trx`; thời gian báo bởi runner khoảng 37 giây. PostgreSQL test đã được dừng sau khi hoàn tất.

Đã tạo [Postman Collection](../../postman/PhoneStore-FR01-FR02.postman_collection.json) gồm 18 request, kiểm tra JSON và cú pháp 20 script. Chưa thực thi bằng Postman/Newman. Hướng dẫn: [TEST-POSTMAN.md](TEST-POSTMAN.md).

Còn năm warning `CS0414` về `_userIdCounter` không dùng trong các test cũ: `OrderLifecycleTests`, `StockRestoreOnCancelTests`, `CheckoutFlowTests`, `EndToEndOrderJourneyTests`, `BusinessFlowRegressionTests`. Không có warning mới trong lần chạy cuối.

Chưa chạy CI GitHub, Postman UI, trình duyệt frontend, tải lớn, PostgreSQL 16 của Docker/CI hoặc triển khai HTTPS/proxy. Không suy ra các môi trường đó đã đạt chỉ từ lần chạy local này. Các lệnh migration thủ công và kịch bản Postman trong hướng dẫn là bước tái hiện dành cho nhóm, không được ghi nhận là đã chạy tại đây.

Chi tiết triển khai, trade-off và hướng dẫn nghiệm thu: [FR-01-FR-02.md](FR-01-FR-02.md).
