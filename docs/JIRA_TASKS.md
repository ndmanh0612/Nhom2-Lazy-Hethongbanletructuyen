# Jira — Project N2LHTBLTT

> **Nhóm 2 - Lazy: Hệ thống bán lẻ trực tuyến**
> Project lead: Mạnh Nguyễn (S0000766) · Loại: `software` · Priority mặc định: Medium
> Export từ Jira ngày 25/10/2026 — nguồn: `Jira.csv`

## Thành viên & mã Jira

| Tên Jira | Tên thật | MSSV | Vai trò |
|---|---|---|---|
| Mạnh Nguyễn | Nguyễn Đức Mạnh | S0000766 | PM / Backend Lead |
| Trí Đỗ Công | Đỗ Công Trí | 0214268 | Identity & Auth |
| Trần Đại Nghĩa | Trần Đại Nghĩa | 0211768 | Catalog / Media |
| Nguyen Tien Dat | Nguyễn Tiến Đạt | 0206768 | Inventory / DevOps |
| toanlion24 | Mai Thanh Toàn | 221066 | Cart / Order |

## Board (theo `Custom field (Team)`)

| Board | Issue | Sprint |
|---|---|---|
| **Phân tích và thiết kế hệ thống** | 1, 2, 3, 4 | — |
| **Project Setup & Core Modules** | 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 | — |
| **Core Business Development** | 15, 16, 17, 18, 19, 20, 21, 22, 23, 24 | — |
| **Order Management & System** | 25, 26, 27, 28, 29, 30, 31, 32, 33, 34 | — |
| **Sprint 5 – DevOps, Testing** | 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45 | Sprint 5 |

Tất cả issue hiện ở trạng thái **To Do** (Status category: `To Do`).

## Issue theo người thực hiện

### Mai Thanh Toàn (`toanlion24`)
| Key | Summary | Board |
|---|---|---|
| N2LHTBLTT-12 | Xây dựng Domain Entities, Repository và API contract cho Cart/Order | Project Setup & Core Modules |
| N2LHTBLTT-14 | Thiết lập Swagger, Validation và Global Exception Handling | Project Setup & Core Modules |
| N2LHTBLTT-19 | FR-06: API giỏ hàng (thêm, sửa số lượng, xóa) | Core Business Development |
| N2LHTBLTT-20 | FR-07: API Checkout COD, tạo đơn và tính tổng tiền | Core Business Development |
| N2LHTBLTT-25 | FR-08: API xem danh sách, chi tiết và lịch sử đơn hàng | Order Management & System |
| N2LHTBLTT-26 | FR-09: API hủy đơn, kiểm tra trạng thái và hoàn tồn | Order Management & System |

### Mạnh Nguyễn (Lead)
| Key | Summary |
|---|---|
| N2LHTBLTT-1 | Phân tích yêu cầu chức năng và phi chức năng |
| N2LHTBLTT-3 | Thiết kế kiến trúc tổng thể và các thành phần hệ thống |
| N2LHTBLTT-5 | Khởi tạo .NET Solution theo Modular Monolith / Clean Architecture |
| N2LHTBLTT-6 | Thiết lập Git Workflow, Pull Request và cấu trúc repository |
| N2LHTBLTT-7 | Cấu hình database, DbContext và migrations theo module |
| N2LHTBLTT-22 | Xử lý transaction, khóa tồn kho và chống bán vượt tồn |
| N2LHTBLTT-23 | Tích hợp Identity, Catalog, Cart, Order và Inventory |
| N2LHTBLTT-24 | Kiểm thử tích hợp luồng tạo đơn, rollback và chống tạo trùng |
| N2LHTBLTT-29 | Tích hợp In-memory Event Bus giữa các module |

### Đỗ Công Trí
| Key | Summary |
|---|---|
| N2LHTBLTT-2 | Xây dựng sơ đồ phân rã chức năng |
| N2LHTBLTT-8 | FR-01: API đăng ký tài khoản khách hàng |
| N2LHTBLTT-9 | FR-02: API đăng nhập, đăng xuất và session authentication |
| N2LHTBLTT-15 | FR-03: Quản lý hồ sơ, mật khẩu và địa chỉ khách hàng |
| N2LHTBLTT-16 | FR-15: Quản lý tài khoản nhân viên, khóa/mở tài khoản và phân quyền |
| N2LHTBLTT-32 | FR-16: API thống kê doanh thu, trạng thái đơn và tồn kho thấp |
| N2LHTBLTT-33 | Kiểm thử phân quyền Customer / Staff / Admin |
| N2LHTBLTT-38 | Kiểm thử Authentication, Authorization và CSRF |
| N2LHTBLTT-41 | Kiểm thử hiệu năng bằng JMeter và tổng hợp kết quả |

### Trần Đại Nghĩa
| Key | Summary |
|---|---|
| N2LHTBLTT-4 | Thiết kế cơ sở dữ liệu, bảng, quan hệ và ERD |
| N2LHTBLTT-10 | FR-12: API quản lý danh mục và thương hiệu |
| N2LHTBLTT-11 | FR-04: API danh sách, tìm kiếm và lọc sản phẩm |
| N2LHTBLTT-17 | FR-05: API chi tiết sản phẩm và phiên bản SKU |
| N2LHTBLTT-18 | FR-13: CRUD sản phẩm, SKU, giá và thông số kỹ thuật |
| N2LHTBLTT-30 | Tích hợp MinIO/S3 và API upload ảnh sản phẩm |
| N2LHTBLTT-31 | Sinh Presigned URL và xử lý truy cập ảnh |
| N2LHTBLTT-40 | Chuẩn bị seed data và bộ Postman Collection |
| N2LHTBLTT-43 | Hoàn thiện README, API Documentation và hướng dẫn chạy |
| N2LHTBLTT-45 | Chuẩn bị kịch bản demo và bảo vệ đồ án |

### Nguyễn Tiến Đạt
| Key | Summary |
|---|---|
| N2LHTBLTT-13 | Xây dựng Inventory Module, Stock Entities và migration |
| N2LHTBLTT-21 | FR-14: API nhập kho, điều chỉnh tồn và nhật ký tồn kho |
| N2LHTBLTT-28 | FR-11: Xử lý giao thành công, thu COD và giao thất bại |
| N2LHTBLTT-29 | Tích hợp In-memory Event Bus giữa các module *(cùng Mạnh)* |
| N2LHTBLTT-34 | Kiểm thử luồng đơn hàng, hủy đơn, hoàn tồn và COD |
| N2LHTBLTT-35 | Viết Dockerfile multi-stage cho .NET Backend |
| N2LHTBLTT-36 | Cấu hình Docker Compose cho API, Database và MinIO |
| N2LHTBLTT-37 | Thiết lập Jenkins Pipeline: Build, Test và Deploy |

## Ánh xạ FR → Issue → Board

| FR | Issue | Mô-đun | Người |
|---|---|---|---|
| FR-01 | 8 | Identity | Trí |
| FR-02 | 9 | Identity | Trí |
| FR-03 | 15 | Identity | Trí |
| FR-04 | 11 | Catalog | Nghĩa |
| FR-05 | 17 | Catalog | Nghĩa |
| FR-06 | 19 | Cart | Toàn |
| FR-07 | 20 | Cart / Order | Toàn |
| FR-08 | 25 | Order | Toàn |
| FR-09 | 26 | Order | Toàn |
| FR-10 | 27 | Order | Mạnh |
| FR-11 | 28 | Order | Đạt |
| FR-12 | 10 | Catalog | Nghĩa |
| FR-13 | 18 | Catalog | Nghĩa |
| FR-14 | 21 | Inventory | Đạt |
| FR-15 | 16 | Identity | Trí |
| FR-16 | 32 | Reporting | Trí |

## Ghi chú về Sprint

README trong repo ghi Sprint 2 với mã nội bộ `S2-03`…`S2-10`. Đối chiếu Jira:
- `S2-08` (Mai Thanh Toàn — Cart & Order Domain Entities) → **N2LHTBLTT-12**
- `S2-10` (Mai Thanh Toàn — Global Exception & Swagger filters) → **N2LHTBLTT-14**
- `S2-04`, `S2-05` (Trí — Identity) → **N2LHTBLTT-8, N2LHTBLTT-9**
- `S2-06`, `S2-07` (Nghĩa — Catalog) → **N2LHTBLTT-10, N2LHTBLTT-11**
- `S2-03` (Đạt — Migrations + Inventory) → **N2LHTBLTT-7, N2LHTBLTT-13**

Jira hiện đã lên tới Sprint 5, Board "Order Management & System" là nhóm nghiệp vụ Order mới nhất. Tên branch/commit phải dùng mã Jira dạng số (`N2LHTBLTT-<số>`), không dùng mã `S2-xx`.
