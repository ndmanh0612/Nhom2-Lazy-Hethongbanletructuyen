# Review: Database / Inventory — `feature/N2LHTBLTT-S2-03-db-migrations-inventory`

**Người review**: Mai Thanh Toàn · **Ngày**: 28/10/2026
**Nhánh được review**: `feature/N2LHTBLTT-S2-03-db-migrations-inventory` (DatNT) — commit `7f54bc1`, `7ce7bd8`

> Đã build + chạy test nhánh này: build OK (0 error), test xanh 9 unit + 2 architecture.
> Tuy nhiên nhánh đang đứng trên commit gốc `afcc987` nên **chưa có code Cart/Order/Auth** để đối chiếu — đây chính là nguồn phát sinh các xung đột bên dưới.

---

## 🔴 Blocker

### 1. Trùng migration → app chết ngay khi apply

Nhánh hiện tại (`toandev`) đã có:

| Migration | Tạo bảng |
|---|---|
| `20261025173434_InitialInventory` | `inventory.product_stocks` |
| `20261026154118_AddStockMovements` | `inventory.stock_movements` |

Nhánh này thêm `20261027152014_InitInventory` → tạo lại `inventory.stocks` + `inventory.stock_movements`.
Khi `Database.Migrate()` chạy sẽ báo *"relation already exists"* và ứng dụng không khởi động được.

### 2. Xung đột model domain (add/add)

| Nhánh này | Nhánh hiện tại (`toandev`) |
|---|---|
| `Stock` (cột `Sku`, `QuantityOnHand`, `QuantityReserved`) | `ProductStock` (cột `StockQuantity`) |
| `MovementType` (INBOUND/OUTBOUND/ADJUSTMENT_IN/ADJUSTMENT_OUT/RETURN) | `StockMovementType` (IMPORT/ADJUSTMENT/RESERVATION/RESTORE) |
| `StockMovement` FK `StockId`, cột `Reason`, `QuantityAfter` | `StockMovement` FK `ProductVariantId`, cột `QuantityBefore`, `OrderId`, `PerformedBy`, `Note` |

`git merge-tree` xác nhận conflict ở `StockMovement.cs` và `InventoryDbContext.cs`.

### 3. Hệ quả dây chuyền — vỡ luồng checkout

Các file sau đang viết cho model hiện tại, lấy model của nhánh này sẽ phải sửa lại tất cả:

- `InventoryContract` — khóa dòng `FOR UPDATE`, trả `bool` cho checkout
- `InventoryService`
- `InventoryController`
- `ProductVariantCreatedHandler`
- `DatabaseSeeder`
- `InventoryApiTests`, `CheckoutFlowTests`, `StockRestoreOnCancelTests`

### 4. Nhánh không có base `develop`/`toandev`

Branch bám `afcc987`. `git merge-tree HEAD origin/feature/N2LHTBLTT-S2-03-db-migrations-inventory` báo conflict ở 8 file:

- `docker/docker-compose.yml`
- `src/Host/PhoneStore.Api/appsettings.json`
- `src/Modules/Cart/PhoneStore.Modules.Cart/CartModuleRegistration.cs`
- `src/Modules/Catalog/PhoneStore.Modules.Catalog/CatalogModuleRegistration.cs`
- `src/Modules/Identity/PhoneStore.Modules.Identity/IdentityModuleRegistration.cs`
- `src/Modules/Inventory/.../InventoryDbContext.cs`
- `src/Modules/Inventory/.../Migrations/InventoryDbContextModelSnapshot.cs`
- `src/Modules/Inventory/.../InventoryModuleRegistration.cs`

---

## 🟠 Major

### 5. Đổi port/password DB gây hỏng môi trường local

Nhánh này đổi `POSTGRES_PASSWORD: postgrespassword` + port `5433`. Ở nhánh hiện tại:

- `docker-compose.yml` đã chạy `5433:5432` với password `postgres`
- `ci.yml` dùng `5432` / `postgres`
- Volume `pgdata` đang tồn tại với password cũ → `postgres` sẽ không login được sau khi đổi, phải xoá volume và seed lại

### 6. `Program.cs` chỉ migrate mỗi Inventory

```csharp
var inventoryDb = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
inventoryDb.Database.Migrate();
```

4 DbContext còn lại (identity/catalog/cart/ordering) không được migrate → DB thiếu bảng, app lỗi ngay ở bước seed. Cần migrate toàn bộ 5 schema.

### 7. Controller bỏ qua lớp service + thiếu phân quyền

`InventoryController` ở nhánh này inject thẳng `InventoryDbContext`, trả anonymous object (không theo envelope `ApiResponse<T>` của dự án), `Take(50)` hardcode cho movements, và **không có `[Authorize]`** dù route là `/api/v1/admin/inventory/...` → bất kỳ ai cũng đọc được tồn kho.

### 8. Thiếu ràng buộc ở tầng DB

Bảng `stocks` không có index theo `QuantityOnHand`, không có check constraint `QuantityOnHand >= 0` — ràng buộc chỉ nằm ở logic domain.

---

## 🟡 Lỗi logic trong domain của nhánh này

### 9. `RemoveStock` không trừ `QuantityReserved`

Đơn đã đặt trước rồi xuất kho → `QuantityReserved` bị bỏ mồ côi, `AvailableQuantity` tính sai.

### 10. `Adjust(0)` không bị chặn

Rơi vào nhánh `ADJUSTMENT_IN` và tạo movement với `Quantity = 0` (đáng lẽ phải ném exception).

### 11. `Stock.Create` không validate `productVariantId > 0`

---

## ✅ Đề xuất

1. **Không merge model của nhánh này.** Model hiện tại (`ProductStock` / `StockMovementType`) là chuẩn vì Cart/Order/Auth đang phụ thuộc.
2. **Phần còn giá trị để lấy**:
   - `Microsoft.EntityFrameworkCore.Design` cho các module
   - `IDesignTimeDbContextFactory` cho cả 5 module
   - script `dotnet ef database update` theo từng schema
   - CI step apply migration
3. Ý tưởng tách `QuantityOnHand` / `QuantityReserved` là **task mới** (`N2LHTBLTT-13`) và phải viết thêm migration `AddReservedQuantity` trên model hiện tại — không phải merge nhánh này.
4. Bổ sung các cột `QuantityBefore`, `OrderId`, `PerformedBy` vào nhật ký biến động (đã có ở nhánh chính, thiếu ở đây).

---

## Checklist cho Đạt khi sửa lại nhánh

- [ ] Rebase lên `develop` (hoặc `toandev`) để có code Cart/Order/Auth mới nhất
- [ ] Xoá `20261027152014_InitInventory` + `InventoryDbContextModelSnapshot.cs` khỏi nhánh
- [ ] Đổi tên entity cho khớp model chuẩn: `ProductStock`, `StockMovementType`
- [ ] Bổ sung cột `QuantityBefore`, `OrderId`, `PerformedBy` vào `StockMovement`
- [ ] `InventoryController` dùng `IInventoryService`, trả `ApiResponse<T>`, thêm `[Authorize(Policy = RequireStaff)]`
- [ ] `Program.cs` migrate cả 5 DbContext
- [ ] Giữ nguyên port/password DB của nhánh chính (`5433` / `postgres`) hoặc cập nhật đồng bộ cả `ci.yml`
- [ ] Thêm test: oversell, `Adjust` âm, migration apply trên DB trống
