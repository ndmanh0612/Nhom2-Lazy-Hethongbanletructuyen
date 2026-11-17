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
| `Database__AutoSeedSample` | API | `true` → thêm dữ liệu mẫu phong phú (23 sản phẩm, ảnh SVG, tồn kho khớp catalog/inventory). Mặc định `false` |
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

HTTPS và domain: xem mục 9 (Let's Encrypt) hoặc mục 10 (Cloudflare Tunnel).

---

## 8. Xử lý sự cố

| Triệu chứng | Nguyên nhân thường gặp | Cách xử lý |
|---|---|---|
| API dừng ngay khi khởi động | Thiếu `PHONESTORE_SIGNING_KEY` hoặc key < 32 ký tự | Bổ sung trong `docker/env.prod` rồi `up -d` lại |
| `401` khi đăng nhập | Sai `PUBLIC_ORIGIN` khiến cookie/CSRF không khớp | Đặt `PUBLIC_ORIGIN` đúng URL public |
| Storefront trả 404 | nginx không tìm thấy `index.html` | Kiểm tra `docker logs phonestore-web` |
| Migration lỗi | DB chưa healthy | `docker compose logs postgres` — đợi `pg_isready` OK |

---

## 9. Domain + HTTPS (Let's Encrypt)

1. Tạo bản ghi DNS: loại `A`, host `phonestore`, giá trị là IP server (zone `lazi.id.vn` quản lý tại zonedns.vn).
   Kiểm tra: `nslookup phonestore.lazi.id.vn 8.8.8.8` phải trả về IP server.
2. Trên server, trong `/opt/phonestore`:

   ```bash
   ./docker/enable-https.sh phonestore.lazi.id.vn <email>
   ```

   Script xin chứng chỉ bằng certbot (webroot `certbot-www/`), đặt `NGINX_CONF=nginx.ssl.conf` và
   `PUBLIC_ORIGIN=https://phonestore.lazi.id.vn` trong `docker/env.prod`, rồi khởi động lại stack.
   Nginx chuyển HTTP sang HTTPS; chứng chỉ tự gia hạn và nginx tự nạp lại.
3. Cấu hình nginx tách thành `nginx.conf` (chỉ HTTP), `nginx.ssl.conf` (HTTP→HTTPS + 443) và
   `nginx-locations.conf` (các location dùng chung).

---

## 10. Đưa web qua Cloudflare (Cloudflare Tunnel)

Cloudflare Tunnel chạy container `cloudflared` kết nối **chiều ra** tới Cloudflare, nên server
(hay cả máy cá nhân) **không cần IP public, không cần mở cổng 80/443, không cần certbot** —
Cloudflare lo HTTPS, CDN và chống DDoS.

```text
Browser ──HTTPS──▶ Cloudflare edge ──tunnel──▶ cloudflared ──HTTP──▶ web(nginx):80 ──▶ api:8080
```

### 10.1. Demo nhanh — không cần tài khoản (quick tunnel)

```bash
docker compose -f docker/docker-compose.prod.yml --env-file docker/env.prod --profile cloudflare-quick up -d --build
docker logs phonestore-cloudflared-quick 2>&1 | grep trycloudflare.com
```

Log in ra URL dạng `https://<ngẫu-nhiên>.trycloudflare.com`. URL đổi mỗi lần container khởi động lại,
chỉ dùng để demo.

### 10.2. Domain riêng (named tunnel)

Điều kiện: domain phải được quản lý DNS tại Cloudflare. Với `phonestore.lazi.id.vn`, zone
`lazi.id.vn` phải chuyển nameserver từ zonedns.vn sang Cloudflare (gói Free không nhận riêng subdomain).

1. Cloudflare Dashboard → **Add a domain** `lazi.id.vn` → đổi nameserver tại nhà đăng ký theo hướng dẫn.
   Nhớ tạo lại các bản ghi DNS đang dùng cho domain khác.
2. **Zero Trust → Networks → Tunnels → Create a tunnel** → loại *Cloudflared* → đặt tên `phonestore`.
   Ở bước *Install connector*, copy token (chuỗi sau `--token`).
3. Tab **Public Hostname** → thêm: subdomain `phonestore`, domain `lazi.id.vn`,
   service `HTTP` → `web:80`. Cloudflare tự tạo bản ghi CNAME. Xoá bản ghi `A phonestore` cũ nếu có.
4. Trong `docker/env.prod`:

   ```dotenv
   CLOUDFLARE_TUNNEL_TOKEN=<token vừa copy>
   NGINX_CONF=nginx.conf
   PUBLIC_ORIGIN=https://phonestore.lazi.id.vn
   ```

   `NGINX_CONF` phải là `nginx.conf` (chỉ HTTP). Nếu để `nginx.ssl.conf`, nginx sẽ chuyển hướng
   HTTP→HTTPS mãi và trình duyệt báo *too many redirects*.
5. Chạy:

   ```bash
   docker compose -f docker/docker-compose.prod.yml --env-file docker/env.prod --profile cloudflare up -d --build
   docker logs -f phonestore-cloudflared   # thấy "Registered tunnel connection" là xong
   ```

6. (Khuyến nghị) Khi đã chạy qua tunnel, đóng cổng 80/443 trên firewall server, hoặc đặt
   `WEB_PORT=127.0.0.1:8088` và `WEB_SSL_PORT=127.0.0.1:8443` để chỉ truy cập được qua Cloudflare.

nginx giữ nguyên header `X-Forwarded-Proto: https` do cloudflared gửi, nên API nhận đúng scheme
HTTPS và đặt cookie đăng nhập/CSRF với cờ `Secure`.

| Triệu chứng | Cách xử lý |
|---|---|
| `ERR_TOO_MANY_REDIRECTS` | Đổi `NGINX_CONF=nginx.conf` rồi `up -d` lại |
| Lỗi 502 / 1033 trên Cloudflare | `docker logs phonestore-cloudflared`; kiểm tra service là `http://web:80` |
| Đăng nhập lỗi 401/CSRF | `PUBLIC_ORIGIN` phải đúng `https://<domain>` |
