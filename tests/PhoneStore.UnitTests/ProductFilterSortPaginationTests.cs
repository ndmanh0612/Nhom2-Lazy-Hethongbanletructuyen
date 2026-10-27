using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PhoneStore.BuildingBlocks.Exceptions;
using PhoneStore.Modules.Catalog.Application.DTOs;
using PhoneStore.Modules.Catalog.Application.Services;
using PhoneStore.Modules.Catalog.Domain.Entities;
using PhoneStore.Modules.Catalog.Infrastructure.Persistence;
using Xunit;

namespace PhoneStore.UnitTests;

public class ProductFilterSortPaginationTests
{
    private CatalogDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CatalogDbContext(options);
    }

    private async Task SeedStandardCatalogAsync(CatalogDbContext dbContext)
    {
        var catPhones = Category.Create("Điện thoại");
        var catAccessories = Category.Create("Phụ kiện");
        var catTablets = Category.Create("Máy tính bảng");

        var brandApple = Brand.Create("Apple");
        var brandSamsung = Brand.Create("Samsung");
        var brandXiaomi = Brand.Create("Xiaomi");

        dbContext.Categories.AddRange(catPhones, catAccessories, catTablets);
        dbContext.Brands.AddRange(brandApple, brandSamsung, brandXiaomi);
        await dbContext.SaveChangesAsync();

        // Product 1: iPhone 15 Pro (Cat: Phones, Brand: Apple)
        // Price: 25M - 28M
        var p1 = Product.Create(catPhones.Id, brandApple.Id, "iPhone 15 Pro", "Flagship Apple 2023");
        p1.AddVariant(ProductVariant.Create(p1.Id, "IP15P-128", "Titan Tự nhiên", "128GB", 25_000_000, 10));
        p1.AddVariant(ProductVariant.Create(p1.Id, "IP15P-256", "Titan Xanh", "256GB", 28_000_000, 5));
        p1.AddImage(ProductImage.Create(p1.Id, "https://example.com/ip15p-main.jpg", 0));
        p1.AddImage(ProductImage.Create(p1.Id, "https://example.com/ip15p-sub.jpg", 1));

        // Product 2: Samsung Galaxy S24 Ultra (Cat: Phones, Brand: Samsung)
        // Price: 30M - 34M
        var p2 = Product.Create(catPhones.Id, brandSamsung.Id, "Samsung Galaxy S24 Ultra", "Flagship Samsung AI");
        p2.AddVariant(ProductVariant.Create(p2.Id, "S24U-256", "Xám Titan", "256GB", 30_000_000, 8));
        p2.AddVariant(ProductVariant.Create(p2.Id, "S24U-512", "Đen Titan", "512GB", 34_000_000, 4));
        p2.AddImage(ProductImage.Create(p2.Id, "https://example.com/s24u-main.jpg", 0));

        // Product 3: Xiaomi 14 (Cat: Phones, Brand: Xiaomi)
        // Price: 18M
        var p3 = Product.Create(catPhones.Id, brandXiaomi.Id, "Xiaomi 14", "Leica Camera Phone");
        p3.AddVariant(ProductVariant.Create(p3.Id, "MI14-256", "Xanh Ngọc", "256GB", 18_000_000, 15));

        // Product 4: Apple Watch Series 9 (Cat: Accessories, Brand: Apple)
        // Price: 9M
        var p4 = Product.Create(catAccessories.Id, brandApple.Id, "Apple Watch Series 9", "Smartwatch Apple");
        p4.AddVariant(ProductVariant.Create(p4.Id, "AW9-41", "Midnight", "41mm", 9_000_000, 20));

        // Product 5: Củ sạc nhanh Apple 20W (Cat: Accessories, Brand: Apple)
        // Price: 500K
        var p5 = Product.Create(catAccessories.Id, brandApple.Id, "Củ sạc nhanh Apple 20W Type-C", "Phụ kiện sạc chính hãng");
        p5.AddVariant(ProductVariant.Create(p5.Id, "CHG-20W", "Trắng", "20W", 500_000, 50));

        // Product 6: Inactive product (should NEVER appear in query)
        var p6 = Product.Create(catPhones.Id, brandSamsung.Id, "Samsung Galaxy S20 Cũ Ẩn", "Sản phẩm ngừng kinh doanh");
        p6.UpdateInfo(p6.Name, p6.Description, p6.SpecificationsJson, isActive: false);
        p6.AddVariant(ProductVariant.Create(p6.Id, "S20-OLD", "Hồng", "128GB", 3_000_000, 0));

        // Product 7: Product with only inactive variants (should not match active price filters)
        var p7 = Product.Create(catPhones.Id, brandXiaomi.Id, "Xiaomi Redmi Note Hết Hàng", "Tất cả variant bị tắt");
        var v7 = ProductVariant.Create(p7.Id, "NOTE-INACTIVE", "Đen", "64GB", 4_000_000, 0);
        v7.SetActive(false);
        p7.AddVariant(v7);

        dbContext.Products.AddRange(p1, p2, p3, p4, p5, p6, p7);
        await dbContext.SaveChangesAsync();
    }

    // ==========================================
    // 1. VALIDATION TESTS
    // ==========================================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task GetProducts_ShouldThrowValidationException_WhenPageIsLessThan1(int invalidPage)
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new CatalogService(dbContext);
        var query = new ProductFilterQuery(null, null, null, null, null, null, Page: invalidPage, PageSize: 20);

        // Act
        var act = async () => await service.GetProductsAsync(query);

        // Assert
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().ContainKey("Page");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(51)]
    [InlineData(100)]
    public async Task GetProducts_ShouldThrowValidationException_WhenPageSizeIsOutOfBounds(int invalidPageSize)
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new CatalogService(dbContext);
        var query = new ProductFilterQuery(null, null, null, null, null, null, Page: 1, PageSize: invalidPageSize);

        // Act
        var act = async () => await service.GetProductsAsync(query);

        // Assert
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().ContainKey("PageSize");
    }

    [Fact]
    public async Task GetProducts_ShouldThrowValidationException_WhenMinPriceIsNegative()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new CatalogService(dbContext);
        var query = new ProductFilterQuery(null, null, null, MinPrice: -1, MaxPrice: null, null);

        // Act
        var act = async () => await service.GetProductsAsync(query);

        // Assert
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().ContainKey("MinPrice");
    }

    [Fact]
    public async Task GetProducts_ShouldThrowValidationException_WhenMaxPriceIsNegative()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new CatalogService(dbContext);
        var query = new ProductFilterQuery(null, null, null, MinPrice: null, MaxPrice: -100, null);

        // Act
        var act = async () => await service.GetProductsAsync(query);

        // Assert
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().ContainKey("MaxPrice");
    }

    [Fact]
    public async Task GetProducts_ShouldThrowValidationException_WhenMinPriceExceedsMaxPrice()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new CatalogService(dbContext);
        var query = new ProductFilterQuery(null, null, null, MinPrice: 20_000_000, MaxPrice: 10_000_000, null);

        // Act
        var act = async () => await service.GetProductsAsync(query);

        // Assert
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().ContainKey("MaxPrice");
    }

    [Theory]
    [InlineData("invalid_sort")]
    [InlineData("popular")]
    [InlineData("rating")]
    public async Task GetProducts_ShouldThrowValidationException_WhenSortByIsInvalid(string invalidSort)
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new CatalogService(dbContext);
        var query = new ProductFilterQuery(null, null, null, null, null, SortBy: invalidSort);

        // Act
        var act = async () => await service.GetProductsAsync(query);

        // Assert
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().ContainKey("SortBy");
    }

    [Fact]
    public async Task GetProducts_ShouldAggregateMultipleValidationErrors_WhenMultipleFieldsAreInvalid()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new CatalogService(dbContext);
        var query = new ProductFilterQuery(null, null, null, MinPrice: -5, MaxPrice: -10, SortBy: "wrong", Page: 0, PageSize: 100);

        // Act
        var act = async () => await service.GetProductsAsync(query);

        // Assert
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().ContainKey("Page");
        ex.Which.Errors.Should().ContainKey("PageSize");
        ex.Which.Errors.Should().ContainKey("MinPrice");
        ex.Which.Errors.Should().ContainKey("MaxPrice");
        ex.Which.Errors.Should().ContainKey("SortBy");
    }

    // ==========================================
    // 2. FILTERING TESTS
    // ==========================================

    [Fact]
    public async Task GetProducts_ShouldExcludeInactiveProducts_ByDefault()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        await SeedStandardCatalogAsync(dbContext);
        var service = new CatalogService(dbContext);

        // Act
        var result = await service.GetProductsAsync(new ProductFilterQuery(null, null, null, null, null, null));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().NotContain(p => p.Name.Contains("Ẩn"));
    }

    [Theory]
    [InlineData("iphone", "iPhone 15 Pro")]
    [InlineData("SAMSUNG", "Samsung Galaxy S24 Ultra")]
    [InlineData("ultra", "Samsung Galaxy S24 Ultra")]
    [InlineData("sạc nhanh", "Củ sạc nhanh Apple 20W Type-C")]
    public async Task GetProducts_ShouldFilterBySearchKeyword_CaseInsensitive(string keyword, string expectedProductName)
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        await SeedStandardCatalogAsync(dbContext);
        var service = new CatalogService(dbContext);

        // Act
        var result = await service.GetProductsAsync(new ProductFilterQuery(Search: keyword, null, null, null, null, null));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().Contain(p => p.Name == expectedProductName);
    }

    [Fact]
    public async Task GetProducts_ShouldFilterByCategoryId()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        await SeedStandardCatalogAsync(dbContext);
        var service = new CatalogService(dbContext);

        var accessoriesCategory = await dbContext.Categories.FirstAsync(c => c.Name == "Phụ kiện");

        // Act
        var result = await service.GetProductsAsync(new ProductFilterQuery(null, CategoryId: accessoriesCategory.Id, null, null, null, null));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(2);
        result.Value.Items.Should().OnlyContain(p => p.CategoryId == accessoriesCategory.Id);
        result.Value.Items.Select(p => p.Name).Should().Contain(["Apple Watch Series 9", "Củ sạc nhanh Apple 20W Type-C"]);
    }

    [Fact]
    public async Task GetProducts_ShouldFilterByBrandId()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        await SeedStandardCatalogAsync(dbContext);
        var service = new CatalogService(dbContext);

        var xiaomiBrand = await dbContext.Brands.FirstAsync(b => b.Name == "Xiaomi");

        // Act
        var result = await service.GetProductsAsync(new ProductFilterQuery(null, null, BrandId: xiaomiBrand.Id, null, null, null));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().Contain(p => p.Name == "Xiaomi 14");
        result.Value.Items.Should().OnlyContain(p => p.BrandId == xiaomiBrand.Id);
    }

    [Fact]
    public async Task GetProducts_ShouldFilterByPriceRange_BothMinAndMaxPrice()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        await SeedStandardCatalogAsync(dbContext);
        var service = new CatalogService(dbContext);

        // Range 15M to 28M: Should match Xiaomi 14 (18M) and iPhone 15 Pro (25M-28M)
        // Should NOT match S24 Ultra (30M+) or Apple Watch (9M)
        var query = new ProductFilterQuery(null, null, null, MinPrice: 15_000_000, MaxPrice: 28_000_000, null);

        // Act
        var result = await service.GetProductsAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(2);
        result.Value.Items.Select(p => p.Name).Should().BeEquivalentTo(["iPhone 15 Pro", "Xiaomi 14"]);
    }

    [Fact]
    public async Task GetProducts_ShouldFilterByMinPriceOnly()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        await SeedStandardCatalogAsync(dbContext);
        var service = new CatalogService(dbContext);

        // Price >= 25M: Should match iPhone 15 Pro (25M+) and S24 Ultra (30M+)
        var query = new ProductFilterQuery(null, null, null, MinPrice: 25_000_000, MaxPrice: null, null);

        // Act
        var result = await service.GetProductsAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(2);
        result.Value.Items.Select(p => p.Name).Should().BeEquivalentTo(["iPhone 15 Pro", "Samsung Galaxy S24 Ultra"]);
    }

    [Fact]
    public async Task GetProducts_ShouldFilterByMaxPriceOnly()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        await SeedStandardCatalogAsync(dbContext);
        var service = new CatalogService(dbContext);

        // Price <= 1M: Should match Củ sạc nhanh Apple 20W (500K)
        var query = new ProductFilterQuery(null, null, null, MinPrice: null, MaxPrice: 1_000_000, null);

        // Act
        var result = await service.GetProductsAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
        result.Value.Items[0].Name.Should().Be("Củ sạc nhanh Apple 20W Type-C");
    }

    [Fact]
    public async Task GetProducts_ShouldIgnoreInactiveVariants_WhenFilteringByPrice()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        await SeedStandardCatalogAsync(dbContext);
        var service = new CatalogService(dbContext);

        // Product 7 has an inactive variant with price 4M.
        // Filtering price between 3M and 5M should NOT return Product 7!
        var query = new ProductFilterQuery(null, null, null, MinPrice: 3_000_000, MaxPrice: 5_000_000, null);

        // Act
        var result = await service.GetProductsAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(0);
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProducts_ShouldCombineMultipleFilters_Successfully()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        await SeedStandardCatalogAsync(dbContext);
        var service = new CatalogService(dbContext);

        var phonesCategory = await dbContext.Categories.FirstAsync(c => c.Name == "Điện thoại");
        var appleBrand = await dbContext.Brands.FirstAsync(b => b.Name == "Apple");

        var query = new ProductFilterQuery(
            Search: "pro",
            CategoryId: phonesCategory.Id,
            BrandId: appleBrand.Id,
            MinPrice: 20_000_000,
            MaxPrice: 30_000_000,
            SortBy: "newest"
        );

        // Act
        var result = await service.GetProductsAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
        result.Value.Items[0].Name.Should().Be("iPhone 15 Pro");
    }

    [Fact]
    public async Task GetProducts_ShouldReturnEmptyPagedResult_WhenNoProductsMatchFilter()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        await SeedStandardCatalogAsync(dbContext);
        var service = new CatalogService(dbContext);

        var query = new ProductFilterQuery(Search: "Sản phẩm không có thực 9999", null, null, null, null, null);

        // Act
        var result = await service.GetProductsAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(0);
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task GetProducts_ShouldReturnEmptyPagedResult_WhenCategoryIdDoesNotExist()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        await SeedStandardCatalogAsync(dbContext);
        var service = new CatalogService(dbContext);

        var query = new ProductFilterQuery(null, CategoryId: 99999, null, null, null, null);

        // Act
        var result = await service.GetProductsAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(0);
        result.Value.Items.Should().BeEmpty();
    }

    // ==========================================
    // 3. SORTING TESTS
    // ==========================================

    [Fact]
    public async Task GetProducts_ShouldSortByPriceAsc_WhenRequested()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        await SeedStandardCatalogAsync(dbContext);
        var service = new CatalogService(dbContext);

        var query = new ProductFilterQuery(null, null, null, null, null, SortBy: "price_asc");

        // Act
        var result = await service.GetProductsAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var activeWithPrice = result.Value.Items.Where(p => p.MinPrice > 0).Select(p => p.MinPrice).ToList();
        activeWithPrice.Should().BeInAscendingOrder();

        // Product without active variants should be placed last
        result.Value.Items.First().Name.Should().Be("Củ sạc nhanh Apple 20W Type-C"); // 500k
        result.Value.Items.Last().MinPrice.Should().Be(0);
    }

    [Fact]
    public async Task GetProducts_ShouldSortByPriceDesc_WhenRequested()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        await SeedStandardCatalogAsync(dbContext);
        var service = new CatalogService(dbContext);

        var query = new ProductFilterQuery(null, null, null, null, null, SortBy: "price_desc");

        // Act
        var result = await service.GetProductsAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var activeWithPrice = result.Value.Items.Where(p => p.MinPrice > 0).Select(p => p.MinPrice).ToList();
        activeWithPrice.Should().BeInDescendingOrder();
        result.Value.Items.First().Name.Should().Be("Samsung Galaxy S24 Ultra"); // 30M
    }

    [Fact]
    public async Task GetProducts_ShouldInvertOrder_BetweenPriceAscAndPriceDesc()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        await SeedStandardCatalogAsync(dbContext);
        var service = new CatalogService(dbContext);

        // Filter only products with active priced variants
        var queryAsc = new ProductFilterQuery(null, null, null, MinPrice: 1, MaxPrice: null, SortBy: "price_asc", PageSize: 50);
        var queryDesc = new ProductFilterQuery(null, null, null, MinPrice: 1, MaxPrice: null, SortBy: "price_desc", PageSize: 50);

        // Act
        var resultAsc = await service.GetProductsAsync(queryAsc);
        var resultDesc = await service.GetProductsAsync(queryDesc);

        // Assert
        var namesAsc = resultAsc.Value.Items.Select(p => p.Name).ToList();
        var namesDesc = resultDesc.Value.Items.Select(p => p.Name).ToList();

        namesDesc.Should().Equal(Enumerable.Reverse(namesAsc));
    }

    [Theory]
    [InlineData("PRICE_ASC")]
    [InlineData("Price_Desc")]
    [InlineData("NEWEST")]
    [InlineData("  price_asc  ")]
    public async Task GetProducts_ShouldAcceptSortBy_CaseInsensitivelyAndTrimmed(string validSort)
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        await SeedStandardCatalogAsync(dbContext);
        var service = new CatalogService(dbContext);

        var query = new ProductFilterQuery(null, null, null, null, null, SortBy: validSort);

        // Act
        var result = await service.GetProductsAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    // ==========================================
    // 4. PAGINATION TESTS
    // ==========================================

    [Fact]
    public async Task GetProducts_ShouldPaginateCorrectly_FirstAndSecondPage()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        await SeedStandardCatalogAsync(dbContext);
        var service = new CatalogService(dbContext);

        var page1Query = new ProductFilterQuery(null, null, null, null, null, SortBy: "price_asc", Page: 1, PageSize: 2);
        var page2Query = new ProductFilterQuery(null, null, null, null, null, SortBy: "price_asc", Page: 2, PageSize: 2);

        // Act
        var page1Result = await service.GetProductsAsync(page1Query);
        var page2Result = await service.GetProductsAsync(page2Query);

        // Assert
        page1Result.Value.Items.Should().HaveCount(2);
        page1Result.Value.Page.Should().Be(1);
        page1Result.Value.PageSize.Should().Be(2);
        page1Result.Value.HasPreviousPage.Should().BeFalse();
        page1Result.Value.HasNextPage.Should().BeTrue();

        page2Result.Value.Items.Should().HaveCount(2);
        page2Result.Value.Page.Should().Be(2);
        page2Result.Value.PageSize.Should().Be(2);
        page2Result.Value.HasPreviousPage.Should().BeTrue();
        page2Result.Value.HasNextPage.Should().BeTrue();

        // Items across page 1 and page 2 must not overlap
        var page1Ids = page1Result.Value.Items.Select(p => p.Id);
        var page2Ids = page2Result.Value.Items.Select(p => p.Id);
        page1Ids.Intersect(page2Ids).Should().BeEmpty();
    }

    [Fact]
    public async Task GetProducts_ShouldComputePaginationMetadataAccurately()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        await SeedStandardCatalogAsync(dbContext);
        var service = new CatalogService(dbContext);

        // Total active products in standard catalog: 6 (p1, p2, p3, p4, p5, p7)
        // With PageSize = 2, TotalPages should be 3
        var query = new ProductFilterQuery(null, null, null, null, null, null, Page: 1, PageSize: 2);

        // Act
        var result = await service.GetProductsAsync(query);

        // Assert
        result.Value.TotalCount.Should().Be(6);
        result.Value.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task GetProducts_ShouldReturnEmptyItems_WhenPageExceedsTotalPages()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        await SeedStandardCatalogAsync(dbContext);
        var service = new CatalogService(dbContext);

        // Total 6 active products, PageSize 2 -> TotalPages 3. Querying Page 5:
        var query = new ProductFilterQuery(null, null, null, null, null, null, Page: 5, PageSize: 2);

        // Act
        var result = await service.GetProductsAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(6);
        result.Value.Page.Should().Be(5);
        result.Value.HasNextPage.Should().BeFalse();
        result.Value.HasPreviousPage.Should().BeTrue();
    }

    // ==========================================
    // 5. DTO MAPPING AND ENRICHMENT
    // ==========================================

    [Fact]
    public async Task GetProducts_ShouldMapMinPriceMaxPriceAndImagesCorrectly()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        await SeedStandardCatalogAsync(dbContext);
        var service = new CatalogService(dbContext);

        var query = new ProductFilterQuery(Search: "iPhone 15 Pro", null, null, null, null, null);

        // Act
        var result = await service.GetProductsAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var product = result.Value.Items.First();
        product.Name.Should().Be("iPhone 15 Pro");
        product.MinPrice.Should().Be(25_000_000);
        product.MaxPrice.Should().Be(28_000_000);
        product.CategoryName.Should().Be("Điện thoại");
        product.BrandName.Should().Be("Apple");
        product.MainImageUrl.Should().Be("https://example.com/ip15p-main.jpg");
    }

    [Fact]
    public async Task GetProducts_ShouldFallbackToDefaultCategoryAndBrandName_WhenMissingInDb()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        // Product without category or brand records in DB (orphaned IDs)
        var orphanProduct = Product.Create(categoryId: 8888, brandId: 9999, "Orphan Phone", "No Cat No Brand");
        orphanProduct.AddVariant(ProductVariant.Create(orphanProduct.Id, "ORP-001", "Black", "64GB", 1_000_000, 1));
        dbContext.Products.Add(orphanProduct);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);

        // Act
        var result = await service.GetProductsAsync(new ProductFilterQuery(null, null, null, null, null, null));

        // Assert
        result.IsSuccess.Should().BeTrue();
        var dto = result.Value.Items.First(p => p.Id == orphanProduct.Id);
        dto.CategoryName.Should().Be("Chưa phân loại");
        dto.BrandName.Should().Be("Chưa phân loại");
    }
}
