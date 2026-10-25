namespace PhoneStore.BuildingBlocks.Contracts.Catalog;

/// <summary>
/// Thông tin giá của một phiên bản sản phẩm, do Catalog sở hữu và chia sẻ qua contract.
/// </summary>
public record ProductVariantPricing(
    long ProductVariantId,
    long ProductId,
    string ProductName,
    string Sku,
    long Price,
    int StockQuantity,
    bool IsActive);

/// <summary>
/// Query contract để các module khác (Cart, Order) lấy giá hiện tại của sản phẩm mà không
/// cần reference trực tiếp vào CatalogDbContext.
/// </summary>
public interface IProductPricingContract
{
    Task<ProductVariantPricing?> GetVariantPricingAsync(
        long productVariantId,
        CancellationToken cancellationToken = default);
}
