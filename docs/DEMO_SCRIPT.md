# 🎬 Kịch Bản Demo & Bảo Vệ Đồ Án (N2LHTBLTT-45)

> **Dự án:** Hệ thống Bán lẻ Trực tuyến PhoneStore (Modular Monolith .NET 10 + PostgreSQL)  
> **Tài liệu căn cứ:** Báo cáo Phân tích Thiết kế (Mục 6.3: Kịch bản demo kiểm chứng chức năng bắt buộc)  
> **Người chuẩn bị:** Trần Đại Nghĩa (`TranNghiaa`) & Nhóm 2 - Lazy

---

## 👥 Danh sách tài khoản thử nghiệm (Pre-seeded Accounts)

| Vai trò (Role) | Email | Mật khẩu | Mục đích sử dụng trong Demo |
|---|---|---|---|
| **ADMIN** | `admin@phonestore.local` | `Password123!` | Quản trị toàn quyền, xem báo cáo doanh thu FR-16 |
| **STAFF** | `staff@phonestore.local` | `Password123!` | Quản lý kho, xác nhận đơn hàng, giao hàng & thu COD |
| **CUSTOMER 1** | `customer@phonestore.local` | `Password123!` | Khách hàng mua sắm, đặt hàng COD, xem lịch sử đơn |
| **CUSTOMER 2** | `customer2@phonestore.local` | `Password123!` | Khách hàng thứ hai test chống bán vượt tồn & hủy đơn |

---

## 📋 Trình tự 7 Bước Demo Chuẩn (Tối ưu trong 10-15 phút)

### 🔹 Bước 1: Khởi động hệ thống & Đăng nhập Quản trị viên
1. **Kiểm tra trạng thái Service:**
   ```bash
   curl -X GET http://localhost:5122/health
   ```
   *Kỳ vọng:* `200 OK`, `status: "Healthy"`, hiển thị đủ 5 module: `Identity`, `Catalog`, `Cart`, `Order`, `Inventory`.

2. **Đăng nhập quyền Admin (`admin@phonestore.local`):**
   ```bash
   curl -X POST http://localhost:5122/api/v1/auth/login \
     -H "Content-Type: application/json" \
     -d '{"email":"admin@phonestore.local","password":"Password123!"}'
   ```
   *Kỳ vọng:* Trả về JWT Access Token (Role: `ADMIN`).

---

### 🔹 Bước 2: Quản trị tạo Sản phẩm mới & Nhập kho (FR-13 & FR-14)
1. **Tạo sản phẩm iPhone 15 Pro Max (kèm SKU):**
   - Gọi `POST /api/v1/admin/products` tạo sản phẩm và phiên bản `IP15PM-256-NAT`.
2. **Nhập kho tồn ban đầu (Số lượng: 2 cái):**
   ```bash
   curl -X POST http://localhost:5122/api/v1/admin/inventory/stocks/import \
     -H "Authorization: Bearer <ADMIN_TOKEN>" \
     -H "Content-Type: application/json" \
     -d '{"productVariantId": 1, "quantity": 2, "unitCost": 28000000, "note": "Nhập kho đợt 1"}'
   ```
   *Kỳ vọng:* Tồn kho khả dụng = 2. Nhật ký tồn kho ghi nhận `IMPORT`.

---

### 🔹 Bước 3: Khách hàng 1 duyệt sản phẩm, thêm giỏ hàng & Đặt hàng COD (FR-04, 06, 07)
1. **Khách hàng 1 đăng nhập:** Lấy token `CUSTOMER 1`.
2. **Thêm vào giỏ hàng:** `POST /api/v1/cart/items` (số lượng 2 cái).
3. **Thực hiện Checkout COD:**
   ```bash
   curl -X POST http://localhost:5122/api/v1/orders/checkout \
     -H "Authorization: Bearer <CUSTOMER1_TOKEN>" \
     -H "Content-Type: application/json" \
     -d '{"receiverName":"Nguyễn Văn A","receiverPhone":"0901234567","shippingAddress":"Số 1 Đại Cồ Việt, Hà Nội"}'
   ```
   *Kỳ vọng:* 
   - Đơn hàng được tạo ở trạng thái `PENDING`.
   - Tồn kho khả dụng của sản phẩm giảm từ 2 về 0 (Reserved).

---

### 🔹 Bước 4: Khách hàng 2 thử mua khi hết hàng (Chứng minh chống bán vượt tồn N2LHTBLTT-22)
1. **Khách hàng 2 đăng nhập:** Lấy token `CUSTOMER 2`.
2. **Thử Checkout cùng sản phẩm đó:**
   *Kỳ vọng:* Hệ thống trả về lỗi `400 Bad Request` hoặc `409 Conflict`: `"Tồn kho không đủ"`.
   *Ý nghĩa chứng minh:* Khóa bi quan `SELECT FOR UPDATE` ngăn chặn tuyệt đối tình trạng race condition và bán vượt tồn kho.

---

### 🔹 Bước 5: Nhân viên duyệt đơn, giao hàng & Thu tiền COD (FR-10 & FR-11)
1. **Nhân viên đăng nhập (`staff@phonestore.local`):** Lấy token `STAFF`.
2. **Xác nhận đơn hàng:**
   `POST /api/v1/orders/{orderId}/confirm` → Trạng thái chuyển thành `CONFIRMED`.
3. **Bàn giao vận chuyển:**
   `POST /api/v1/orders/{orderId}/ship` → Trạng thái chuyển thành `SHIPPING`.
4. **Giao hàng thành công & Thu tiền COD:**
   ```bash
   curl -X POST http://localhost:5122/api/v1/orders/{orderId}/deliver \
     -H "Authorization: Bearer <STAFF_TOKEN>" \
     -H "Content-Type: application/json" \
     -d '{"collectedAmount": 60000000, "note": "Giao thành công, khách đã thanh toán tiền mặt"}'
   ```
   *Kỳ vọng:* Đơn hàng chuyển sang `DELIVERED`, lưu vết `collectedAt` và `collectedAmount`.

---

### 🔹 Bước 6: Quản trị xem Dashboard Thống kê Doanh thu (FR-16 / N2LHTBLTT-32)
1. **Gọi API Thống kê:**
   ```bash
   curl -X GET http://localhost:5122/api/v1/admin/reports/dashboard \
     -H "Authorization: Bearer <ADMIN_TOKEN>"
   ```
   *Kỳ vọng phản hồi:*
   ```json
   {
     "success": true,
     "data": {
       "orderCountsByStatus": {
         "PENDING": 0,
         "CONFIRMED": 0,
         "SHIPPING": 0,
         "DELIVERED": 1,
         "CANCELLED": 0
       },
       "totalOrders": 1,
       "revenue": {
         "totalRevenue": 60000000,
         "totalShippingFee": 0,
         "deliveredOrderCount": 1
       },
       "lowStockItems": [
         { "productVariantId": 1, "stockQuantity": 0 }
       ]
     }
   }
   ```
   *Ý nghĩa chứng minh:* Doanh thu chỉ ghi nhận từ đơn DELIVERED; cảnh báo chính xác sản phẩm có tồn kho thấp.

---

### 🔹 Bước 7: Kiểm chứng Phân quyền & Hủy đơn Hoàn tồn (NFR-02 & FR-09)
1. **Chứng minh Phân quyền (RBAC):**
   - Lấy token `CUSTOMER` gọi API quản trị `GET /api/v1/admin/staff` hoặc `GET /api/v1/admin/reports/dashboard`.
   - *Kỳ vọng:* Trả về `403 Forbidden` (Khách không được phép truy cập tài nguyên quản trị).
2. **Chứng minh Hủy đơn hoàn tồn:**
   - Khách tạo một đơn hàng mới (tồn kho giảm).
   - Khách gọi `POST /api/v1/orders/{orderId}/cancel`.
   - Kiểm tra lại tồn kho: Tồn kho được hoàn trả tự động nhờ cơ chế Event Bus (`OrderCancelledEvent` → `OrderCancelledHandler`).
