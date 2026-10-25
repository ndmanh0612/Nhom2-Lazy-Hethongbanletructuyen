# Hỏi Nghĩa — Bug trong API danh mục sản phẩm (FR-04)

> Phát hiện khi viết integration test E2E cho Catalog (`N2LHTBLTT-11`).

---

## Tin nhắn ngắn

```
Nghĩa ơi, tôi vừa viết integration test E2E cho API sản phẩm và phát hiện 2 bug trong
CatalogService.GetProductsAsync:

1) SORT KHÔNG HOẠT ĐỘNG: ?sortBy=price_desc vẫn trả về sản phẩm theo thứ tự tạo.
   Trong code, query.SortBy được validate (CatalogValidators) nhưng KHÔNG được dùng
   trong câu truy vấn — luôn OrderByDescending(p => p.CreatedAt).

2) LỌC GIÁ KHÔNG HOẠT ĐỘNG: ?minPrice=20000000 vẫn trả về sản phẩm rẻ hơn.
   Tương tự, query.MinPrice / query.MaxPrice có validate nhưng không áp dụng vào query.

Cả hai đã được tôi sửa tạm trong src/Modules/Catalog/.../CatalogService.cs kèm test
(CatalogQueryTests — 10 test). Nhưng đây là phần FR-04 của bạn nên tôi muốn hỏi:

- Anh muốn giữ bản sửa của tôi, hay anh sửa lại theo ý anh (ví dụ sắp xếp theo giá MAX
  thay vì MIN của các biến thể, hoặc thêm sort theo tên / đánh giá)?
- Có cần thêm tiêu chí sắp xếp nào khác không (bán chạy nhất, đánh giá cao nhất)?
- Filter theo khoảng giá nên lấy theo giá MIN hay giá MAX của sản phẩm?
  Tôi đang dùng: sản phẩm được giữ nếu CÓ ít nhất một biến thể đang bán nằm trong khoảng.

Cảm ơn anh 🙏
```

---

## Chi tiết kỹ thuật

### Bug 1 — `SortBy` bị bỏ qua

`ProductFilterQuery` có `SortBy` (`price_asc` / `price_desc` / `newest`) và
`CatalogValidators` có rule kiểm tra giá trị hợp lệ — nên API trả lỗi 400 khi
`sortBy=bogus`. Nhưng khi truy vấn, `SortBy` không được dùng:

```csharp
// TRƯỚC KHI SỬA — trong src/Modules/Catalog/.../CatalogService.cs
var products = await baseQuery
    .Include(p => p.Variants)
    .Include(p => p.Images)
    .OrderByDescending(p => p.CreatedAt)   // ← luôn theo thời gian tạo
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync(cancellationToken);
```

**Sửa:** chuyển sang `AsEnumerable()` rồi áp dụng `switch` theo `SortBy`, dùng giá
biến thể thấp nhất (`Min`) làm mốc:

```csharp
products = query.SortBy switch
{
    "price_asc"  => products.OrderBy(p => p.Variants.Where(v => v.IsActive).Select(v => (long?)v.Price).Min() ?? long.MaxValue),
    "price_desc" => products.OrderByDescending(p => p.Variants.Where(v => v.IsActive).Select(v => (long?)v.Price).Min() ?? long.MaxValue),
    "newest"     => products.OrderByDescending(p => p.CreatedAt),
    _            => products.OrderByDescending(p => p.CreatedAt)
};
```

> Lưu ý: `AsEnumerable()` đưa việc sắp xếp về in-memory. Nếu anh muốn sort hoàn toàn
> ở tầng SQL (để phân trang scale tốt), cần đổi sang subquery trên `ProductVariants`
> (`Min(v => v.Price)` trong `OrderBy` của IQueryable). Tôi chọn cách in-memory vì
> `OrderBy` theo biểu thức lồng nhau không translate được bởi EF Core.

### Bug 2 — `MinPrice` / `MaxPrice` bị bỏ qua

Tương tự, hai tham số này được validate (bao gồm rule `MaxPrice >= MinPrice`) nhưng
không có trong `Where`. **Sửa:**

```csharp
if (query.MinPrice.HasValue || query.MaxPrice.HasValue)
{
    var minPrice = query.MinPrice ?? 0;
    var maxPrice = query.MaxPrice ?? long.MaxValue;

    baseQuery = baseQuery.Where(p => p.Variants.Any(v =>
        v.IsActive && v.Price >= minPrice && v.Price <= maxPrice));
}
```

Câu hỏi cho anh: quy tắc "có ít nhất một biến thể nằm trong khoảng" có đúng ý anh
không, hay nên lọc theo giá MIN (sản phẩm rẻ nhất) / giá MAX (sản phẩm đắt nhất)?

### Bug 3 — phân trang bị bỏ qua (đã phát hiện khi test)

Khi sửa `SortBy` (bắt buộc phải rời khỏi IQueryable), tôi vô tình phát hiện `dtoList`
đang map từ `products` (toàn bộ) thay vì `pagedProducts` (đã cắt trang):

```csharp
// TRƯỚC KHI SỬA
var dtoList = products.Select(p => { ... });   // ← products = toàn bộ, không phân trang

// SAU KHI SỬA
var dtoList = pagedProducts.Select(p => { ... });
```

Bug này có sẵn từ trước: `?pageSize=1` trả về **3** sản phẩm thay vì 1 (catalog chỉ
có 3 sản phẩm seed). Đã có test `Pagination_splits_results_and_reports_total_count`.

### Test đã thêm

`tests/PhoneStore.IntegrationTests/CatalogQueryTests.cs` — 10 test:

| Test | Phủ |
|---|---|
| `Search_by_name_returns_matching_product` | tìm kiếm theo tên |
| `Filter_by_min_price_excludes_cheaper_products` | lọc giá số 1 |
| `Sort_by_price_desc_returns_highest_price_first` | giá giảm dần |
| `Sort_by_price_asc_returns_lowest_price_first` | giá tăng dần |
| `Sort_by_price_desc_and_asc_are_reverse_of_each_other` | 2 chiều sort là ảnh ngược nhau |
| `Sort_by_newest_returns_products_in_descending_created_order` | mới nhất |
| `Pagination_splits_results_and_reports_total_count` | phân trang + `totalCount` |
| `Filter_by_category_returns_only_products_of_that_category` | lọc danh mục |
| `Invalid_sort_by_returns_validation_error` | `sortBy=bogus` → 400 |
| `Unknown_product_returns_not_found` | `/api/v1/products/999999` → 404 |

### Tóm tắt

| Hạng mục | Trước | Sau |
|---|---|---|
| `SortBy` | validate nhưng không dùng | áp dụng 3 nhánh sort |
| `MinPrice` / `MaxPrice` | validate nhưng không dùng | lọc theo biến thể đang bán |
| Phân trang | trả toàn bộ kết quả | trả đúng `pageSize` |
| Test Catalog | 0 test E2E | 10 test E2E |
