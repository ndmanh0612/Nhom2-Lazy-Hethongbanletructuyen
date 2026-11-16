# 🚀 Hướng dẫn Deploy PhoneStore (Docker + nginx)

Tài liệu này mô tả cách đóng gói và triển khai hệ thống PhoneStore lên môi trường
production bằng Docker Compose: PostgreSQL + API (.NET 10) + nginx reverse proxy.

---

## 1. Kiến trúc triển khai

```text
                    ┌─────────────────────────────┐
   Browser ───────▶ │  nginx (container "web")    │  :8088 (host) → :80 (container)
                    │  • TLS termination (nếu bật)│
                    │  • gzip + cache static      │
                    │  • proxy /api → api:8080    │
                    │  • SPA fallback → index.html│
                    └──────────────┬──────────────┘
                                   │  internal network "phonestore-net"
                    ┌──────────────▼──────────────┐
                    │  PhoneStore.Api (container)  │  ASPNETCORE_HTTP_PORTS=8080
                    │  • 5 module nghiệp vệ        │
                    │  • phục vụ SPA tại wwwroot   │
                    │  • Swagger UI                │
                    └──────────────┬──────────────┘
                                   │
                    ┌──────────────▼──────────────┐
                    │  PostgreSQL 16 (container)  │  schema: identity, catalog,
                    │  volume: pgdata              │          cart, ordering, inventory
                    └─────────────────────────────┘
```

| Container | Vai trò | Cổng nội bộ | Cổng host |
|---|---|---|---|
| `phonestore-web` | nginx reverse proxy + SPA | 80 | `${WEB_PORT}` (mặc định `80`) |
| `phonestore-api` | ASP.NET Core Web API | 8080 | *không expose ra host* |
| `phonestore-postgres` | PostgreSQL 16 | 5432 | *không expose ra host* |

---

## 2. Chuẩn bị cấu hình

Sao chép file mẫu rồi điền giá trị thật:

```bash
cp docker/env.prod.sample docker/env.prod
```

Nội dung tối thiểu cần có:

```dotenv
POSTGRES_USER=postgres
POSTGRES_PASSWORD=<mật khẩu mạnh>
POSTGRES_DB=phonestore_db
PHONESTORE_SIGNING_KEY=<chuỗi ngẫu nhiên >= 32 ký tự>
WEB_PORT=8088
PUBLIC_ORIGIN=http://localhost:8088
```

Sinh khoá ký JWT an toàn:

```bash
# PowerShell
$bytes = New-Object byte[] 48
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create(); $rng.GetBytes($bytes)
[Convert]::ToBase64String($bytes)
```

> `docker/env.prod` đã nằm trong `.gitignore` — **không được push lên GitHub**.

---

## 3. Build & chạy

```bash
# Build image (multi-stage: restore → publish → runtime)
docker compose -f docker/docker-compose.prod.yml --env-file docker/env.prod build

# Chạy stack ở chế độ nền
docker compose -f docker/docker-compose.prod.yml --env-file docker/env.prod up -d

# Theo dõi log
docker compose -f docker/docker-compose.prod.yml --env-file docker/env.prod logs -f api
```

| URL | Nội dung |
|---|---|
| `http://localhost:8088/` | Storefront (SPA) |
| `http://localhost:8088/health` | Health check |
| `http://localhost:8088/swagger` | Swagger UI |

Dừng / xoá stack:

```bash
docker compose -f docker/docker-compose.prod.yml --env-file docker/env.prod down
# kèm xoá dữ liệu PostgreSQL:
docker compose -f docker/docker-compose.prod.yml --env-file docker/env.prod down -v
```

---

## 4. Biến môi trường quan trọng

| Biến | Áp dụng cho | Ý nghĩa |
|---|---|---|
| `Authentication__SigningKey` | API | Khoá ký HS256, **bắt buộc ≥ 32 ký tự** ở Production |
| `Authentication__AllowDevelopmentSigningKey` | API | Luôn để `false` ở Production |
| `ConnectionStrings__DefaultConnection` | API | Điểm đến PostgreSQL nội bộ `postgres:5432` |
| `Database__AutoMigrate` | API | `true` → chạy migration 5 schema khi khởi động |
| `Database__AutoSeed` | API | `true` → seed tài khoản + sản phẩm mẫu |
| `Swagger__Enabled` | API | Bật Swagger cho môi trường demo/staging |
| `Cors__AllowedOrigins__0` | API | Origin public của storefront (nếu tách domain) |

---

## 5. Tài khoản seed sẵn

Khi `Database__AutoSeed=true`, hệ thống seed idempotent các tài khoản:

| Email | Mật khẩu | Vai trò |
|---|---|---|
| `admin@phonestore.vn` | `Admin@123` | ADMIN |
| `staff@phonestore.vn` | `Staff@123` | STAFF |
| `customer@phonestore.vn` | `Customer@123` | CUSTOMER |
| `demo@phonestore.vn` | `Demo@123` | CUSTOMER |

> **Lưu ý production**: đổi mật khẩu các tài khoản seed hoặc tắt `AutoSeed` sau lần deploy đầu.

---

## 6. Storefront: chức năng thật vs mockup

Storefront nằm tại `src/Host/PhoneStore.Api/wwwroot/` và được phục vụ trực tiếp bởi API.

| Nhóm | Trạng thái |
|---|---|
| Đăng ký / đăng nhập / đăng xuất / hồ sơ / địa chỉ | **Thật** — API Identity (FR-01 → FR-03) |
| Danh sách / chi tiết sản phẩm, lọc, sắp xếp, phân trang | **Thật** — API Catalog (FR-04, FR-05) |
| Giỏ hàng | **Thật** — API Cart (FR-06) |
| Checkout COD, đơn hàng, hủy đơn, lịch sử trạng thái | **Thật** — API Order (FR-07 → FR-09) |
| Bảng điều khiển nhân viên: tồn kho thấp, thu COD | **Thật** — API Inventory / Order (FR-10 → FR-11, FR-14) |
| Flash sale, khuyến mãi, trả góp, tin tức, FAQ, đánh giá, lịch sử giá, yêu thích, so sánh, cửa hàng, tra cứu mã đơn | **Mockup** — dữ liệu mô phỏng trong `assets/js/mock.js`, có banner "Mockup" trên UI |

---

## 7. Deploy lên server thật

```bash
# 1. Cài Docker + Docker Compose trên Ubuntu 22.04 / Debian 12
# 2. Clone repo và tạo file cấu hình
git clone <repo-url> phonestore && cd phonestore
cp docker/env.prod.sample docker/env.prod && nano docker/env.prod

# 3. Build & chạy
docker compose -f docker/docker-compose.prod.yml --env-file docker/env.prod up -d --build
```

Nâng cấp phiên bản mới:

```bash
git pull
docker compose -f docker/docker-compose.prod.yml --env-file docker/env.prod up -d --build
```

Bật HTTPS: đặt chứng chỉ Let's Encrypt vào thư mục `docker/certs/`, rồi bổ sung `listen 443 ssl`
cùng `server_name` trong `docker/nginx.conf` và map cổng `443:443` trong compose.

---

## 8. Xử lý sự cố

| Triệu chứng | Nguyên nhân thường gặp | Cách xử lý |
|---|---|---|
| API dừng ngay khi khởi động | Thiếu `PHONESTORE_SIGNING_KEY` hoặc key < 32 ký tự | Bổ sung trong `docker/env.prod` rồi `up -d` lại |
| `401` khi đăng nhập | Sai `PUBLIC_ORIGIN` khiến cookie/CSRF không khớp | Đặt `PUBLIC_ORIGIN` đúng URL public |
| Storefront trả 404 | nginx không tìm thấy `index.html` | Kiểm tra `docker logs phonestore-web` |
| Migration lỗi | DB chưa healthy | `docker compose logs postgres` — đợi `pg_isready` OK |
