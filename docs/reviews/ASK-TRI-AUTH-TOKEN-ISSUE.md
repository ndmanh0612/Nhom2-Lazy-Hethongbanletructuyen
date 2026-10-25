# Hỏi Trí — Vấn đề xác thực trong API Identity (FR-01/02/03)

> Phát hiện khi viết integration test E2E cho Identity.

---

## Tin nhắn ngắn

```
Trí ơi, tôi vừa viết integration test E2E cho API Identity (đăng ký / đăng nhập / hồ sơ /
địa chỉ) — 13 test đều xanh. Nhưng có 2 điểm về auth mình muốn hỏi anh:

1) TOKEN ĐĂNG KÝ KHÔNG DÙNG ĐƯỢC Ở ĐÂU KHÁC. /api/v1/auth/register và /login trả về
   token (dạng GUID), nhưng các endpoint /api/v1/me, /api/v1/me/addresses, /api/v1/cart,
   /api/v1/orders đều đọc userId từ header "X-User-Id" chứ không từ token. Nghĩa là token
   hiện chỉ là "trang trí" — client gửi header X-User-Id: 5 là xem được hồ sơ user 5.

2) FALLBACK userId ?? 1 TRONG MeController. Gọi /api/v1/me KHÔNG gửi header nào vẫn trả 200
   với hồ sơ của user id=1. Có nghĩa là endpoint "xem hồ sơ cá nhân" hiện không yêu cầu
   đăng nhập, và ai cũng xem được tài khoản số 1.

Tôi đã viết test đúng theo hành vi hiện tại (dùng header X-User-Id), chưa sửa gì vì phần
này là task N2LHTBLTT-8 / N2LHTBLTT-9 của anh. Câu hỏi:

- Khi anh làm session/JWT thật, các endpoint sẽ đọc userId từ claim "sub" và bỏ hẳn
  fallback header, đúng không? Nếu vậy giờ tôi có nên bỏ fallback `?? 1` và bắt buộc
  header X-User-Id (trả 401 khi thiếu) để giống hành vi cuối cùng không?

- Token nên là JWT hay session ID lưu trong DB/Redis? Ảnh hưởng tới việc tôi có cần
  thêm endpoint /api/v1/auth/logout vào tài liệu test hay không.

- /api/v1/auth/logout hiện có trong Swagger (21 path) nhưng tôi chưa test được — anh
  xem giúp nó cần session thật không.

Cảm ơn anh 🙏
```

---

## Chi tiết kỹ thuật

### 1. Token trả về không được dùng để xác thực

```csharp
// src/Modules/Identity/.../Application/Services/IdentityService.cs
var token = Guid.NewGuid().ToString("N");   // ← token giả, không lưu vào đâu
return Result.Success(new AuthResponseDto(user.Id, user.Email, user.FullName, user.Role.ToString(), token));
```

Các endpoint khác đọc userId từ header:

```csharp
// src/Modules/Identity/.../Endpoints/MeController.cs
public async Task<IActionResult> GetProfile([FromHeader(Name = "X-User-Id")] long? userId, ...)
```

Và Cart/Order dùng `ICurrentUser` (`HttpContextCurrentUser`) — vốn đã hỗ trợ đọc claim
`sub` trước, fallback sang header `X-User-Id`:

```csharp
// src/BuildingBlocks/.../Infrastructure/Identity/HttpContextCurrentUser.cs
var principal = _httpContextAccessor.HttpContext?.User;
if (principal?.Identity?.IsAuthenticated == true)
{
    var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? principal.FindFirstValue("sub");
    if (long.TryParse(subject, out var userId)) return userId;
}

// Fallback tạm thời cho môi trường chưa có JWT thật.
var header = _httpContextAccessor.HttpContext?.Request.Headers["X-User-Id"].ToString();
```

→ Phần "đọc claim `sub`" đã sẵn sàng, chỉ thiếu middleware xác thực tạo `ClaimsPrincipal`
từ token. Khi anh bổ sung, chỉ cần đăng ký `UseAuthentication()` + middleware đọc token
là các module khác không phải đổi.

### 2. Fallback `userId ?? 1` khiến endpoint hồ sơ không cần đăng nhập

```csharp
var uid = userId ?? 1; // Fallback mock user for testing if no auth context
```

Gọi `GET /api/v1/me` không có header → trả `200 OK` với hồ sơ user id=1.

**Kiểm chứng:**

```bash
curl -s http://localhost:5000/api/v1/me
# {"success":true,"message":"Thành công","data":{"id":1,"email":"customer@phonestore.vn",...}}
```

### 3. `/api/v1/auth/logout` chưa rõ hành vi

Swagger liệt kê `/api/v1/auth/logout` nhưng chưa có test nào chạm tới. Cần xác nhận:
- Logout cần token hợp lệ không?
- Sau logout thì token bị vô hiệu hoá như thế nào (xoá session, đenlist token)?

---

## Test đã thêm

`tests/PhoneStore.IntegrationTests/IdentityApiTests.cs` — 13 test:

| Test | Phủ |
|---|---|
| `Register_creates_new_account_and_returns_token` | đăng ký thành công, trả token + role |
| `Register_with_duplicate_email_returns_conflict` | trùng email → 409 |
| `Register_with_invalid_email_returns_validation_error` | email sai định dạng → 400 |
| `Register_with_weak_password_returns_validation_error` | mật khẩu yếu → 400 |
| `Login_with_valid_credentials_returns_token` | login đúng |
| `Login_with_wrong_password_returns_invalid_credentials` | sai mật khẩu → 401 `INVALID_CREDENTIALS` |
| `Login_with_unknown_email_returns_invalid_credentials` | email không tồn tại → 401 |
| `Get_profile_returns_user_information` | xem hồ sơ |
| `Update_profile_changes_full_name_and_phone` | cập nhật hồ sơ |
| `Change_password_then_login_with_new_password_succeeds` | đổi mật khẩu |
| `Addresses_can_be_added_and_listed` | thêm + xem địa chỉ |
| `Addresses_are_scoped_per_user` | địa chỉ tách theo user |

> Lưu ý: các test dùng header `X-User-Id` — **đúng theo hành vi hiện tại**, không phải
> theo hành vi mong muốn. Khi anh chuyển sang JWT thật, chỉ cần đổi cách lấy userId
> trong test (thêm `Authorization: Bearer {token}`), phần assert giữ nguyên.
