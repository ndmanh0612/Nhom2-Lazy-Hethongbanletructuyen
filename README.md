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

# Chạy toàn bộ test (unit + architecture + integration E2E)
dotnet test PhoneStore.slnx

# Chạy Web API Backend
dotnet run --project src/Host/PhoneStore.Api/PhoneStore.Api.csproj
```

> **Lưu ý về integration test**: `PhoneStore.IntegrationTests` cần PostgreSQL chạy sẵn ở port 5433.
> Test tự tạo database riêng (tên random) cho từng lần chạy và xoá sau khi hoàn tất —
> không đụng tới `phonestore_db`. Biến môi trường `PHONESTORE_TEST_ADMIN_CONNECTION`
> có thể override connection string quản trị (mặc định `postgres` trên `postgres`).

### 4. Truy cập Swagger UI
Mở trình duyệt: `http://localhost:5000/` hoặc `https://localhost:5001/` để xem toàn bộ danh mục API của các module.
Ở môi trường Development, Swagger UI nằm ở root (`/`), còn OpenAPI JSON ở `/swagger/v1/swagger.json`.

### 5. Tình trạng test hiện tại

| Project | Số test | Loại |
|---|---:|---|
| `PhoneStore.UnitTests` | 34 | Unit test nghiệp vụ domain + application service |
| `PhoneStore.ArchitectureTests` | 7 | Ranh giới module (không rò rỉ tham chiếu) |
| `PhoneStore.IntegrationTests` | 48 | E2E qua HTTP thật với `WebApplicationFactory` + PostgreSQL |
| **Tổng** | **89** | |

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
