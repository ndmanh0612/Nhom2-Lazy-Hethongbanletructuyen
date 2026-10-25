# Hỏi Mạnh — Task N2LHTBLTT-12 (Cart/Order)

> 2 phiên bản: **Tin nhắn ngắn** (gửi nhanh) và **Bản đầy đủ** (họp nhóm / đính kèm Jira).

---

## Phiên bản 1 — Tin nhắn ngắn

```
Mạnh ơi, tôi vừa xong task N2LHTBLTT-12 (domain entities + repository + API contract cho Cart/Order),
23 file, 27 test xanh, đã commit trên nhánh feature/N2LHTBLTT-12-cart-order-entities.

Có 3 chỗ đang bị kẹt cần bạn chốt hướng trước khi tôi làm tiếp task 19 (API giỏ hàng) với task 20
(checkout COD):

1) GIÁ: Cart không được reference thẳng CatalogDbContext, nên lấy giá phải qua contract hoặc event bus.
Tôi đang để UnitPrice do client gửi lên trong AddToCartRequest — làm vậy thì client đặt giá 1 đồng
là mua được iPhone 😅. Tôi đề xuất đặt 1 interface IProductPricingContract trong BuildingBlocks, Catalog
implement, Cart/Order chỉ biết interface. Bạn thấy ổn không, hay đang tính dùng Event Bus cho cái này?

(Đã chốt theo giả định: tạo IProductPricingContract trong BuildingBlocks, Catalog implement, bỏ giá
khỏi AddToCartRequest. Chi tiết ở cuối tài liệu.)

2) TRANSACTION: checkout cần ghi giỏ (schema cart) + ghi đơn (schema ordering) + trừ tồn kho (schema
inventory) trong 1 transaction, mà 3 module lại 3 DbContext riêng. Tôi đang phân vân giữa TransactionScope,
outbox, hay 1 DbContext chung. Bạn đã có hướng nào cho task N2LHTBLTT-22 chưa?

3) HOÀN TỒN KHI HỦY ĐƠN (task 26): bạn với Đạt thống nhất tên event với payload chưa? Tôi cần biết
event mang ProductVariantId + Quantity để Inventory hoàn kho, đặt tên gì, đặt ở BuildingBlocks hay
trong module Order.

Nếu chưa có câu trả lời thì cứ để tôi đặt giả định làm tiếp được, sửa sau cũng được, đừng bị delay
vì tôi nha 🙏
```

---

## Phiên bản 2 — Bản đầy đủ

### Bối cảnh

Tôi vừa hoàn thành task `N2LHTBLTT-12` — dựng domain entities, repository và API contract cho Cart/Order
module. Commit `600a3e5` trên nhánh `feature/N2LHTBLTT-12-cart-order-entities`, chưa merge. 23 file,
+1097 dòng, 27/27 test xanh.

**Phần đã xong:**

- Cart: `Cart`, `CartItem`, enum `CartStatus` (ACTIVE / CONVERTED / ABANDONED), `ICartRepository`
- Order: `Order`, `OrderItem`, `OrderStatusHistory`, `OrderItemDraft`, enum `OrderStatus`
  (PENDING → CONFIRMED → SHIPPING → DELIVERED, DELIVERY_FAILED, CANCELLED), `IOrderRepository`
- EF Core mapping cho 5 bảng: `cart.carts`, `cart.cart_items`, `ordering.orders`,
  `ordering.order_items`, `ordering.order_status_histories`
- 12 record API contract cho Cart và Order
- Unit test + architecture test bảo vệ ranh giới module

**Các task sau `N2LHTBLTT-12`:** `N2LHTBLTT-19` (FR-06 giỏ hàng), `N2LHTBLTT-20` (FR-07 checkout COD),
`N2LHTBLTT-25` (FR-08 đơn hàng), `N2LHTBLTT-26` (FR-09 hủy đơn).

### 3 chỗ cần bạn chốt giúp

---

#### 1. Cart/Order lấy giá sản phẩm ở đâu? — 🔴 Chặn task 19, 20

**Vấn đề.** Cart module không được reference thẳng `CatalogDbContext` (nguyên tắc "không rò rỉ tham chiếu"
trong đề tài), nhưng giá bán thì bắt buộc phải lấy từ Catalog.

**Hiện trạng.** Contract `AddToCartRequest` đang nhận `UnitPrice`, `ProductName`, `Sku` từ client:

```csharp
public record AddToCartRequest(
    [Required] long ProductVariantId,
    [Required, MaxLength(200)] string ProductName,
    [Required, MaxLength(64)] string Sku,
    [Range(0, long.MaxValue)] long UnitPrice,
    [Range(1, 10)] int Quantity = 1);
```

Implement task 19 theo contract này thì client gửi `UnitPrice = 1` là mua được iPhone giá 1 đồng.

**Cần bạn chốt:**

- Chọn hướng nào — **query contract** (interface trong `BuildingBlocks`) hay **Event Bus**?
- Nếu là query contract: interface đặt ở đâu, ai implement, ai đăng ký DI?
- Nếu là Event Bus: ai phát event (`Catalog` khi đổi giá hay khi có đơn?), payload gồm những gì, và
  tầng đồng bộ thì event có kịp đến trước khi request xử lý xong không? In-memory bus hiện chạy sync
  qua MediatR nên dùng được, nhưng nếu bạn đang tính đổi hướng sang Outbox thì kiến trúc khác.
- Giá có được cache sẵn ở Catalog không, hay Order phải query mỗi lần checkout?

**Ý tôi.** Tách 2 loại giao tiếp:

- **Query** (cần giá hiện tại) → interface trong `BuildingBlocks`, Catalog implement:

```csharp
// src/BuildingBlocks/PhoneStore.BuildingBlocks/Contracts/Catalog/IProductPricingContract.cs
public interface IProductPricingContract
{
    Task<ProductVariantPricing?> GetVariantPricingAsync(
        long productVariantId,
        CancellationToken cancellationToken = default);
}
```

- **Event** (một chiều, không cần kết quả) → dùng cho hoàn tồn kho khi hủy đơn (mục 3).

Tôi không tự quyết vì nó đụng tới `N2LHTBLTT-23` của bạn với `N2LHTBLTT-29` của bạn + Đạt.

---

#### 2. Transaction xuyên nhiều DbContext — 🔴 Chặn task 20

**Vấn đề.** Checkout cần ghi giỏ (schema `cart`) + ghi đơn (schema `ordering`) + trừ tồn kho (schema
`inventory`) trong **một transaction**. Nhưng kiến trúc hiện tại là 3 module, 3 `DbContext`, 3
`MigrationsHistoryTable` riêng — mỗi module xài chung một connection string.

**Cần bạn chốt (task `N2LHTBLTT-22` của bạn):**

- **TransactionScope** (System.Transactions) — hỗ trợ giao dịch phân tán, tự enlist nhiều `DbContext`
  trên cùng connection. Khả thi nhất với cấu trúc hiện tại.
- **Outbox pattern** — Cart/Order ghi event vào bảng outbox, worker xử lý Inventory sau. Bền vững hơn
  nhưng phức tạp hơn cho đồ án.
- **Một DbContext chung** cho các module giao dịch — phá vỡ cô lập schema, tôi không khuyến nghị.
- Có cần chống bán vượt tồn bằng khoá `SELECT ... FOR UPDATE` không, và khoá ở tầng nào?

**Ghi chú.** Với `TransactionScope` thì `Npgsql` cần bật `Enlist=true`, và transaction phải nâng lên
`ReadCommitted` trở lên (PostgreSQL không hỗ trợ transaction mức `Snapshot` trong `TransactionScope`).

---

#### 3. Event hoàn tồn khi hủy đơn — 🟡 Chặn task 26

**Vấn đề.** FR-09 yêu cầu hủy đơn phải hoàn lại tồn kho. `Order.Cancel()` hiện mới đổi trạng thái, chưa
phát event nào cho Inventory.

**Cần bạn chốt (task `N2LHTBLTT-29` của bạn + Đạt):**

- Tên event: `OrderCancelled`? `OrderCancelledEvent`? `OrderCancelledDomainEvent`?
- Đặt ở `BuildingBlocks` hay trong module `Order`? Nếu đặt trong module Order thì Inventory phải
  reference Order module — có mâu thuẫn nguyên tắc ranh giới không?
- Payload cần những gì? Tôi đề xuất: `OrderId`, `CustomerId`, và danh sách `ProductVariantId + Quantity`
  để Inventory hoàn kho. Cần thêm `UnitPrice` không?
- Inventory xử lý event ở đâu — MediatR notification handler trong module Inventory, hay service riêng?
- Cơ chế dispatch `DomainEvent`: `AggregateRoot.AddDomainEvent()` đã có sẵn trong `BuildingBlocks` nhưng
  chưa module nào dùng. Publish trước hay sau `SaveChanges`? Ai chịu trách nhiệm dispatch?

---

#### 4. Hai chỗ nhỏ cần xác nhận

**a) Migration cho schema `cart` và `ordering`.** Tôi đã viết xong mapping EF cho 5 bảng. Task
`N2LHTBLTT-13` của Đạt có gồm migration cho 5 schema không, hay mỗi module tự migration? Nếu Đạt
tạo thì tôi gửi mapping để sinh đúng, tránh trùng bảng.

**b) Lấy `userId` từ đâu.** `Cart.UserId` và `Order.CustomerId` cần id người đang đăng nhập. Identity hiện
trả token giả (`Guid.NewGuid()`), chưa có session thật (`N2LHTBLTT-8`, `9` của Trí). Lấy từ JWT claim
hay header? Trí chốt chưa?

---

### Nếu chưa có câu trả lời

Tôi đặt giả định sau để khỏi bị block, rồi có chốt thì sửa lại cũng được:

1. Tạo `IProductPricingContract` trong `BuildingBlocks/Contracts/Catalog/`, Catalog implement, Cart/Order
   chỉ biết interface.
2. Dùng `TransactionScope` cho checkout, chưa cần outbox.
3. Event hoàn tồn tạm đặt tên `OrderCancelled` trong `BuildingBlocks/Contracts/Order/`, payload gồm
   `OrderId` + danh sách `ProductVariantId`/`Quantity`.
4. `userId` lấy từ claim `sub` trong JWT, tạm bọc một extension method để dễ đổi sau.

**Cảm ơn bạn nha 🙏**

---

## Phần đã implement theo giả định (2026-10-26)

Đã làm tiếp để không bị block. Tất cả đều nằm sau interface trong `BuildingBlocks` nên đổi hướng sau này
chỉ cần sửa implementation, không phải sửa call-site.

### Giả định 1 — Query contract lấy giá

| Hạng mục | Chi tiết |
|---|---|
| Interface | `BuildingBlocks/Contracts/Catalog/IProductPricingContract.cs` → `GetVariantPricingAsync(long)` trả `ProductVariantPricing?` |
| Implement | `Catalog/Application/Services/ProductPricingContract.cs`, đăng ký scoped trong `AddCatalogModule` |
| Contract cũ | `AddToCartRequest` **đã bỏ** `UnitPrice`, `ProductName`, `Sku` — chỉ còn `ProductVariantId` + `Quantity` |
| Cart | `CartService.AddItemAsync` lấy giá từ contract, từ chối variant không tồn tại bằng `PRODUCT_VARIANT_NOT_FOUND` |
| Order | `OrderService.CheckoutAsync` lấy lại giá lúc checkout, không dùng giá cũ trong giỏ |
| Test | `CartServiceTests` — 3 test, gồm test "client đặt giá không ảnh hưởng giá server" |

### Giả định 2 — TransactionScope cho checkout

- `OrderService.CheckoutAsync` bọc toàn bộ trong `TransactionScope(TransactionScopeAsyncFlowOption.Enabled)`:
  tạo đơn (schema `ordering`) + `MarkAsConverted` giỏ (schema `cart`) nằm chung transaction, lỗi thì rollback
  cả hai.
- Chưa dùng Outbox, chưa đụng tới tồn kho (task của Đạt).

### Giả định 3 — Event hoàn tồn khi hủy đơn

| Hạng mục | Chi tiết |
|---|---|
| Event | `BuildingBlocks/Contracts/Order/OrderCancelled.cs` — `OrderId`, `CustomerId`, `OrderNumber`, `Items` (`ProductVariantId` + `Quantity`) |
| Phát ra | `Order.Cancel()` gọi `AddDomainEvent(...)` |
| Dispatch | `OrderService.CancelOrderAsync` dispatch **sau** `SaveChanges` qua `IDomainEventDispatcher` |
| Nhận | `Inventory/Application/EventHandlers/OrderCancelledHandler.cs` (MediatR `INotificationHandler`) |
| Hoàn tồn | `Inventory/Application/Services/InventoryContract.cs` implement `IInventoryContract.RestoreStockAsync` |
| Bảng tồn kho | Thêm entity `ProductStock` + bảng `inventory.product_stocks` (schema riêng của Inventory, không đụng `catalog.product_variants`) |
| Test | `OrderTests` — 2 test cho domain event, `InventoryTests` — 6 test cho `ProductStock` |

> Lưu ý: `OrderCancelled` implements cả `IDomainEvent` lẫn `INotification`, nên `InMemoryEventBus` có thể
> publish trực tiếp, và `AggregateRoot.AddDomainEvent` vẫn dùng được cho nhóm event.

### Giả định 4 — Lấy userId

- `BuildingBlocks/Application/ICurrentUser.cs` + `Infrastructure/Identity/HttpContextCurrentUser.cs`.
- Đọc claim `NameIdentifier`/`sub` trước, fallback header `X-User-Id` khi chưa có JWT thật.
- `CartController` và `OrdersController` dùng `RequireUserId()`, thiếu thì trả 401 `UNAUTHORIZED`.

### Sửa thêm ngoài dự kiến

| Vấn đề | Cách sửa |
|---|---|
| MediatR không scan handler của module | `AddBuildingBlocks` nay nhận marker type của cả 5 module thay vì chỉ `typeof(Program)` |
| `AppException` chưa được map ra HTTP response | Thêm `BuildingBlocks/Infrastructure/Web/AppExceptionMiddleware.cs`, trả `ErrorResponse` chuẩn |
| Repository thiếu `SaveChanges` | Thêm `SaveChangesAsync` vào `ICartRepository` / `IOrderRepository` |
| Ranh giới module chưa được test cho Inventory | Thêm `InventoryModule_ShouldNotReference_OtherModules`, `AllModules_ShouldOnlyReference_BuildingBlocks`, `BuildingBlocks_ShouldNotReference_AnyModule` |

### Trạng thái

- Build: 0 error, 0 warning.
- Test: **41 test xanh** (34 unit + 7 architecture).
- API chạy được, DI resolve OK, `/health` trả 200, `/api/v1/cart` trả 401 đúng khi chưa đăng nhập.
  (Phần query DB chưa test được vì Postgres chưa chạy trên máy.)

### Phần còn thiếu (cần DB + chốt hướng)

1. **Migration** — chưa có migration nào trong repo, schema `cart`, `ordering`, `inventory` chưa được tạo.
   Task `N2LHTBLTT-13` của Đạt.
2. **Seed dữ liệu** — cần `catalog.product_variants` + `inventory.product_stocks` để test checkout thật.
3. **Trừ tồn kho lúc checkout** — hiện checkout chưa trừ tồn kho (chờ Đạt xong `S2-09`).
4. **Authentication thật** — chờ Trí xong `N2LHTBLTT-8`, `N2LHTBLTT-9`.

