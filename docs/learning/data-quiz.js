/* =================== QUIZ =================== */
const QUIZ=[
 {t:'12',q:'Vì sao Order module không được inject CartDbContext trực tiếp?',o:['Để tránh circular reference giữa các project','Để tuân thủ ranh giới module — giao tiếp qua contract, không qua DbContext','Vì CartDbContext là internal','Vì EF Core không cho phép dùng chung DbContext'],c:1,e:'<b>Ranh giới module.</b> Order gọi <code>ICartContract</code> / <code>IInventoryContract</code> / <code>IProductPricingContract</code> do module khác implement. Đây là điều <code>PhoneStore.ArchitectureTests</code> kiểm tra (7 test).'},
 {t:'12',q:'AggregateRoot khác BaseEntity ở chỗ nào?',o:['AggregateRoot không có Id','AggregateRoot có thêm DomainEvents, AddDomainEvent, ClearDomainEvents','AggregateRoot có thêm CreatedAt/UpdatedAt','AggregateRoot là abstract, BaseEntity là interface'],c:1,e:'<code>BaseEntity</code>: Id, CreatedAt, UpdatedAt, Touch(). <code>AggregateRoot</code> kế thừa và bổ sung cơ chế domain event — chỉ aggregate root được phát event.'},
 {t:'12',q:'CartItem.MaxQuantityPerItem bằng bao nhiêu?',o:['5','10','20','50'],c:1,e:'<code>MaxQuantityPerItem = 10</code> — mỗi sản phẩm tối đa 10 đơn vị. Còn <code>Cart.MaxItemsPerCart = 50</code> là số sản phẩm khác nhau tối đa trong 1 giỏ.'},
 {t:'12',q:'TryReserveStockAsync trả về gì và dùng để làm gì?',o:['Task trả về Order, dùng tạo đơn','Task&lt;bool&gt; — false nếu tồn không đủ, dùng chống bán vượt tồn','Task&lt;int&gt; trả về số lượng còn lại','Task không trả về gì'],c:1,e:'Trả <code>bool</code>: <code>false</code> khi tồn không đủ hoặc thiếu bản ghi tồn kho. Đây là cơ chế chống bán vượt tồn (task 21/22 của Đạt/Mạnh).'},
 {t:'14',q:'Exception nào KHÔNG kế thừa AppException nên AppExceptionMiddleware không map được?',o:['NotFoundException','ConflictException','ArgumentException','ValidationException'],c:2,e:'<code>ArgumentException</code> là exception của .NET, không kế thừa AppException → rơi vào <code>catch (Exception)</code> → 500 thay vì 400.<br><b>Đây chính là bug bạn đã gặp</b> ở CartItem.SetQuantity, sửa bằng cách đổi sang ValidationException.'},
 {t:'14',q:'Trong ErrorResponse, fieldErrors xuất hiện khi nào?',o:['Mọi lỗi đều có','Chỉ khi exception là ValidationException','Chỉ khi HTTP 400','Chỉ khi HTTP 500'],c:1,e:'Middleware kiểm tra <code>exception is ValidationException</code> thì mới gắn <code>response.FieldErrors = validationException.Errors</code> — map field → string[].'},
 {t:'14',q:'Swagger UI ở môi trường Development nằm ở đường dẫn nào?',o:['/swagger','/swagger/v1/swagger.json','/ (root)','/api/docs'],c:2,e:'UI ở <b>root</b> <code>/</code> (do <code>c.RoutePrefix = string.Empty</code>), còn OpenAPI JSON ở <code>/swagger/v1/swagger.json</code>.'},
 {t:'14',q:'Vì sao bỏ DataAnnotations khỏi DTO?',o:['FluentValidation nhanh hơn','Tránh validate 2 lần (DataAnnotations ở model binding + FluentValidation ở filter)','DataAnnotations không hỗ trợ tiếng Việt','Giảm rò rỉ dependency'],c:1,e:'Cả hai cùng chạy sẽ sinh 2 bộ lỗi. Giờ DTO sạch attribute, validator nằm riêng trong <code>Application/Validators/</code> của từng module.'},
 {t:'19',q:'AddToCartRequest gồm những field nào?',o:['productVariantId, quantity, price','productVariantId, quantity, name','productVariantId, quantity','id, productVariantId, quantity'],c:2,e:'Chỉ <code>productVariantId</code> + <code>quantity</code> (mặc định 1). Giá, tên, SKU do server lấy từ Catalog — không bao giờ tin giá client.'},
 {t:'19',q:'Thêm sản phẩm đã có trong giỏ thì xử lý thế nào?',o:['Báo lỗi trùng sản phẩm','Tạo dòng mới, giữ cả 2 dòng','Cộng dồn số lượng vào dòng cũ (IncreaseQuantity)','Xoá dòng cũ rồi tạo dòng mới'],c:2,e:'<code>Cart.AddItem</code> tìm item cùng <code>ProductVariantId</code> → <code>existingItem.IncreaseQuantity(quantity)</code>.'},
 {t:'19',q:'Giỏ hàng đã CHECKOUT xong thì trạng thái là gì, và có dùng lại được không?',o:['ACTIVE, dùng lại được','CONVERTED, không dùng lại — lần checkout sau tạo giỏ mới','ABANDONED, bị xoá','PENDING, chờ xác nhận'],c:1,e:'<code>MarkAsConverted()</code> đặt Status = CONVERTED. <code>ICartContract.GetActiveCartAsync</code> chỉ trả giỏ ACTIVE nên lần sau tạo giỏ mới — test <code>Cancelled_cart_is_not_reused_for_the_next_order</code> khẳng định.'},
 {t:'20',q:'Phí ship khi Subtotal = 5.000.000 là bao nhiêu?',o:['0','30.000','10.000.000','5% tổng'],c:1,e:'<code>ShippingFee = Subtotal >= 10 000 000 ? 0 : 30 000</code>. 5 triệu < 10 triệu → phí ship 30.000. Miễn phí ship từ 10 triệu trở lên.'},
 {t:'20',q:'Mã đơn hàng có dạng nào?',o:['ORD-20261026-A3F91C','ORDER-000123','20261026-000123','ORD-A3F91C'],c:0,e:'<code>ORD-{yyyyMMdd}-{6 ký tự hex}</code> → <code>ORD-20261026-A3F91C</code>. Thử tối đa 5 lần nếu trùng.'},
 {t:'20',q:'Vì sao checkout bọc TransactionScope?',o:['Để tạo đơn nhanh hơn','Để tạo đơn và chuyển trạng thái giỏ là all-or-nothing — lỗi thì rollback cả hai','Để khoá bảng đơn hàng','Để đảm bảo mã đơn duy nhất'],c:1,e:'Tạo đơn (schema ordering) + đánh dấu giỏ CONVERTED (schema cart) phải cùng thành công. Không có scope, một trong hai lỗi sẽ để lại dữ liệu lệch.'},
 {t:'25',q:'Khách hàng A xem đơn của khách hàng B sẽ nhận mã gì?',o:['404 NOT_FOUND','403 FORBIDDEN','401 UNAUTHORIZED','200 nhưng data rỗng'],c:1,e:'<b>403 FORBIDDEN</b> — đã xác thực nhưng không sở hữu đơn đó. Ẩn thành 404 sẽ gây nhầm lẫn "đơn không tồn tại".'},
 {t:'25',q:'Mặc định pageSize của API danh sách đơn là bao nhiêu, tối đa là bao nhiêu?',o:['10 / 50','20 / 100','50 / 200','20 / 50'],c:1,e:'<code>OrderFilterQuery</code>: <code>Page = 1, PageSize = 20</code>. Validator: <code>PageSize</code> trong khoảng [1, 100].'},
 {t:'25',q:'OrderDetailDto có thêm gì so với OrderDto?',o:['statusHistories[]','shippingFee','items[]','cancelReason'],c:0,e:'<code>OrderDetailDto</code> = OrderDto + <code>statusHistories[]</code> (fromStatus, toStatus, note, changedAt). Danh sách dùng OrderDto nhẹ, chi tiết mới có lịch sử trạng thái.'},
 {t:'26',q:'Điều kiện nào để hủy đơn?',o:['Chỉ khi PENDING','Khi PENDING hoặc CONFIRMED','Khi chưa DELIVERED','Bất kỳ lúc nào, trừ đã thanh toán'],c:1,e:'<code>Order.Cancel</code> ném ConflictException nếu Status không phải PENDING hoặc CONFIRMED → HTTP 409 INVALID_ORDER_STATUS.'},
 {t:'26',q:'Vì sao dispatch event sau SaveChangesAsync?',o:['Để event chỉ xử lý khi transaction đã commit, tránh hoàn tồn kho khi đơn bị rollback','Để tăng hiệu năng','Vì MediatR bắt buộc','Để log đầy đủ hơn'],c:0,e:'Dispatch trước mà rollback sau sẽ khiến tồn kho bị hoàn trong khi đơn vẫn còn — dữ liệu lệch. Thứ tự: Save → Dispatch → ClearDomainEvents.'},
 {t:'26',q:'Module nào "nghe" event OrderCancelled?',o:['Order','Cart','Inventory','Catalog'],c:2,e:'<code>OrderCancelledHandler</code> nằm trong module Inventory, implement <code>INotificationHandler&lt;OrderCancelled&gt;</code> → gọi <code>IInventoryContract.RestoreStockAsync</code>.'},
 {t:'39',q:'Mỗi lần chạy integration test, database test được tạo/xoá thế nào?',o:['Dùng chung phonestore_db và xoá bảng sau mỗi test','Tạo DB tên random phonestore_test_<guid> rồi DROP sau khi xong','Dùng InMemory database provider','Tạo DB nếu chưa có, giữ lại để lần sau dùng'],c:1,e:'Nhờ vậy <b>không đụng</b> vào <code>phonestore_db</code> và các test chạy song song (về mặt DB) không đụng nhau.'},
 {t:'39',q:'Vì sao phải DisableTestParallelization?',o:['Vì PostgreSQL chậm','Vì connection string được override bằng biến môi trường toàn cục — chạy song song sẽ trỏ nhầm DB','Vì xUnit có bug','Vì test dùng chung static state'],c:1,e:'<code>[assembly: CollectionBehavior(DisableTestParallelization = true)]</code>. Hệ quả: không chạy 2 tiến trình <code>dotnet test</code> cùng lúc.'},
 {t:'39',q:'CI fail vì lý do gì ở lần chạy đầu?',o:['Code sai logic','GitHub Actions không có PostgreSQL','Thiếu package NuGet','Sai port của app'],c:1,e:'48/48 integration test chết vì lỗi <code>NpgsqlConnector.ConnectAsync</code>. Fix: thêm <code>services.postgres</code> + <code>PHONESTORE_TEST_ADMIN_CONNECTION</code> trỏ port 5432.'},
 {t:'39',q:'Port PostgreSQL dùng ở local và ở CI lần lượt là?',o:['5432 / 5433','5433 / 5432','5433 / 5433','5432 / 5432'],c:1,e:'Local <b>5433</b> (docker-compose đổi để tránh đụng PG cài sẵn), CI <b>5432</b> (service chuẩn). Vì vậy connection string phải suy ra động, không hardcode.'},
 {t:'39',q:'Tổng số test hiện tại của repo là bao nhiêu?',o:['89','126','168','201'],c:2,e:'168 = 35 unit + 7 architecture + 126 integration. (89 là con số cũ trước khi bộ regression 126 test được thêm.)'},
 {t:'14',q:'ApiResponse thành công có field nào?',o:['success, message, data, timestamp','code, message, requestId','status, data','data, error'],c:0,e:'<code>ApiResponse&lt;T&gt;</code>: <code>{ success, message, data, timestamp }</code>. <code>ErrorResponse</code> mới có <code>code</code> + <code>requestId</code>.'},
 {t:'12',q:'OrderItemDraft dùng để làm gì?',o:['Lưu trong DB','Dựng Order lúc checkout trước khi có Id','Trả về cho client','Lưu lịch sử thay đổi'],c:1,e:'Record tạm chứa productVariantId, productName, sku, price, quantity — đầu vào của <code>Order.Create</code> trước khi entity có Id thật.'}
];

/* =================== CODE LAB =================== */
const LAB=[
 {id:'l1',f:'src/Modules/Cart/.../Domain/Entities/CartItem.cs',t:'Task 19 — Giới hạn số lượng',code:`public void SetQuantity(int quantity)
{
    if (quantity <= 0)
    {
        throw new ValidationException(
            "quantity",
            "Số lượng phải lớn hơn 0.");
    }

    if (quantity > MaxQuantityPerItem)
    {
        throw new [A1](
            "quantity",
            $"Mỗi sản phẩm chỉ được thêm tối đa {MaxQuantityPerItem} đơn vị.");
    }

    Quantity = quantity;
    Touch();
}`,blanks:[['A1','ValidationException','ArgumentException','InvalidOperationException','NotFoundException']],why:'Phải là <code>ValidationException</code> (kế thừa AppException) để middleware map thành <b>400 VALIDATION_ERROR</b> kèm fieldErrors. Nếu dùng <code>ArgumentException</code> → 500. Đây chính là bug bạn đã sửa trong PR #2.'},
 {id:'l2',f:'src/Modules/Order/.../Domain/Entities/Order.cs',t:'Task 20 — Công thức tổng tiền',code:`public void RecalculateTotals()
{
    Subtotal = _items.Sum(item => item.Subtotal);
    ShippingFee = Subtotal >= [A1] ? 0 : [A2];
    TotalAmount = Subtotal + ShippingFee;
}`,blanks:[['A1','FreeShippingThreshold','StandardShippingFee','TotalAmount','ShippingFee'],['A2','StandardShippingFee','FreeShippingThreshold','Subtotal','ShippingFee']],why:'<code>FreeShippingThreshold = 10 000 000</code> (mức miễn phí ship) và <code>StandardShippingFee = 30 000</code> (phí ship khi chưa đủ mức).'},
 {id:'l3',f:'src/Modules/Order/.../Domain/Entities/Order.cs',t:'Task 26 — Chặn hủy sai trạng thái',code:`public void Cancel(string reason)
{
    if (Status != OrderStatus.PENDING && Status != OrderStatus.[A1])
    {
        throw new ConflictException(
            $"Đơn hàng đang ở trạng thái {Status} không thể hủy.",
            "INVALID_ORDER_STATUS");
    }

    var previousStatus = Status;
    Status = OrderStatus.CANCELLED;
    CancelReason = reason.Trim();
    CancelledAt = DateTimeOffset.UtcNow;
    _statusHistories.Add(OrderStatusHistory.Create(this, previousStatus, OrderStatus.CANCELLED, CancelReason));
    Touch();
}`,blanks:[['A1','CONFIRMED','CANCELLED','SHIPPING','DELIVERED']],why:'Hủy được khi PENDING hoặc <b>CONFIRMED</b>. Nếu kết thúc bằng CANCELLED thì lần hủy thứ 2 cũng qua → tồn kho bị hoàn 2 lần.'},
 {id:'l4',f:'src/Modules/Order/.../Application/Services/OrderService.cs',t:'Task 20 — Transaction + chuyển giỏ',code:`using var transactionScope = new TransactionScope(
    TransactionScopeAsyncFlowOption.Enabled);

var order = OrderEntity.Create(
    orderNumber, customerId,
    request.ReceiverName, request.ReceiverPhone,
    request.ShippingAddress, request.Note,
    itemDrafts);

await _orderRepository.AddAsync(order, cancellationToken);
await _orderRepository.SaveChangesAsync(cancellationToken);

await _cartContract.MarkAsConvertedAsync(cart.CartId, cancellationToken);

transactionScope.[A1]();

return Result.Success(ToDto(order));`,blanks:[['A1','Complete','Commit','Save','Dispose']],why:'<code>Complete()</code> mới commit. Nếu không gọi, scope dispose mà không commit → rollback toàn bộ, giỏ không bị chuyển CONVERTED.'},
 {id:'l5',f:'src/Modules/Order/.../Application/Services/OrderService.cs',t:'Task 26 — Thứ tự dispatch event',code:`order.Cancel(request.Reason);
await _orderRepository.SaveChangesAsync(cancellationToken);

// Dispatch event sau khi đã commit dữ liệu đơn hàng.
await _domainEventDispatcher.[A1](order.DomainEvents, cancellationToken);
order.ClearDomainEvents();

return Result.Success(ToDetailDto(order));`,blanks:[['A1','DispatchAsync','PublishAsync','HandleAsync','RaiseAsync']],why:'<code>DispatchAsync</code> — đúng tên hàm của <code>IDomainEventDispatcher</code>. Phải dispatch <b>sau</b> SaveChanges để event chỉ chạy khi transaction đã commit.'},
 {id:'l6',f:'src/BuildingBlocks/.../Infrastructure/Web/AppExceptionMiddleware.cs',t:'Task 14 — Map exception sang HTTP',code:`catch (AppException exception)
{
    var response = new ErrorResponse
    {
        Code = exception.Code,
        Message = exception.Message,
        RequestId = context.TraceIdentifier,
        Timestamp = DateTimeOffset.UtcNow
    };

    if (exception is [A1] validationException)
    {
        response.FieldErrors = validationException.Errors;
    }

    context.Response.StatusCode = (int)exception.[A2];
    await context.Response.WriteAsJsonAsync(response);
}`,blanks:[['A1','ValidationException','ArgumentException','NotFoundException','ConflictException'],['A2','StatusCode','Code','HttpStatus','ErrorCode']],why:'Chỉ <code>ValidationException</code> có <code>Errors</code> (field → string[]). <code>StatusCode</code> là property <code>HttpStatusCode</code> trên AppException, được ép về int để gán vào Response.'},
 {id:'l7',f:'src/BuildingBlocks/.../Infrastructure/Identity/HttpContextCurrentUser.cs',t:'Task 19 — Lấy userId',code:`var principal = _httpContextAccessor.HttpContext?.User;
if (principal?.Identity?.IsAuthenticated == true)
{
    var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? principal.FindFirstValue("[A1]");
    if (long.TryParse(subject, out var userId))
    {
        return userId;
    }
}

// Fallback tạm thời cho môi trường chưa có JWT thật.
var header = _httpContextAccessor.HttpContext?.Request.Headers["[A2]"].ToString();
if (long.TryParse(header, out var headerUserId))
{
    return headerUserId;
}`,blanks:[['A1','sub','userId','uid','id'],['A2','X-User-Id','User-Id','X-UserId','UserId']],why:'Claim JWT chuẩn là <code>sub</code>; header tạm thời là <code>X-User-Id</code>. Khi Identity xong task 8/9, toàn bộ integration test phải chuyển sang dùng token thật.'},
 {id:'l8',f:'src/Modules/Cart/.../Application/Services/CartService.cs',t:'Task 19 — Giá lấy server-side',code:`public async Task<Result<CartDto>> AddItemAsync(
    long userId, AddToCartRequest request, CancellationToken ct = default)
{
    // Giá và thông tin sản phẩm luôn lấy từ Catalog, không tin vào dữ liệu client.
    var pricing = await _pricingContract.[A1](request.ProductVariantId, ct);
    if (pricing is null)
    {
        throw new NotFoundException(
            "Không tìm thấy phiên bản sản phẩm hoặc sản phẩm đã ngừng bán.",
            "PRODUCT_VARIANT_NOT_FOUND");
    }

    var cart = await GetOrCreateActiveCartAsync(userId, ct);
    cart.AddItem(pricing.ProductVariantId, pricing.ProductName,
                 pricing.Sku, pricing.Price, request.Quantity);`,blanks:[['A1','GetVariantPricingAsync','GetPriceAsync','GetProductAsync','GetPricingAsync']],why:'<code>GetVariantPricingAsync</code> — trả về giá/tên/SKU của <b>phiên bản</b> sản phẩm. Giá lấy từ Catalog chứ không lấy từ request của client.'},
 {id:'l9',f:'src/Modules/Cart/.../Domain/Entities/Cart.cs',t:'Task 19 — Giới hạn số loại sản phẩm',code:`public const int MaxItemsPerCart = [A1];

public CartItem AddItem(long productVariantId, string productName,
                        string sku, long unitPrice, int quantity)
{
    if (Status != CartStatus.[A2])
    {
        throw new ValidationException("cart",
            "Chỉ giỏ hàng đang hoạt động mới được thêm sản phẩm.");
    }

    if (_items.Count >= MaxItemsPerCart && _items.All(i => i.ProductVariantId != productVariantId))
    {
        throw new ValidationException("cart",
            $"Giỏ hàng không được vượt quá {MaxItemsPerCart} sản phẩm khác nhau.");
    }`,blanks:[['A1','50','10','20','100'],['A2','ACTIVE','CONVERTED','PENDING','ABANDONED']],why:'Tối đa <b>50</b> sản phẩm khác nhau mỗi giỏ; chỉ giỏ <b>ACTIVE</b> mới thêm được. (Điều kiện <code>All(... != ...)</code> nghĩa là chỉ chặn khi thêm sản phẩm MỚI, không chặn cộng dồn sản phẩm đã có.)'},
 {id:'l10',f:'src/Modules/Order/.../Domain/Entities/Order.cs',t:'Task 26 — Phát domain event',code:`// Phát event để Inventory hoàn trả tồn kho (FR-09).
AddDomainEvent(new [A1](
    Id,
    CustomerId,
    OrderNumber,
    _items.Select(item => new [A2](item.ProductVariantId, item.Quantity)).ToList()));`,blanks:[['A1','OrderCancelled','OrderStatusChanged','StockRestored','OrderCreated'],['A2','OrderCancelledItem','OrderItem','StockChangeItem','OrderItemDraft']],why:'Event là <code>OrderCancelled</code>, mỗi item là <code>OrderCancelledItem</code> (ProductVariantId + Quantity) — đúng kiểu mà <code>OrderCancelledHandler</code> của module Inventory nhận.'}
];

/* =================== CHECKLIST =================== */
const CHECKS=[
 {t:'12',h:'Task 12 — Domain, Repository, Contract',li:[
  'Tôi phân biệt được BaseEntity và AggregateRoot, và giải thích được vì sao chỉ aggregate root mới phát event',
  'Tôi liệt kê được 5 entity của Cart/Order mà không cần nhìn code',
  'Tôi giải thích được vì sao Order không được inject CartDbContext (ranh giới module)',
  'Tôi nêu được 3 contract: ICartContract, IInventoryContract, IProductPricingContract và ai implement',
  'Tôi vẽ được luồng Order.Create → OrderItem.Create → RecalculateTotals',
  'Tôi biết OrderItemDraft khác OrderItem và dùng khi nào']},
 {t:'14',h:'Task 14 — Swagger, Validation, Exception',li:[
  'Tôi liệt kê được 5 loại AppException và HTTP status tương ứng',
  'Tôi giải thích được vì sao ArgumentException gây 500 còn ValidationException gây 400',
  'Tôi biết fieldErrors xuất hiện khi nào và dùng để làm gì',
  'Tôi biết Swagger UI ở root, OpenAPI JSON ở đâu, XML doc lấy từ đâu',
  'Tôi giải thích được lý do bỏ DataAnnotations',
  'Tôi phân biệt được ApiResponse và ErrorResponse (field nào của mỗi loại)']},
 {t:'19',h:'Task 19 — FR-06 Giỏ hàng',li:[
  'Tôi liệt kê được 4 endpoint giỏ hàng và method tương ứng',
  'Tôi giải thích được vì sao AddToCartRequest không có field giá',
  'Tôi nhớ 2 giới hạn: 10 đơn vị / sản phẩm và 50 sản phẩm khác nhau / giỏ',
  'Tôi mô tả được hành vi khi thêm sản phẩm đã có trong giỏ',
  'Tôi phân biệt được ACTIVE / CONVERTED / ABANDONED',
  'Tôi kể được câu chuyện bug 500 → 400 và nguyên nhân gốc rễ']},
 {t:'20',h:'Task 20 — FR-07 Checkout COD',li:[
  'Tôi kể được 6 bước của CheckoutAsync theo đúng thứ tự',
  'Tôi tính được tổng tiền cho một đơn cụ thể (tự chọn số tiền)',
  'Tôi giải thích được vì sao lấy lại giá từ Catalog thay vì dùng giá trong giỏ',
  'Tôi giải thích được TransactionScope bảo vệ điều gì và khi nào rollback',
  'Tôi viết được format mã đơn hàng',
  'Tôi biết response trả 201 chứ không phải 200, và vì sao']},
 {t:'25',h:'Task 25 — FR-08 Danh sách & chi tiết đơn',li:[
  'Tôi liệt kê được các tham số lọc và mặc định của chúng',
  'Tôi giải thích được vì sao xem đơn người khác trả 403 chứ không phải 404',
  'Tôi phân biệt được OrderDto và OrderDetailDto',
  'Tôi biết PagedResult trả về những field nào',
  'Tôi nêu được các quy tắc validation của OrderFilterQuery']},
 {t:'26',h:'Task 26 — FR-09 Hủy đơn & hoàn tồn',li:[
  'Tôi kể được chuỗi 6 bước từ hủy đơn đến hoàn tồn kho',
  'Tôi giải thích được vì sao dispatch event sau SaveChanges',
  'Tôi biết event tên gì, do ai phát, ai nhận, nhận xong làm gì',
  'Tôi giải thích được vì sao hủy 2 lần không hoàn tồn 2 lần',
  'Tôi biết HTTP status và error code khi hủy sai trạng thái']},
 {t:'39',h:'Task 39 — Kiểm thử tích hợp',li:[
  'Tôi giải thích được vì sao cần PostgreSQL thật thay vì InMemory',
  'Tôi mô tả được cơ chế tạo/xoá database test random',
  'Tôi giải thích được vì sao phải tắt parallelization',
  'Tôi biết biến môi trường nào điều khiển connection string test',
  'Tôi kể được vì sao port phải động (local 5433 vs CI 5432)',
  'Tôi phân biệt được test hồi quy và test E2E',
  'Tôi đếm được tổng số test và cách chia theo từng file']},
 {t:'git',h:'Git workflow',li:[
  'Tôi tự tạo được branch đúng format và commit đúng format Conventional Commits kèm mã Jira',
  'Tôi biết PR phải target nhánh nào và cần mấy reviewer',
  'Tôi biết thứ tự merge khi có PR stack',
  'Tôi chạy được build + test trước khi push',
  'Tôi biết 6 type commit được phép']}
];
