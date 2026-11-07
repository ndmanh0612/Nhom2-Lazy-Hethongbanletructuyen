using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PhoneStore.BuildingBlocks.Exceptions;
using PhoneStore.Modules.Catalog.Application.Services;
using PhoneStore.Modules.Catalog.Domain.Entities;
using PhoneStore.Modules.Catalog.Infrastructure.Persistence;
using Xunit;

namespace PhoneStore.UnitTests;

/// <summary>
/// N2LHTBLTT-17 (FR-05): Bộ kiểm thử đơn vị cho API chi tiết sản phẩm và phiên bản SKU.
/// Kiểm thử các trường hợp nghiệp vụ, biến thể SKU, hình ảnh, phân loại và ngoại lệ.
/// </summary>
public class ProductDetailTests
{
    private CatalogDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CatalogDbContext(options);
    }

    [Fact]
    public async Task GetProductById_WhenProductExists_ShouldReturnProductDetailWithAllFields()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var category = Category.Create("Điện thoại thông minh");
        var brand = Brand.Create("Apple");
        dbContext.Categories.Add(category);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var specsJson = "{\"screen\": \"6.1 inch OLED\", \"chip\": \"A16 Bionic\", \"ram\": \"6GB\"}";
        var product = Product.Create(category.Id, brand.Id, "iPhone 15", "Flagship tiêu chuẩn Apple", specsJson);
        product.AddVariant(ProductVariant.Create(product.Id, "IP15-128-BLK", "Đen", "128GB", 22_900_000, 50));
        product.AddImage(ProductImage.Create(product.Id, "https://example.com/ip15.png", 0));

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);

        // Act
        var result = await service.GetProductByIdAsync(product.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var detail = result.Value;
        detail.Should().NotBeNull();
        detail.Id.Should().Be(product.Id);
        detail.Name.Should().Be("iPhone 15");
        detail.Description.Should().Be("Flagship tiêu chuẩn Apple");
        detail.SpecificationsJson.Should().Be(specsJson);
        detail.CategoryId.Should().Be(category.Id);
        detail.CategoryName.Should().Be("Điện thoại thông minh");
        detail.BrandId.Should().Be(brand.Id);
        detail.BrandName.Should().Be("Apple");
        detail.IsActive.Should().BeTrue();
        detail.Variants.Should().HaveCount(1);
        detail.Images.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetProductById_WhenProductHasVariants_ShouldReturnOnlyActiveVariants()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var category = Category.Create("Điện thoại");
        var brand = Brand.Create("Samsung");
        dbContext.Categories.Add(category);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var product = Product.Create(category.Id, brand.Id, "Galaxy S24", "Samsung AI Smartphone");

        // Variant 1: Active
        var vActive1 = ProductVariant.Create(product.Id, "S24-128-GRY", "Xám", "128GB", 20_000_000, 15);
        // Variant 2: Inactive
        var vInactive = ProductVariant.Create(product.Id, "S24-128-YEL", "Vàng", "128GB", 20_000_000, 0);
        vInactive.SetActive(false);
        // Variant 3: Active
        var vActive2 = ProductVariant.Create(product.Id, "S24-256-BLK", "Đen", "256GB", 23_000_000, 25);

        product.AddVariant(vActive1);
        product.AddVariant(vInactive);
        product.AddVariant(vActive2);

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);

        // Act
        var result = await service.GetProductByIdAsync(product.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var detail = result.Value;
        detail.Variants.Should().HaveCount(2);
        detail.Variants.Should().NotContain(v => v.Sku == "S24-128-YEL");
        detail.Variants.Should().Contain(v => v.Sku == "S24-128-GRY" && v.Price == 20_000_000 && v.StockQuantity == 15);
        detail.Variants.Should().Contain(v => v.Sku == "S24-256-BLK" && v.Price == 23_000_000 && v.StockQuantity == 25);
    }

    [Fact]
    public async Task GetProductById_WhenVariantsAreMapped_ShouldMapAllFieldsAccurately()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var category = Category.Create("Điện thoại");
        var brand = Brand.Create("Xiaomi");
        dbContext.Categories.Add(category);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var product = Product.Create(category.Id, brand.Id, "Xiaomi 14", "Leica Camera");
        var variant = ProductVariant.Create(product.Id, "MI14-256-GRN", "Xanh Ngọc", "256GB", 18_500_000, 30);
        product.AddVariant(variant);

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);

        // Act
        var result = await service.GetProductByIdAsync(product.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var vDto = result.Value.Variants.Single();
        vDto.Id.Should().Be(variant.Id);
        vDto.ProductId.Should().Be(product.Id);
        vDto.Sku.Should().Be("MI14-256-GRN");
        vDto.Color.Should().Be("Xanh Ngọc");
        vDto.Capacity.Should().Be("256GB");
        vDto.Price.Should().Be(18_500_000);
        vDto.StockQuantity.Should().Be(30);
        vDto.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetProductById_WhenAllVariantsAreInactive_ShouldReturnEmptyVariantsList()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var category = Category.Create("Điện thoại");
        var brand = Brand.Create("Apple");
        dbContext.Categories.Add(category);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var product = Product.Create(category.Id, brand.Id, "iPhone 11", "Mẫu cũ đã ngừng kinh doanh SKU");
        var v1 = ProductVariant.Create(product.Id, "IP11-64", "Trắng", "64GB", 8_000_000, 0);
        v1.SetActive(false);
        var v2 = ProductVariant.Create(product.Id, "IP11-128", "Đen", "128GB", 9_500_000, 0);
        v2.SetActive(false);

        product.AddVariant(v1);
        product.AddVariant(v2);

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);

        // Act
        var result = await service.GetProductByIdAsync(product.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Variants.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProductById_WhenProductHasNoVariants_ShouldReturnEmptyVariantsList()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var category = Category.Create("Laptop");
        var brand = Brand.Create("Dell");
        dbContext.Categories.Add(category);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var product = Product.Create(category.Id, brand.Id, "Dell XPS 13 Concept", "Chưa công bố biến thể");
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);

        // Act
        var result = await service.GetProductByIdAsync(product.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Variants.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProductById_WhenProductHasImages_ShouldReturnImagesOrderedBySortOrderAscending()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var category = Category.Create("Điện thoại");
        var brand = Brand.Create("Apple");
        dbContext.Categories.Add(category);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var product = Product.Create(category.Id, brand.Id, "iPhone 15 Pro", "Flagship");
        var img3 = ProductImage.Create(product.Id, "https://example.com/back.jpg", sortOrder: 3);
        var img0 = ProductImage.Create(product.Id, "https://example.com/front.jpg", sortOrder: 0);
        var img1 = ProductImage.Create(product.Id, "https://example.com/side.jpg", sortOrder: 1);

        // Thêm theo thứ tự lộn xộn
        product.AddImage(img3);
        product.AddImage(img0);
        product.AddImage(img1);

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);

        // Act
        var result = await service.GetProductByIdAsync(product.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var images = result.Value.Images;
        images.Should().HaveCount(3);
        images[0].ImageUrl.Should().Be("https://example.com/front.jpg");
        images[0].SortOrder.Should().Be(0);

        images[1].ImageUrl.Should().Be("https://example.com/side.jpg");
        images[1].SortOrder.Should().Be(1);

        images[2].ImageUrl.Should().Be("https://example.com/back.jpg");
        images[2].SortOrder.Should().Be(3);
    }

    [Fact]
    public async Task GetProductById_WhenImagesHaveSameSortOrder_ShouldMaintainDeterministicOrdering()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var category = Category.Create("Điện thoại");
        var brand = Brand.Create("Apple");
        dbContext.Categories.Add(category);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var product = Product.Create(category.Id, brand.Id, "iPhone 15 Pro", "Flagship");
        var imgA = ProductImage.Create(product.Id, "https://example.com/img-a.jpg", sortOrder: 0);
        var imgB = ProductImage.Create(product.Id, "https://example.com/img-b.jpg", sortOrder: 0);

        product.AddImage(imgA);
        product.AddImage(imgB);

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);

        // Act
        var result = await service.GetProductByIdAsync(product.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Images.Should().HaveCount(2);
        result.Value.Images[0].Id.Should().BeLessThanOrEqualTo(result.Value.Images[1].Id);
    }

    [Fact]
    public async Task GetProductById_WhenProductHasNoImages_ShouldReturnEmptyImagesList()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var category = Category.Create("Phụ kiện");
        var brand = Brand.Create("Apple");
        dbContext.Categories.Add(category);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var product = Product.Create(category.Id, brand.Id, "Củ sạc 20W", "Không có ảnh");
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);

        // Act
        var result = await service.GetProductByIdAsync(product.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Images.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProductById_WhenProductDoesNotExist_ShouldThrowNotFoundException()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new CatalogService(dbContext);

        // Act
        var act = async () => await service.GetProductByIdAsync(999999);

        // Assert
        var exception = await act.Should().ThrowAsync<NotFoundException>();
        exception.Which.Code.Should().Be("PRODUCT_NOT_FOUND");
    }

    [Fact]
    public async Task GetProductById_WhenProductIsInactive_ShouldThrowNotFoundException()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var category = Category.Create("Điện thoại");
        var brand = Brand.Create("Apple");
        dbContext.Categories.Add(category);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var product = Product.Create(category.Id, brand.Id, "iPhone 15 Bị Ẩn", "Sản phẩm thử nghiệm");
        product.UpdateInfo(product.Name, product.Description, product.SpecificationsJson, isActive: false);

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);

        // Act
        var act = async () => await service.GetProductByIdAsync(product.Id);

        // Assert
        var exception = await act.Should().ThrowAsync<NotFoundException>();
        exception.Which.Code.Should().Be("PRODUCT_NOT_FOUND");
    }

    [Fact]
    public async Task GetProductById_WhenIdIsZeroOrNegative_ShouldThrowNotFoundException()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new CatalogService(dbContext);

        // Act
        var actZero = async () => await service.GetProductByIdAsync(0);
        var actNegative = async () => await service.GetProductByIdAsync(-10);

        // Assert
        await actZero.Should().ThrowAsync<NotFoundException>();
        await actNegative.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetProductById_WhenCategoryDoesNotExist_ShouldReturnFallbackCategoryName()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var brand = Brand.Create("Apple");
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        // CategoryId = 99999 không tồn tại
        var product = Product.Create(99999, brand.Id, "iPhone Không Có Danh Mục", "Mô tả");
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);

        // Act
        var result = await service.GetProductByIdAsync(product.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CategoryName.Should().Be("Chưa phân loại");
        result.Value.BrandName.Should().Be("Apple");
    }

    [Fact]
    public async Task GetProductById_WhenBrandDoesNotExist_ShouldReturnFallbackBrandName()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var category = Category.Create("Điện thoại");
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync();

        // BrandId = 99999 không tồn tại
        var product = Product.Create(category.Id, 99999, "Điện thoại Không Có Thương Hiệu", "Mô tả");
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);

        // Act
        var result = await service.GetProductByIdAsync(product.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CategoryName.Should().Be("Điện thoại");
        result.Value.BrandName.Should().Be("Chưa phân loại");
    }

    [Fact]
    public async Task GetProductById_WhenSpecificationsJsonIsValid_ShouldMapCorrectly()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var category = Category.Create("Điện thoại");
        var brand = Brand.Create("Google");
        dbContext.Categories.Add(category);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var customSpecs = "{\"camera\": \"50MP\", \"battery\": \"5050mAh\", \"os\": \"Android 14\"}";
        var product = Product.Create(category.Id, brand.Id, "Pixel 8 Pro", "Google flagship", customSpecs);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);

        // Act
        var result = await service.GetProductByIdAsync(product.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SpecificationsJson.Should().Be(customSpecs);
    }
}
