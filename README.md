# 📱 PhoneStore Backend - Modular Monolith

> **Đề tài**: Phân tích và thiết kế hệ thống bán điện thoại và phụ kiện (Tham khảo CellphoneS)  
> **Môn học**: Đồ Án Phát Triển Ứng Dụng Phía Máy Chủ Nâng Cao (Mã HP: 608819)  
> **Trường**: Đại học Xây Dựng Hà Nội (HUCE) - Khoa Công Nghệ Thông Tin  
> **Giảng viên hướng dẫn**: ThS. Lê Văn Minh, ThS. Nguyễn Đình Quý  

---

## 👥 Danh sách thành viên nhóm & Phân công vai trò

| STT | Họ và tên | MSSV | Vai trò chính | Trách nhiệm phụ trách |
|:---:|:---|:---:|:---|:---|
| 1 | **Nguyễn Đức Mạnh** (Lead) | S0000766 | PM / Backend Lead | Kiến trúc hệ thống, Ordering Module, Tích hợp & Review |
| 2 | **Đỗ Công Trí** | 0214268 | Backend Developer | Identity & Authentication Module, Quản lý khách hàng |
| 3 | **Trần Đại Nghĩa** | 0211768 | Backend Developer | Catalog Module, Sản phẩm, SKU, Media |
| 4 | **Mai Thanh Toàn** | 221066 | Backend Developer | Cart Module, Checkout COD, Order Lifecycle |
| 5 | **Nguyễn Tiến Đạt** | 0206768 | Backend / DevOps | Inventory Module, CSDL Migrations, Docker, CI/CD |

---

## 🏛️ Kiến trúc hệ thống (Modular Monolith & Clean Architecture)

Hệ thống được thiết kế theo chuẩn **Modular Monolith** kết hợp **Clean Architecture**, phân chia ranh giới nghiệp vụ (Boundaries) rõ ràng và cô lập CSDL theo từng Schema riêng biệt:

```text
PhoneStore/
├── src/
│   ├── BuildingBlocks/
│   │   └── PhoneStore.BuildingBlocks/         # Shared Kernel, BaseEntity, Result, IEventBus, CQRS
│   ├── Modules/
│   │   ├── Identity/                          # Schema: identity (Users, Addresses, Auth)
│   │   ├── Catalog/                           # Schema: catalog (Categories, Brands, Products, Variants)
│   │   ├── Cart/                              # Schema: cart (Carts, CartItems)
│   │   ├── Order/                             # Schema: ordering (Orders, OrderItems, OrderStatusHistories)
│   │   └── Inventory/                         # Schema: inventory (Stock, Movements, Adjustments)
│   └── Host/
│       └── PhoneStore.Api/                    # Web API Host (Swagger, Middleware, Routing)
├── tests/
│   ├── PhoneStore.ArchitectureTests/          # Kiểm tra ranh giới kiến trúc & rò rỉ module
│   ├── PhoneStore.UnitTests/                  # Unit tests nghiệp vụ
│   └── PhoneStore.IntegrationTests/           # E2E test qua HTTP thật (WebApplicationFactory + PostgreSQL)
├── docker/                                    # docker-compose.yml (PostgreSQL 16, MinIO S3)
├── .github/                                   # CI Pipeline (GitHub Actions) & PR template
├── GIT_WORKFLOW.md                            # Cẩm nang quy chuẩn Git cho nhóm
└── README.md
```

### 🔒 Nguyên tắc kiến trúc bắt buộc:
1. **Không rò rỉ tham chiếu**: Không module nào được reference trực tiếp tới DbContext hay implementation của module khác.
2. **Cô lập Schema CSDL**: Mỗi module sở hữu DbContext riêng với schema độc lập (`identity`, `catalog`, `cart`, `ordering`, `inventory`).
3. **Giao tiếp liên module**: Thông qua public Contracts hoặc In-memory Event Bus trong `BuildingBlocks`.

---

## 🚀 Hướng dẫn khởi chạy dự án cho thành viên nhóm

### 1. Yêu cầu môi trường
- .NET 10 SDK (hoặc .NET 8/9 compatible)
- Docker Desktop (cho PostgreSQL và MinIO)
- Git & IDE (Visual Studio 2022 / JetBrains Rider / VS Code)

### 2. Khởi chạy Database & MinIO (Docker)
```bash
docker compose -f docker/docker-compose.yml up -d
```
- **PostgreSQL**: `localhost:5433` | DB: `phonestore_db` | User: `postgres` | Pass: `postgres`
- **MinIO Console**: `http://localhost:9001` (User: `admin`, Pass: `password123`)

> Connection string mặc định nằm trong `src/Host/PhoneStore.Api/appsettings.json`
> (`ConnectionStrings:DefaultConnection`). Có thể override bằng biến môi trường
> `ConnectionStrings__DefaultConnection`.

### 3. Build và Chạy Web API
```bash
# Build toàn bộ Solution
dotnet build PhoneStore.slnx

# Chạy Web API Backend (Development)
dotnet run --project src/Host/PhoneStore.Api/PhoneStore.Api.csproj
```

> Chi tiết về cách chạy test: xem [mục 8](#8-chạy-test).
> Chi tiết về cổng (Swagger): xem [mục 4](#4-truy-cập-swagger-ui).

### 4. Truy cập Swagger UI
Mở trình duyệt: `http://localhost:5122/` (profile `http`) hoặc `https://localhost:7168/` (profile `https`).
Port này lấy từ `src/Host/PhoneStore.Api/Properties/launchSettings.json`. Muốn đổi port khi chạy thủ công:

```bash
# Windows (PowerShell)
$env:ASPNETCORE_URLS="http://localhost:5099"; dotnet run --project src/Host/PhoneStore.Api/PhoneStore.Api.csproj

# macOS / Linux
ASPNETCORE_URLS="http://localhost:5099" dotnet run --project src/Host/PhoneStore.Api/PhoneStore.Api.csproj
```

Ở môi trường Development, Swagger UI nằm ở root (`/`), còn OpenAPI JSON ở `/swagger/v1/swagger.json`.

### 5. Tài khoản seed sẵn
Dữ liệu mẫu được seed tự động khi app khởi động ở môi trường Development
(`src/Host/PhoneStore.Api/Seeding/DatabaseSeeder.cs`, idempotent — chạy lại không nhân bản):

| Email | Mật khẩu | Vai trò |
|---|---|---|
| `customer@phonestore.vn` | `Customer@123` | CUSTOMER |
| `staff@phonestore.vn` | `Staff@123` | STAFF |
| `demo@phonestore.vn` | `Demo@123` | CUSTOMER |

### 6. Cơ chế xác thực tạm thời
JWT thật đang chờ task `N2LHTBLTT-8` / `N2LHTBLTT-9` (Identity & Auth). Hiện API nhận danh tính
qua header tạm thời:

```
X-User-Id: 2001
```

Thiếu header → API trả **401**. Khi Identity hoàn tất sẽ đổi sang claim `sub` / `NameIdentifier`;
chi tiết ở [`docs/reviews/ASK-TRI-AUTH-TOKEN-ISSUE.md`](docs/reviews/ASK-TRI-AUTH-TOKEN-ISSUE.md).

### 7. Tình trạng test hiện tại

| Project | Số test | Loại |
|---|---:|---|
| `PhoneStore.UnitTests` | 35 | Unit test nghiệp vụ domain + application service |
| `PhoneStore.ArchitectureTests` | 7 | Ranh giới module (không rò rỉ tham chiếu) |
| `PhoneStore.IntegrationTests` | 126 | E2E qua HTTP thật với `WebApplicationFactory` + PostgreSQL |
| **Tổng** | **168** | |

Bộ integration test gồm các file chính:

| File | Số test | Phủ |
|---|---:|---|
| `BusinessFlowRegressionTests` | 32 | Hồi quy luồng giỏ / checkout / hủy đơn / phân quyền |
| `CatalogAndIdentityRegressionTests` | 30 | Hồi quy lọc, sắp xếp, phân trang Catalog + đăng ký / đăng nhập |
| `EndToEndOrderJourneyTests` | 16 | Hành trình đặt hàng đầu-cuối |
| `IdentityApiTests` | 12 | Đăng ký, đăng nhập, hồ sơ, mật khẩu, địa chỉ |
| `CatalogQueryTests` | 10 | Tìm kiếm, lọc giá/danh mục, sắp xếp, phân trang |
| `OrderLifecycleTests` | 10 | Danh sách / chi tiết / lọc trạng thái / hủy đơn |
| `CartApiTests` | 8 | Thêm, gộp, sửa, xóa, vượt giới hạn, xác thực |
| `CheckoutFlowTests` | 7 | Health, catalog, checkout COD, validation |
| `StockRestoreOnCancelTests` | 1 | Hủy đơn → hoàn tồn kho `inventory.product_stocks` |

### 8. Chạy test

```bash
# Build trước
dotnet build PhoneStore.slnx

# Toàn bộ test (khoảng 40s, vì integration test cần PostgreSQL thật)
dotnet test PhoneStore.slnx

# Từng bộ riêng
dotnet test tests/PhoneStore.UnitTests/PhoneStore.UnitTests.csproj
dotnet test tests/PhoneStore.ArchitectureTests/PhoneStore.ArchitectureTests.csproj
dotnet test tests/PhoneStore.IntegrationTests/PhoneStore.IntegrationTests.csproj

# Chạy 1 test / 1 class cụ thể
dotnet test tests/PhoneStore.IntegrationTests --filter "FullyQualifiedName~BusinessFlowRegressionTests"
dotnet test tests/PhoneStore.IntegrationTests --filter "FullyQualifiedName~Checkout_clears_the_cart"
```

> **Bắt buộc cho integration test**: PostgreSQL phải chạy sẵn ở `localhost:5433` (user/pass `postgres`).
> Test tự tạo database riêng (tên random `phonestore_test_<guid>`) rồi xoá sau khi hoàn tất —
> không đụng tới `phonestore_db`. Nếu PostgreSQL của bạn ở port khác, override biến môi trường:
>
> ```bash
> export PHONESTORE_TEST_ADMIN_CONNECTION="Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres"
> ```
>
> Test đã tắt parallelization (`AssemblyInfo.cs`) vì connection string được override bằng biến môi
> trường toàn cục — không chạy song song 2 tiến trình `dotnet test` cùng lúc.

### 9. Gọi API bằng `curl`

```bash
# Health
curl http://localhost:5122/health

# Danh sách sản phẩm (lọc / sắp xếp / phân trang)
curl "http://localhost:5122/api/v1/products?search=iphone&sortBy=price_asc&page=1&pageSize=10"
curl http://localhost:5122/api/v1/categories
curl http://localhost:5122/api/v1/brands

# Đăng ký → lấy userId trong response
curl -X POST http://localhost:5122/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"khach@example.com","fullName":"Khách Hàng","phone":"0912345678","password":"Pass@123"}'

# Đăng nhập
curl -X POST http://localhost:5122/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"customer@phonestore.vn","password":"Customer@123"}'

# Giỏ hàng (cần header X-User-Id tạm thời)
curl -H "X-User-Id: 2001" http://localhost:5122/api/v1/cart

curl -X POST http://localhost:5122/api/v1/cart/items \
  -H "X-User-Id: 2001" -H "Content-Type: application/json" \
  -d '{"productVariantId":1,"quantity":2}'

# Checkout COD
curl -X POST http://localhost:5122/api/v1/orders/checkout \
  -H "X-User-Id: 2001" -H "Content-Type: application/json" \
  -d '{"receiverName":"Nguyễn Văn A","receiverPhone":"0912345678","shippingAddress":"123 Nguyễn Huệ, Quận 1, TP.HCM"}'

# Đơn hàng
curl -H "X-User-Id: 2001" http://localhost:5122/api/v1/orders
curl -H "X-User-Id: 2001" "http://localhost:5122/api/v1/orders?status=PENDING"

curl -X POST http://localhost:5122/api/v1/orders/1/cancel \
  -H "X-User-Id: 2001" -H "Content-Type: application/json" \
  -d '{"reason":"Khách đổi ý."}'
```

**Quy ước response**: mọi response (thành công và lỗi) dùng chung envelope
`{ success, message, data, timestamp }`. Response lỗi có thêm `code` và `requestId`.

| HTTP | Mã lỗi (`code`) | Ý nghĩa |
|---:|---|---|
| 400 | `VALIDATION_ERROR` | Dữ liệu gửi lên không hợp lệ |
| 401 | `UNAUTHORIZED` | Thiếu hoặc sai thông tin xác thực |
| 404 | `PRODUCT_NOT_FOUND`, `CART_ITEM_NOT_FOUND`, `USER_NOT_FOUND` | Không tìm thấy dữ liệu |
| 409 | `EMAIL_ALREADY_EXISTS`, `CATEGORY_ALREADY_EXISTS`, `INVALID_ORDER_STATUS` | Xung đột trạng thái / dữ liệu trùng |

---

## 📋 Phân công công việc Sprint 2 cho 4 anh em tiếp tục

Bộ khung Base Code và Git Workflow (`N2LHTBLTT-5`, `N2LHTBLTT-6`) đã hoàn tất. 4 anh em pull nhánh `develop` về, tạo feature branch theo `GIT_WORKFLOW.md` và thực hiện các task:

| Thành viên | Mã công việc | Chi tiết nhiệm vụ Sprint 2 | Thư mục phụ trách |
|:---|:---:|:---|:---|
| **Đỗ Công Trí** | `S2-04`, `S2-05` | Triển khai chi tiết logic API Đăng ký (`FR-01`), Đăng nhập/Đăng xuất & Session Auth (`FR-02`) | `src/Modules/Identity/` |
| **Trần Đại Nghĩa** | `S2-06`, `S2-07` | Triển khai chi tiết logic API Danh mục, Thương hiệu (`FR-12`), Tìm kiếm & Lọc sản phẩm (`FR-04`) | `src/Modules/Catalog/` |
| **Mai Thanh Toàn** | `S2-08`, `S2-10` | Xây dựng đầy đủ Domain Entities Cart, Order (`FR-06`, `FR-07`), cấu hình Global Exception & Swagger filters | `src/Modules/Cart/`, `src/Modules/Order/` |
| **Nguyễn Tiến Đạt** | `S2-03`, `S2-09` | Tạo Migration DB PostgreSQL theo 5 schema, xây dựng Domain Entities & Migration cho Inventory Module | `src/Modules/Inventory/`, `docker/` |

> *Chi tiết quy chuẩn commit, tạo nhánh và mở PR xem tại [GIT_WORKFLOW.md](GIT_WORKFLOW.md).*
