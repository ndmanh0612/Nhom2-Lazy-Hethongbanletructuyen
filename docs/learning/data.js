/* =================== DATA =================== */
const TASKS=[
 {k:'N2LHTBLTT-12',n:'Domain Entities, Repository và API contract cho Cart/Order',fr:'—',st:'PR #1',d:'Tạo khung nghiệp vụ lõi: AggregateRoot, entity Cart/CartItem/Order/OrderItem/OrderStatusHistory, repository pattern, và các contract để Order nói chuyện với Cart/Inventory/Catalog mà không import trực tiếp DbContext của họ.'},
 {k:'N2LHTBLTT-14',n:'Thiết lập Swagger, Validation và Global Exception Handling',fr:'—',st:'PR #1',d:'Hạ tầng dùng chung cho toàn hệ thống: Swagger UI ở root + XML doc, FluentValidation thay DataAnnotations, AppExceptionMiddleware chuẩn hoá mọi lỗi về một ErrorResponse JSON có code + requestId.'},
 {k:'N2LHTBLTT-19',n:'FR-06: API giỏ hàng (thêm, sửa số lượng, xóa)',fr:'FR-06',st:'PR #1',d:'4 endpoint giỏ hàng. Giá luôn lấy từ Catalog server-side, client không tự đặt giá. Giới hạn 10 đơn vị / sản phẩm, 50 sản phẩm khác nhau / giỏ.'},
 {k:'N2LHTBLTT-20',n:'FR-07: API Checkout COD, tạo đơn và tính tổng tiền',fr:'FR-07',st:'PR #1',d:'Chuyển giỏ thành đơn COD. Giá lấy lại từ Catalog, bọc trong TransactionScope để rollback được, sinh mã đơn ORD-yyyyMMdd-XXXXXX, miễn phí ship từ 10.000.000.'},
 {k:'N2LHTBLTT-25',n:'FR-08: API xem danh sách, chi tiết và lịch sử đơn hàng',fr:'FR-08',st:'PR #1',d:'Danh sách đơn có phân trang + lọc theo trạng thái / khoảng thời gian, trả về PagedResult. Chi tiết đơn kèm lịch sử trạng thái. Chặn xem đơn của người khác bằng ForbiddenException.'},
 {k:'N2LHTBLTT-26',n:'FR-09: API hủy đơn, kiểm tra trạng thái và hoàn tồn',fr:'FR-09',st:'PR #1',d:'Hủy đơn chỉ khi PENDING/CONFIRMED. Order phát domain event OrderCancelled, Event Bus đưa sang module Inventory, Inventory hoàn tồn kho — Order không biết Inventory tồn tại.'},
 {k:'N2LHTBLTT-39',n:'Kiểm thử tích hợp và regression test các API nghiệp vụ',fr:'—',st:'PR #2',d:'126 integration test E2E qua HTTP thật (WebApplicationFactory + PostgreSQL) + 35 unit + 7 architecture = 168 test. Mỗi lần chạy tạo database riêng rồi xoá. Sửa CI để GitHub Actions có PostgreSQL.'}
];

const TIMELINE=[
 {d:'25/10 — Sprint 5 mở',h:'N2LHTBLTT-12 · Khung domain Cart/Order',li:['AggregateRoot + BaseEntity (Id, CreatedAt, UpdatedAt, DomainEvents)','Cart (ACTIVE/CONVERTED/ABANDONED) + CartItem (MaxQuantityPerItem = 10)','Order + OrderItem + OrderStatusHistory + OrderItemDraft','ICartRepository / IOrderRepository + ICartContract / IInventoryContract / IProductPricingContract']},
 {d:'25/10 — Ngay sau đó',h:'N2LHTBLTT-14 · Hạ tầng chung',li:['Swagger UI ở root (/), OpenAPI ở /swagger/v1/swagger.json, nạp XML comments','FluentValidation thay DataAnnotations + 3 filter chuẩn hoá response','AppExceptionMiddleware bắt cả exception không lường trước → 500 JSON chuẩn']},
 {d:'25/10 — Buổi chiều',h:'N2LHTBLTT-19 · FR-06 Giỏ hàng',li:['GET /api/v1/cart · POST items · PUT items/{id} · DELETE items/{id}','Giá lấy server-side qua IProductPricingContract — không tin client','Bug sau này: ArgumentException không map được → 500, sửa thành ValidationException → 400']},
 {d:'25/10 — Buổi chiều',h:'N2LHTBLTT-20 · FR-07 Checkout COD',li:['POST /api/v1/orders/checkout trả 201','TransactionScope bọc toàn bộ: tạo đơn + đánh dấu giỏ CONVERTED','Miễn phí ship từ 10.000.000, phí ship chuẩn 30.000']},
 {d:'25/10 — Tối',h:'N2LHTBLTT-25 · FR-08 Xem đơn hàng',li:['GET /api/v1/orders (lọc status / fromDate / toDate, phân trang PagedResult)','GET /api/v1/orders/{id} kèm statusHistories','Ownership check: đơn của khách khác → 403 FORBIDDEN']},
 {d:'26/10 — Sáng',h:'N2LHTBLTT-26 · FR-09 Hủy đơn',li:['POST /api/v1/orders/{id}/cancel','Chỉ hủy được PENDING/CONFIRMED, nếu không → 409 INVALID_ORDER_STATUS','OrderCancelled event → InMemoryEventBus → MediatR → OrderCancelledHandler → RestoreStockAsync']},
 {d:'26/10 — Trưa',h:'N2LHTBLTT-39 · Bộ regression test',li:['9 file integration test, 126 test E2E qua HTTP thật','Mỗi lần chạy tạo database phonestore_test_<guid> rồi xoá — không đụng phonestore_db','Tổng 168 test: 35 unit + 7 architecture + 126 integration']},
 {d:'26/10 — 11:23',h:'Fix CI — PostgreSQL cho GitHub Actions',li:['PR #1 fail vì CI không có DB → 48/48 test chết vì lỗi Npgsql','Thêm service postgres:16-alpine + PHONESTORE_TEST_ADMIN_CONNECTION','Factory suy ra port từ connection string quản trị thay vì hardcode 5433']}
];

const FLOWS={
 cart:{title:'FR-06 · Giỏ hàng',steps:[
  {n:'GET /api/v1/cart',d:'CartService.GetOrCreateActiveCartAsync: tìm giỏ ACTIVE của user; không có thì tạo mới rồi lưu. Trả CartDto (id, status, totalQuantity, subtotal, items[]).'},
  {n:'POST /api/v1/cart/items',d:'AddToCartRequest chỉ có productVariantId + quantity — KHÔNG có giá. Service gọi IProductPricingContract.GetVariantPricingAsync để lấy giá/tên/SKU thật từ Catalog. Nếu không tìm thấy → 404 PRODUCT_VARIANT_NOT_FOUND. Sau đó cart.AddItem(...) — nếu sản phẩm đã có trong giỏ thì IncreaseQuantity, ngược lại tạo CartItem mới.'},
  {n:'PUT /api/v1/cart/items/{cartItemId}',d:'cart.UpdateItemQuantity → CartItem.SetQuantity(quantity): quantity <= 0 → ValidationException; quantity > 10 → ValidationException. Tìm item trong giỏ, không thấy → 404 CART_ITEM_NOT_FOUND.'},
  {n:'DELETE /api/v1/cart/items/{cartItemId}',d:'cart.RemoveItem(cartItem) — đồng thời kiểm tra item có thuộc giỏ không (EnsureItemBelongsToCart), nếu không → ValidationException.'},
  {n:'Xác thực',d:'Mọi endpoint cần userId. Lấy từ ICurrentUser: claim NameIdentifier/sub nếu đã đăng nhập, fallback header X-User-Id (tạm cho tới khi Identity xong task 8/9). Không có → 401 UNAUTHORIZED.'}
 ]},
 checkout:{title:'FR-07 · Checkout COD',steps:[
  {n:'Bước 1 — Lấy giỏ',d:'ICartContract.GetActiveCartAsync(customerId). Giỏ null hoặc rỗng → ValidationException "Giỏ hàng đang trống, không thể checkout."'},
  {n:'Bước 2 — Lấy giá lại',d:'Với từng item gọi IProductPricingContract.GetVariantPricingAsync. Không lấy được → ValidationException "Sản phẩm X không còn được bán." Kết quả là danh sách OrderItemDraft.'},
  {n:'Bước 3 — Sinh mã đơn',d:'GenerateOrderNumberAsync: ORD-{yyyyMMdd}-{6 ký tự hex ngẫu nhiên}, thử tối đa 5 lần nếu trùng, gọi ExistsOrderNumberAsync để kiểm tra.'},
  {n:'Bước 4 — Mở TransactionScope',d:'new TransactionScope(TransactionScopeAsyncFlowOption.Enabled) — bọc tạo đơn + chuyển giỏ. Nếu bất kỳ bước nào ném exception thì scope không Complete() → rollback tự động.'},
  {n:'Bước 5 — Tạo Order',d:'Order.Create(...) dựng entity với Status = PENDING, PaymentMethod = "COD", ghi lịch sử trạng thái đầu tiên, rồi RecalculateTotals(): Subtotal = tổng subtotal; ShippingFee = Subtotal >= 10.000.000 ? 0 : 30.000; TotalAmount = Subtotal + ShippingFee.'},
  {n:'Bước 6 — Lưu + chuyển giỏ',d:'orderRepository.AddAsync + SaveChangesAsync → rồi ICartContract.MarkAsConvertedAsync(cartId) để giỏ thành CONVERTED. Cuối cùng transactionScope.Complete() → commit. Trả 201 Created.'}
 ]},
 order:{title:'FR-08 · Xem đơn hàng',steps:[
  {n:'GET /api/v1/orders',d:'Nhận OrderFilterQuery: status?, fromDate?, toDate?, page = 1, pageSize = 20. Validator: page >= 1; pageSize trong [1,100]; fromDate <= toDate; status phải là enum hợp lệ.'},
  {n:'Phân trang',d:'Trả về PagedResult<OrderDto> — có items[], totalCount, page, pageSize, tổng số trang. Lọc theo CustomerId của user đang đăng nhập, không lọc theo user khác.'},
  {n:'GET /api/v1/orders/{orderId}',d:'Lấy order theo id. Nếu null → 404 ORDER_NOT_FOUND. Nếu order.CustomerId != customerId → ForbiddenException 403 FORBIDDEN (không phải 404 — tránh lộ sự tồn tại của đơn người khác).'},
  {n:'Chi tiết',d:'Trả OrderDetailDto = OrderDto + statusHistories[] (fromStatus, toStatus, note, changedAt) — lịch sử mọi lần chuyển trạng thái, sắp xếp theo ChangedAt.'}
 ]},
 cancel:{title:'FR-09 · Hủy đơn & hoàn tồn',steps:[
  {n:'Kiểm tra quyền sở hữu',d:'order = GetByIdAsync(orderId); null → 404 ORDER_NOT_FOUND. order.CustomerId != customerId → 403 FORBIDDEN.'},
  {n:'Kiểm tra trạng thái',d:'order.Cancel(reason): chỉ cho hủy khi Status là PENDING hoặc CONFIRMED. Đã SHIPPING/DELIVERED/CANCELLED → ConflictException với code INVALID_ORDER_STATUS → HTTP 409.'},
  {n:'Ghi lịch sử',d:'Cancel set CancelReason, CancelledAt = UtcNow, Status = CANCELLED, và thêm OrderStatusHistory.Create(this, previousStatus, CANCELLED, CancelReason).'},
  {n:'Phát domain event',d:'AddDomainEvent(new OrderCancelled(Id, CustomerId, OrderNumber, items.Select(i => new OrderCancelledItem(i.ProductVariantId, i.Quantity)))) — danh sách item được gom ngay tại thời điểm hủy.'},
  {n:'Lưu rồi mới dispatch',d:'SaveChangesAsync trước, sau đó _domainEventDispatcher.DispatchAsync(order.DomainEvents) rồi order.ClearDomainEvents(). Dispatch SAU commit để event không bị xử lý khi transaction rollback.'},
  {n:'Inventory nhận event',d:'DomainEventDispatcher → IEventBus.PublishAsync → InMemoryEventBus dùng Mediator.Publish (event là INotification) → OrderCancelledHandler (INotificationHandler<OrderCancelled>) → IInventoryContract.RestoreStockAsync(items) → ProductStock.Restore(quantity).'},
  {n:'Lưu ý quan trọng',d:'Hủy 2 lần: lần 2 Status đã là CANCELLED → 409, nên tồn kho KHÔNG bị hoàn 2 lần. Test Cancelling_an_order_twice_does_not_restore_stock_twice khẳng định điều này.'}
 ]}
};

const CHEAT={
 api:`<table>
<thead><tr><th>Method</th><th>Endpoint</th><th>Task</th><th>Request</th><th>Response</th><th>Lỗi chính</th></tr></thead>
<tbody>
<tr><td><span class="m m-get">GET</span></td><td><code>/api/v1/cart</code></td><td>19</td><td>—</td><td>200 CartDto</td><td>401</td></tr>
<tr><td><span class="m m-post">POST</span></td><td><code>/api/v1/cart/items</code></td><td>19</td><td><code>{productVariantId, quantity}</code></td><td>200 CartDto</td><td>400, 404</td></tr>
<tr><td><span class="m m-put">PUT</span></td><td><code>/api/v1/cart/items/{cartItemId}</code></td><td>19</td><td><code>{quantity}</code></td><td>200 CartDto</td><td>400, 404</td></tr>
<tr><td><span class="m m-del">DELETE</span></td><td><code>/api/v1/cart/items/{cartItemId}</code></td><td>19</td><td>—</td><td>200 CartDto</td><td>404</td></tr>
<tr><td><span class="m m-post">POST</span></td><td><code>/api/v1/orders/checkout</code></td><td>20</td><td><code>{receiverName, receiverPhone, shippingAddress, note?}</code></td><td>201 OrderDto</td><td>400, 401</td></tr>
<tr><td><span class="m m-get">GET</span></td><td><code>/api/v1/orders</code></td><td>25</td><td><code>?status=&fromDate=&toDate=&page=&pageSize=</code></td><td>200 PagedResult</td><td>400, 401</td></tr>
<tr><td><span class="m m-get">GET</span></td><td><code>/api/v1/orders/{orderId}</code></td><td>25</td><td>—</td><td>200 OrderDetailDto</td><td>403, 404</td></tr>
<tr><td><span class="m m-post">POST</span></td><td><code>/api/v1/orders/{orderId}/cancel</code></td><td>26</td><td><code>{reason}</code></td><td>200 OrderDetailDto</td><td>400, 403, 404, 409</td></tr>
</tbody></table>
<div class="note acc"><b>Query lọc đơn hàng:</b> <code class="inl">status</code> = PENDING | CONFIRMED | SHIPPING | DELIVERED | DELIVERY_FAILED | CANCELLED · <code class="inl">fromDate</code>/<code class="inl">toDate</code> ISO-8601 · <code class="inl">page</code> mặc định 1 · <code class="inl">pageSize</code> mặc định 20, tối đa 100.</div>`,
 err:`<table>
<thead><tr><th>HTTP</th><th>Code</th><th>Exception</th><th>Khi nào</th></tr></thead>
<tbody>
<tr><td>400</td><td><code>VALIDATION_ERROR</code></td><td>ValidationException</td><td>Dữ liệu sai format / vượt giới hạn. Có <code>fieldErrors</code> map theo field.</td></tr>
<tr><td>401</td><td><code>UNAUTHORIZED</code></td><td>UnauthorizedException</td><td>Thiếu userId (chưa có claim, chưa có header X-User-Id).</td></tr>
<tr><td>403</td><td><code>FORBIDDEN</code></td><td>ForbiddenException</td><td>Đã xác thực nhưng không phải chủ đơn.</td></tr>
<tr><td>404</td><td><code>NOT_FOUND</code></td><td>NotFoundException</td><td>Không tìm thấy dữ liệu.</td></tr>
<tr><td>404</td><td><code>ORDER_NOT_FOUND</code></td><td>NotFoundException</td><td>Không có đơn với id đó.</td></tr>
<tr><td>404</td><td><code>CART_ITEM_NOT_FOUND</code></td><td>NotFoundException</td><td>Item không có trong giỏ.</td></tr>
<tr><td>404</td><td><code>PRODUCT_VARIANT_NOT_FOUND</code></td><td>NotFoundException</td><td>Phiên bản sản phẩm không tồn tại / ngừng bán.</td></tr>
<tr><td>409</td><td><code>INVALID_ORDER_STATUS</code></td><td>ConflictException</td><td>Hủy đơn không hợp lệ về trạng thái; hoặc chuyển trạng thái từ DELIVERED/CANCELLED.</td></tr>
<tr><td>500</td><td><code>INTERNAL_SERVER_ERROR</code></td><td>Exception</td><td>Lỗi không lường trước — middleware bắt, log, trả JSON chuẩn thay vì HTML.</td></tr>
</tbody></table>
<div class="note"><b>Body lỗi chuẩn:</b> <code class="inl">{ code, message, fieldErrors?, requestId, timestamp }</code> — không bao giờ trả HTML trần.</div>`,
 const:`<table>
<thead><tr><th>Hằng số</th><th>Giá trị</th><th>Ở đâu</th><th>Ý nghĩa</th></tr></thead>
<tbody>
<tr><td><code>CartItem.MaxQuantityPerItem</code></td><td><code>10</code></td><td>CartItem.cs</td><td>Tối đa 10 đơn vị mỗi sản phẩm trong giỏ.</td></tr>
<tr><td><code>Cart.MaxItemsPerCart</code></td><td><code>50</code></td><td>Cart.cs</td><td>Tối đa 50 sản phẩm khác nhau mỗi giỏ.</td></tr>
<tr><td><code>Order.FreeShippingThreshold</code></td><td><code>10 000 000</code></td><td>Order.cs</td><td>Từ mức này (10 triệu) miễn phí ship.</td></tr>
<tr><td><code>Order.StandardShippingFee</code></td><td><code>30 000</code></td><td>Order.cs</td><td>Phí ship khi chưa đủ mức miễn phí.</td></tr>
<tr><td><code>Order.PaymentMethod</code></td><td><code>"COD"</code></td><td>Order.cs</td><td>Hiện chỉ hỗ trợ COD.</td></tr>
<tr><td><code>OrderStatus</code></td><td>PENDING, CONFIRMED, SHIPPING, DELIVERED, DELIVERY_FAILED, CANCELLED</td><td>OrderStatus.cs</td><td>Vòng đời đơn hàng.</td></tr>
<tr><td><code>CartStatus</code></td><td>ACTIVE, CONVERTED, ABANDONED</td><td>CartStatus.cs</td><td>Vòng đời giỏ hàng.</td></tr>
<tr><td><code>PageSize</code> mặc định</td><td><code>20</code> (tối đa 100)</td><td>OrderFilterQuery</td><td>Phân trang danh sách đơn.</td></tr>
<tr><td>Port Postgres (local)</td><td><code>5433</code></td><td>docker-compose.yml</td><td>Đổi từ 5432 để tránh đụng PG cài sẵn.</td></tr>
<tr><td>Port Postgres (CI)</td><td><code>5432</code></td><td>ci.yml</td><td>GitHub Actions service dùng port chuẩn.</td></tr>
<tr><td>Port API</td><td><code>5122</code> / https <code>7168</code></td><td>launchSettings.json</td><td>Swagger ở root.</td></tr>
</tbody></table>`,
 file:`<table>
<thead><tr><th>File</th><th>Trách nhiệm</th></tr></thead>
<tbody>
<tr><td><code>src/Modules/Cart/.../Domain/Entities/Cart.cs</code></td><td>Luật giỏ hàng: AddItem, UpdateItemQuantity, RemoveItem, MarkAsConverted, MarkAsAbandoned.</td></tr>
<tr><td><code>src/Modules/Cart/.../Domain/Entities/CartItem.cs</code></td><td>Luật item: MaxQuantityPerItem, SetQuantity, IncreaseQuantity, Subtotal.</td></tr>
<tr><td><code>src/Modules/Cart/.../Application/Services/CartService.cs</code></td><td>Điều phối: lấy giá từ Catalog, tìm giỏ ACTIVE, map sang DTO.</td></tr>
<tr><td><code>src/Modules/Cart/.../Application/Services/CartContract.cs</code></td><td>Implement ICartContract — Order dùng để đọc giỏ + đánh dấu CONVERTED.</td></tr>
<tr><td><code>src/Modules/Cart/.../CartModuleRegistration.cs</code></td><td>DI + CartController (4 endpoint FR-06).</td></tr>
<tr><td><code>src/Modules/Order/.../Domain/Entities/Order.cs</code></td><td>Luật đơn: Create, ChangeStatus, Cancel, RecalculateTotals, phát OrderCancelled.</td></tr>
<tr><td><code>src/Modules/Order/.../Domain/Entities/OrderStatusHistory.cs</code></td><td>Lịch sử chuyển trạng thái (from, to, note, changedAt).</td></tr>
<tr><td><code>src/Modules/Order/.../Domain/Entities/OrderItemDraft.cs</code></td><td>Draft item dùng lúc checkout (chưa có Id).</td></tr>
<tr><td><code>src/Modules/Order/.../Application/Services/OrderService.cs</code></td><td>CheckoutAsync, GetOrdersAsync, GetOrderAsync, CancelOrderAsync, sinh mã đơn.</td></tr>
<tr><td><code>src/Modules/Order/.../Application/Validators/OrderValidators.cs</code></td><td>Validator cho Checkout, Cancel, OrderFilterQuery.</td></tr>
<tr><td><code>src/Modules/Order/.../OrderModuleRegistration.cs</code></td><td>DI + OrdersController (4 endpoint FR-07/08/09).</td></tr>
<tr><td><code>src/BuildingBlocks/.../Infrastructure/Web/AppExceptionMiddleware.cs</code></td><td>Chuyển exception → ErrorResponse JSON.</td></tr>
<tr><td><code>src/BuildingBlocks/.../Infrastructure/Web/SwaggerRegistration.cs</code></td><td>Swagger UI ở root, XML comments, Bearer scheme.</td></tr>
<tr><td><code>src/BuildingBlocks/.../Common/ApiResponse.cs</code></td><td>ApiResponse&lt;T&gt; + ErrorResponse.</td></tr>
<tr><td><code>src/BuildingBlocks/.../Contracts/Order/OrderCancelled.cs</code></td><td>Domain event hủy đơn + OrderCancelledItem.</td></tr>
<tr><td><code>src/Host/PhoneStore.Api/Seeding/DatabaseSeeder.cs</code></td><td>Seed user / catalog / stock cho local, idempotent.</td></tr>
<tr><td><code>tests/PhoneStore.IntegrationTests/PhoneStoreApiFactory.cs</code></td><td>WebApplicationFactory + tạo/xoá database test random.</td></tr>
<tr><td><code>tests/PhoneStore.IntegrationTests/ApiTestHelper.cs</code></td><td>Helper dùng chung: GetDataAsync, SendAsync, CheckoutAsync, AssertErrorEnvelope.</td></tr>
<tr><td><code>.github/workflows/ci.yml</code></td><td>CI: postgres service + 3 bước test.</td></tr>
</tbody></table>`,
 git:`<table>
<thead><tr><th>Việc</th><th>Lệnh / Quy tắc</th></tr></thead>
<tbody>
<tr><td>Tạo branch</td><td><code>git checkout -b feature/N2LHTBLTT-&lt;số&gt;-&lt;tên&gt;</code></td></tr>
<tr><td>Commit</td><td><code>&lt;type&gt;(&lt;scope&gt;): &lt;mô tả&gt; [N2LHTBLTT-&lt;số&gt;]</code></td></tr>
<tr><td>Type cho phép</td><td>feat · fix · refactor · test · docs · chore</td></tr>
<tr><td>Push</td><td><code>git push origin &lt;branch&gt;</code></td></tr>
<tr><td>PR target</td><td>Luôn vào <code>develop</code> (trừ hotfix → main)</td></tr>
<tr><td>Merge</td><td>Squash and Merge hoặc Rebase</td></tr>
<tr><td>Reviewer</td><td>Ít nhất 1 thành viên khác — không tự review PR của mình</td></tr>
<tr><td>Kiểm tra trước push</td><td><code>dotnet build PhoneStore.slnx</code> + <code>dotnet test PhoneStore.slnx</code></td></tr>
</tbody></table>
<div class="note ok"><b>PR của bạn hiện tại:</b> PR #1 (Cart/Order + Swagger + test) và PR #2 (task 39, regression test) — PR #2 stack trên PR #1, merge #1 trước rồi rebase #2.</div>`
};
