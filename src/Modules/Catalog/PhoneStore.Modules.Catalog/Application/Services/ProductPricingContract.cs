using Microsoft.EntityFrameworkCore;
using PhoneStore.BuildingBlocks.Contracts.Catalog;
using PhoneStore.Modules.Catalog.Infrastructure.Persistence;

namespace PhoneStore.Modules.Catalog.Application.Services;

/// <summary>
/// Catalog là module sở hữu dữ liệu giá, vì vậy nó là nơi implement IProductPricingContract.
/// Các module khác (Cart, Order) chỉ phụ thuộc vào interface trong BuildingBlocks.
/// </summary>
public class ProductPricingContract : IProductPricingContract
{
    private readonly CatalogDbContext _dbContext;

    public ProductPricingContract(CatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProductVariantPricing?> GetVariantPricingAsync(
        long productVariantId,
        CancellationToken cancellationToken = default)
    {
        if (productVariantId <= 0)
        {
            return null;
        }

        return await (
            from variant in _dbContext.ProductVariants.AsNoTracking()
            join product in _dbContext.Products.AsNoTracking() on variant.ProductId equals product.Id
            where variant.Id == productVariantId && variant.IsActive && product.IsActive
            select new ProductVariantPricing(
                variant.Id,
                variant.ProductId,
                product.Name,
                variant.Sku,
                variant.Price,
                variant.StockQuantity,
                variant.IsActive))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
