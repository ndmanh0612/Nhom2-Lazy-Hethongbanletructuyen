# Kiểm thử FR-01 / FR-02 từng bước bằng Postman

Áp dụng cho code local trên nhánh `codex/n2lhtbltt-8-9-toandev-auth`, nền `toandev`. Các thay đổi chưa được push lên GitHub; clone lại nhánh remote `toandev` sẽ chưa có các bản sửa local này.

## 1. Xác định đúng phạm vi

FR-01/FR-02 là hai yêu cầu nghiệp vụ, gồm các endpoint đăng ký, đăng nhập, đăng xuất và kiểm tra phiên. Cần test cả vòng đời để phát hiện trường hợp login trả token nhưng token không dùng được hoặc logout không thu hồi được phiên.

| Endpoint | Vai trò trong bài test |
|---|---|
| `GET /health` | Backend có đang hoạt động không |
| `POST /api/v1/auth/register` | FR-01: tạo tài khoản CUSTOMER |
| `POST /api/v1/auth/login` | FR-02: đăng nhập |
| `GET /api/v1/me` | JWT có bảo vệ đúng tài khoản không |
| `POST /api/v1/auth/refresh` | Làm mới phiên, thay cặp token |
| `POST /api/v1/auth/logout` | FR-02: thu hồi phiên |
| `GET /api/v1/auth/csrf` | Lấy lại cookie CSRF nếu cần |

## 2. Khởi động backend

Mở PowerShell trong thư mục:

```powershell
Set-Location 'C:\Users\dellcuatri142\Downloads\sever_da\team-repo'
git branch --show-current
dotnet --version
```

Kỳ vọng nhánh `codex/n2lhtbltt-8-9-toandev-auth` và SDK major 10. Không chuyển/reset nhánh khi đang có các thay đổi chưa commit.

**Database:** chọn PostgreSQL local đang có hoặc Docker. Khi dùng Docker, mở Docker Desktop và chờ engine hoạt động, rồi chạy:

```powershell
docker compose -f docker/docker-compose.yml up -d postgres
docker compose -f docker/docker-compose.yml ps postgres
```

DB mặc định trong repo là `phonestore_db` trên `localhost:5433`; container publish `5433:5432`. FR này chỉ cần PostgreSQL, không cần khởi động MinIO. Nếu dùng DB riêng, đặt `ConnectionStrings__DefaultConnection` theo cấu hình đó trong cùng terminal; không commit thông tin kết nối riêng.

Build và áp dụng migration có sẵn. Các lệnh cập nhật schema của DB local được cấu hình, không tạo migration mới:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet restore PhoneStore.slnx
if ($LASTEXITCODE -ne 0) { throw 'Restore thất bại; kiểm tra lỗi trước khi tiếp tục.' }
dotnet build PhoneStore.slnx --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build thất bại; kiểm tra lỗi trước khi tiếp tục.' }

dotnet ef --version
# Nếu chưa cài dotnet-ef: dotnet tool install --global dotnet-ef --version 10.0.13
$taskHost = 'src/Host/PhoneStore.Api/PhoneStore.Api.csproj'
foreach ($taskModule in @('Identity', 'Catalog', 'Cart', 'Order', 'Inventory')) {
    dotnet ef database update --context "${taskModule}DbContext" --project "src/Modules/$taskModule/PhoneStore.Modules.$taskModule" --startup-project $taskHost --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) { throw "Migration $taskModule thất bại; kiểm tra lỗi trước khi tiếp tục." }
}

dotnet run --project $taskHost --configuration Release --no-build --launch-profile http
```

Giữ terminal cuối đang chạy. Profile `http` cấu hình `http://localhost:5122`; hãy kiểm tra dòng `Now listening on` thực tế. Trong Postman gửi:

```http
GET http://localhost:5122/health
```

Kỳ vọng `200` và `status=Healthy`. Swagger ở `http://localhost:5122/`. Nếu chưa gọi health được thì giải quyết lỗi khởi động/kết nối trước khi test Auth.

Backend không được giữ chạy bởi bộ integration test: fixture tạo DB riêng rồi xóa khi kết thúc. Vì vậy test tự động đạt không có nghĩa cổng `5122` đang sẵn sàng cho Postman.

## 3. Import bộ request đã chuẩn bị

File: [PhoneStore-FR01-FR02.postman_collection.json](../../postman/PhoneStore-FR01-FR02.postman_collection.json).

1. Mở Postman trên máy local và import file trên. Tùy phiên bản, nút có tên `Import` hoặc nằm trong `Use resources or import`.
2. Mở collection `PhoneStore — FR-01 FR-02 — Local Auth Tests`.
3. Trong Variables, giữ `base_url = http://localhost:5122`, không thêm dấu `/` cuối. Chọn `No environment` hoặc bảo đảm environment không định nghĩa biến trùng tên collection.
4. Giữ cookie jar bật. Không thêm thủ công header `Cookie`; không bật `Disable cookie jar` trong request Settings.
5. Gửi lần lượt `00` đến `17`, hoặc mở collection runner, chọn toàn bộ request đúng thứ tự, một iteration, rồi chạy.
6. Xem status, JSON, response headers/cookies và mục Tests/Test Results sau mỗi request. HTTP `400/401/403/409` ở ca âm tính là **kết quả đạt nếu đúng kỳ vọng**.

Request `01` tự sinh email và mật khẩu test mới; response scripts tự lưu `access_token`, `csrf_token`, `user_id` vào collection variables. Request `09` giữ JWT cũ ở `old_access_token` trước khi lưu JWT mới. Không cần sao chép token nếu chạy collection theo thứ tự.

Không gửi lại `01` giữa luồng: nó tạo tài khoản mới và thay variables/cookie, nên các bước sau sẽ test tài khoản/phiên khác. Muốn chạy lại trọn bộ, bắt đầu lại từ `00/01`.

Variables có token sau khi chạy: không export/chia sẻ collection đã điền token hoặc ảnh chứa JWT/cookie; file gốc trong repo để trống các giá trị này. Dùng tài khoản test do collection tạo.

## 4. Hiểu và gửi thủ công từng bước chính

### A. FR-01 — Đăng ký

Tạo request `POST {{base_url}}/api/v1/auth/register`. Chọn **Body → raw → JSON**, Authorization **No Auth**:

```json
{
  "email": "tri.auth.demo.001@example.com",
  "password": "Pass@123",
  "fullName": "Đỗ Công Trí",
  "phone": "0912345678"
}
```

Đây là dữ liệu mẫu cho test thủ công. Đổi email nếu đã tạo trước đó. Nếu dùng collection thì giữ Body có `{{email}}`, `{{password}}` để scripts điều khiển dữ liệu; không thay bằng ví dụ trên giữa lần chạy.

Kỳ vọng:

- HTTP `201 Created`, `success=true`, `data.userId` được cấp, `data.role=CUSTOMER`.
- Có `data.accessToken`, `data.expiresAt`; không có `password`, `passwordHash` hoặc `refreshToken` trong JSON.
- Response `Set-Cookie` có `phonestore_rt` với `HttpOnly` và cookie `phonestore_csrf`.
- `Cache-Control: no-store`. Trên local HTTP, cookie refresh không có `Secure`; trên HTTPS code sẽ bật thuộc tính này.

Nếu gửi cùng request lần nữa, kỳ vọng `409`, `code=EMAIL_ALREADY_EXISTS`. Đổi password thành `abc` thì kỳ vọng `400` kèm `fieldErrors`; validation chạy trước kiểm tra email trùng.

### B. FR-02 — Đăng nhập

`POST {{base_url}}/api/v1/auth/login`, **Body → raw → JSON**, **No Auth**:

```json
{
  "email": "tri.auth.demo.001@example.com",
  "password": "Pass@123"
}
```

Kỳ vọng `200`, JWT mới và refresh cookie. Gửi password sai nhưng vẫn là chuỗi không rỗng, kỳ vọng `401 INVALID_CREDENTIALS`. Password rỗng là lỗi validation `400`, không phải ca sai thông tin đăng nhập.

Để tự lưu JWT trong request thủ công, đặt đoạn sau ở **Scripts → Post-response** (một số phiên bản cũ dùng tab Tests):

```javascript
pm.test('Login thành công', () => pm.response.to.have.status(200));
if (pm.response.code === 200) {
  pm.collectionVariables.set('access_token', pm.response.json().data.accessToken);
}
```

### C. Kiểm tra JWT đang xác thực đúng người

`GET {{base_url}}/api/v1/me`. Chọn **Authorization → Bearer Token**, ô Token là `{{access_token}}`. Không thêm chữ `Bearer` vào ô token vì Postman tự thêm scheme.

Kỳ vọng `200`, `data.email` đúng email vừa đăng nhập. Đổi sang **No Auth**, dù cookie vẫn còn thì phải nhận `401 UNAUTHORIZED`. Cookie refresh không tự xác thực endpoint `/me`.

### D. Kiểm tra refresh và CSRF

Mở Cookies của domain `localhost`, lấy **giá trị** `phonestore_csrf`. Nếu chưa có, gửi `GET {{base_url}}/api/v1/auth/csrf` trước. Tạo `POST {{base_url}}/api/v1/auth/refresh`:

- Authorization: **No Auth**.
- Body: raw JSON `{}`.
- Header: `X-CSRF-TOKEN` bằng giá trị cookie `phonestore_csrf`.
- Giữ cookie jar bật để Postman tự gửi `phonestore_rt` và `phonestore_csrf`.

Nếu chạy collection, header đã là `X-CSRF-TOKEN: {{csrf_token}}`. Không gửi refresh token trong JSON.

Kỳ vọng `200`; lưu `data.accessToken` mới. Gọi `/me` bằng JWT trước refresh → `401`; JWT mới → `200`. Xóa header CSRF và gửi refresh → `403 CSRF_TOKEN_INVALID`.

### E. FR-02 — Đăng xuất và chứng minh phiên bị thu hồi

`POST {{base_url}}/api/v1/auth/logout`, Body `{}`, **No Auth**, cùng header CSRF như refresh.

Kỳ vọng `200`; response xóa cookie refresh. **Giữ một bản JWT vừa dùng để test**, gọi lại `/me` bằng JWT đó: phải nhận `401`. Đây là bằng chứng backend thu hồi phiên; chỉ thấy thông báo logout thành công là chưa đủ.

Sau logout, refresh có CSRF nhưng không còn refresh cookie → `401 REFRESH_TOKEN_MISSING`. Gửi logout lại với CSRF hợp lệ → `200`. Cookie CSRF được giữ lại, nó không phải thông tin đăng nhập.

## 5. Kỳ vọng đầy đủ của collection

| STT | Ca kiểm thử | HTTP mong đợi |
|---|---|---:|
| 00 | Health | 200 |
| 01 | Đăng ký email mới | 201 |
| 02 | Email trùng | 409 |
| 03 | Password `abc` | 400 |
| 04 | Đăng nhập sai password | 401 |
| 05 | Đăng nhập đúng | 200 |
| 06 | `/me` có JWT | 200 |
| 07 | `/me` không JWT, cookie vẫn còn | 401 |
| 08 | Refresh thiếu header CSRF | 403 |
| 09 | Refresh có cookie và CSRF | 200 |
| 10 | `/me` bằng JWT cũ trước refresh | 401 |
| 11 | `/me` bằng JWT mới | 200 |
| 12 | Logout có CSRF | 200 |
| 13 | `/me` bằng JWT của phiên đã logout | 401 |
| 14 | Refresh sau logout | 401 |
| 15 | Logout lần nữa | 200 |
| 16 | Đăng ký có Origin không được phép | 403 |
| 17 | Email sai định dạng | 400 |

Để nghiệm thu, status và assertions trong Test Results phải cùng đúng; lưu thêm response JSON/headers đã che token nếu cần đưa vào báo cáo.

## 6. Kiểm tra database nếu cần bằng chứng bổ sung

Mở pgAdmin/query tool trên đúng DB mà backend đang dùng; thay email dưới đây bằng collection variable `email`:

```sql
SELECT "Id", "Email", "FullName", "Role", "IsActive",
       length("PasswordHash") AS "PasswordHashLength"
FROM identity.users
WHERE "Email" = 'tri.auth.demo.001@example.com';

SELECT t."Id", t."UserId", t."IsRevoked", t."RevokedAt", t."ExpiresAt"
FROM identity.refresh_tokens t
JOIN identity.users u ON u."Id" = t."UserId"
WHERE u."Email" = 'tri.auth.demo.001@example.com'
ORDER BY t."Id";
```

Kỳ vọng một user cho một email, role CUSTOMER, có hash mật khẩu. Độ dài hash là bằng chứng phụ, không tự chứng minh thuật toán băm đúng. Không đưa toàn bộ hash/token vào báo cáo.

Một user có thể có nhiều phiên: register tự đăng nhập, login tạo thêm phiên và refresh tạo dòng thay thế. Logout chỉ thu hồi phiên hiện tại, vì vậy không kỳ vọng tất cả dòng của user đều `IsRevoked=true`.

## 7. Lỗi hay gặp và thứ tự kiểm tra

| Hiện tượng | Kiểm tra dựa trên bằng chứng |
|---|---|
| `ECONNREFUSED` hoặc không gửi được request | Terminal Host còn chạy không? `Now listening on` là port nào? Gọi `/health` trước. |
| Npgsql không kết nối DB | Log báo host/port nào? Docker postgres có healthy không? Dùng port host 5433 với cấu hình Docker hiện có. |
| `42P01` / relation không tồn tại | Đã áp dụng migration cả năm module vào đúng DB chưa? Host seed không tự chạy migration. |
| `409` ngay khi register | Email đã tồn tại; đổi email thủ công hoặc chạy lại request 01 để sinh email mới. |
| `400` / `415` ngoài kỳ vọng | Body có đúng JSON raw, `Content-Type: application/json`, đúng tên `email/password/fullName` không? Đọc `fieldErrors`. |
| Refresh/logout trả `403` | Cookie và header CSRF có đủ và khớp không? Có tắt cookie jar, đặt Origin lạ hay trộn `localhost` với `127.0.0.1` không? |
| `/me` trả `401` sau refresh | Đã thay JWT cũ bằng `data.accessToken` mới chưa? Collection có bị environment ghi đè `access_token` không? |
| `/me` trả `401` sau restart | Khóa tạm Development đổi khi process restart; login lại hoặc refresh bằng cookie còn hạn. |
| Bất kỳ `500` nào | Lưu method, URL, status, response `requestId`, thời điểm và đoạn log server tương ứng; chưa đủ log thì chưa kết luận nguyên nhân. |

## 8. Kiểm thử tự động đã kiểm chứng

Đã chạy lại riêng ba lớp `IdentityApiTests`, `AuthAndStaffApiTests`, `IdentitySessionRegressionTests`: **46/46 đạt**, PostgreSQL 17.9 thật, ASP.NET TestServer. Có cả ca cạnh tranh/rollback mà Postman tuần tự không chứng minh được.

Lệnh tương đương khi đã có PostgreSQL cho test:

```powershell
# Connection tới database postgres, user có quyền tạo/xóa DB test.
$env:PHONESTORE_TEST_ADMIN_CONNECTION = '<connection string local dành cho test>'
dotnet test tests/PhoneStore.IntegrationTests/PhoneStore.IntegrationTests.csproj --configuration Release --filter 'FullyQualifiedName~IdentityApiTests|FullyQualifiedName~AuthAndStaffApiTests|FullyQualifiedName~IdentitySessionRegressionTests'
```

Collection JSON và 20 script đã được kiểm tra cú pháp JavaScript. Chưa chạy collection bằng giao diện/runner Postman hoặc Newman; không ghi nhận collection đã pass chỉ từ kết quả .NET. Các bước khởi động Docker/EF thủ công là hướng dẫn tái hiện, không phải log đã thực hiện trên DB người dùng.

Tài liệu Postman chính thức: [import](https://learning.postman.com/docs/getting-started/importing-and-exporting/importing-data/), [cookie jar](https://learning.postman.com/docs/use/send-requests/response-data/cookies/), [scripts và pm APIs](https://learning.postman.com/docs/tests-and-scripts/write-scripts/postman-sandbox-reference/overview/). Postman không mô phỏng đầy đủ thuộc tính SameSite như trình duyệt; cần test frontend thực tế riêng để kết luận hành vi cookie trên browser.
