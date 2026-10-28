using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PhoneStore.BuildingBlocks.Exceptions;
using PhoneStore.Modules.Catalog.Application.DTOs;
using PhoneStore.Modules.Catalog.Application.Services;
using PhoneStore.Modules.Catalog.Domain.Entities;
using PhoneStore.Modules.Catalog.Infrastructure.Persistence;
using Xunit;

namespace PhoneStore.UnitTests;

public class CatalogCategoryAndBrandTests
{
    private CatalogDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CatalogDbContext(options);
    }

    // ==========================================
    // CATEGORY TESTS
    // ==========================================

    [Fact]
    public async Task GetCategories_ShouldReturnActiveCategoriesOnly_ByDefault()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var cat1 = Category.Create("Điện thoại");
        var cat2 = Category.Create("Phụ kiện cũ");
        cat2.Update("Phụ kiện cũ", false); // Inactive

        dbContext.Categories.AddRange(cat1, cat2);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);

        // Act
        var result = await service.GetCategoriesAsync(includeInactive: false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("Điện thoại");
    }

    [Fact]
    public async Task GetCategories_ShouldReturnAllCategories_WhenIncludeInactiveIsTrue()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var cat1 = Category.Create("Điện thoại");
        var cat2 = Category.Create("Phụ kiện cũ");
        cat2.Update("Phụ kiện cũ", false);

        dbContext.Categories.AddRange(cat1, cat2);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);

        // Act
        var result = await service.GetCategoriesAsync(includeInactive: true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetCategoryById_ShouldReturnCategory_WhenIdExists()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var cat = Category.Create("Máy tính bảng");
        dbContext.Categories.Add(cat);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);

        // Act
        var result = await service.GetCategoryByIdAsync(cat.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(cat.Id);
        result.Value.Name.Should().Be("Máy tính bảng");
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetCategoryById_ShouldThrowNotFoundException_WhenIdDoesNotExist()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new CatalogService(dbContext);

        // Act
        var act = async () => await service.GetCategoryByIdAsync(99999);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*99999*");
    }

    [Fact]
    public async Task CreateCategory_ShouldCreateCategorySuccessfully()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new CatalogService(dbContext);
        var request = new CreateCategoryRequest("Đồng hồ thông minh");

        // Act
        var result = await service.CreateCategoryAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Đồng hồ thông minh");
        result.Value.IsActive.Should().BeTrue();

        var dbEntity = await dbContext.Categories.FindAsync(result.Value.Id);
        dbEntity.Should().NotBeNull();
        dbEntity!.Name.Should().Be("Đồng hồ thông minh");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateCategory_ShouldThrowValidationException_WhenNameIsInvalid(string? invalidName)
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new CatalogService(dbContext);
        var request = new CreateCategoryRequest(invalidName!);

        // Act
        var act = async () => await service.CreateCategoryAsync(request);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateCategory_ShouldThrowValidationException_WhenNameExceeds100Characters()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new CatalogService(dbContext);
        var longName = new string('A', 101);
        var request = new CreateCategoryRequest(longName);

        // Act
        var act = async () => await service.CreateCategoryAsync(request);

        // Assert
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors["Name"].Should().Contain(e => e.Contains("100"));
    }

    [Fact]
    public async Task CreateCategory_ShouldThrowConflictException_WhenNameAlreadyExists()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var cat = Category.Create("Âm thanh");
        dbContext.Categories.Add(cat);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);
        var request = new CreateCategoryRequest("âm thanh"); // Case-insensitive test

        // Act
        var act = async () => await service.CreateCategoryAsync(request);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task UpdateCategory_ShouldUpdateCategorySuccessfully()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var cat = Category.Create("Phụ kiện");
        dbContext.Categories.Add(cat);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);
        var request = new UpdateCategoryRequest("Phụ kiện cao cấp", false);

        // Act
        var result = await service.UpdateCategoryAsync(cat.Id, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Phụ kiện cao cấp");
        result.Value.IsActive.Should().BeFalse();

        var dbEntity = await dbContext.Categories.FindAsync(cat.Id);
        dbEntity!.Name.Should().Be("Phụ kiện cao cấp");
        dbEntity.IsActive.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task UpdateCategory_ShouldThrowValidationException_WhenNameIsInvalid(string? invalidName)
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var cat = Category.Create("Phụ kiện");
        dbContext.Categories.Add(cat);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);
        var request = new UpdateCategoryRequest(invalidName!, true);

        // Act
        var act = async () => await service.UpdateCategoryAsync(cat.Id, request);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task UpdateCategory_ShouldThrowValidationException_WhenNameExceeds100Characters()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var cat = Category.Create("Phụ kiện");
        dbContext.Categories.Add(cat);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);
        var longName = new string('B', 101);
        var request = new UpdateCategoryRequest(longName, true);

        // Act
        var act = async () => await service.UpdateCategoryAsync(cat.Id, request);

        // Assert
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors["Name"].Should().Contain(e => e.Contains("100"));
    }

    [Fact]
    public async Task UpdateCategory_ShouldThrowNotFoundException_WhenCategoryDoesNotExist()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new CatalogService(dbContext);
        var request = new UpdateCategoryRequest("Tên mới", true);

        // Act
        var act = async () => await service.UpdateCategoryAsync(99999, request);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateCategory_ShouldThrowConflictException_WhenDuplicateNameWithAnotherCategory()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var cat1 = Category.Create("Điện thoại");
        var cat2 = Category.Create("Laptop");
        dbContext.Categories.AddRange(cat1, cat2);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);
        var request = new UpdateCategoryRequest("laptop", true); // Try to rename cat1 to cat2's name

        // Act
        var act = async () => await service.UpdateCategoryAsync(cat1.Id, request);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task DeleteCategory_ShouldHardDelete_WhenNoProductsReferenceIt()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var cat = Category.Create("Danh mục rác");
        dbContext.Categories.Add(cat);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);

        // Act
        var result = await service.DeleteCategoryAsync(cat.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("thành công");

        var dbEntity = await dbContext.Categories.FindAsync(cat.Id);
        dbEntity.Should().BeNull();
    }

    [Fact]
    public async Task DeleteCategory_ShouldSoftDelete_WhenProductsReferenceIt()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var cat = Category.Create("Điện thoại");
        var brand = Brand.Create("Apple");
        dbContext.Categories.Add(cat);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var product = Product.Create(cat.Id, brand.Id, "iPhone 15 Pro", "Mô tả", "{}");
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);

        // Act
        var result = await service.DeleteCategoryAsync(cat.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("vô hiệu hóa");

        var dbEntity = await dbContext.Categories.FindAsync(cat.Id);
        dbEntity.Should().NotBeNull();
        dbEntity!.IsActive.Should().BeFalse();
    }

    // ==========================================
    // BRAND TESTS
    // ==========================================

    [Fact]
    public async Task GetBrands_ShouldReturnActiveBrandsOnly_ByDefault()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var b1 = Brand.Create("Apple");
        var b2 = Brand.Create("Blackberry");
        b2.Update("Blackberry", false);

        dbContext.Brands.AddRange(b1, b2);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);

        // Act
        var result = await service.GetBrandsAsync(includeInactive: false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("Apple");
    }

    [Fact]
    public async Task GetBrandById_ShouldReturnBrand_WhenIdExists()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var brand = Brand.Create("Samsung");
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);

        // Act
        var result = await service.GetBrandByIdAsync(brand.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(brand.Id);
        result.Value.Name.Should().Be("Samsung");
    }

    [Fact]
    public async Task GetBrandById_ShouldThrowNotFoundException_WhenIdDoesNotExist()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new CatalogService(dbContext);

        // Act
        var act = async () => await service.GetBrandByIdAsync(99999);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateBrand_ShouldCreateBrandSuccessfully()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new CatalogService(dbContext);
        var request = new CreateBrandRequest("Xiaomi");

        // Act
        var result = await service.CreateBrandAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Xiaomi");
        result.Value.IsActive.Should().BeTrue();

        var dbEntity = await dbContext.Brands.FindAsync(result.Value.Id);
        dbEntity.Should().NotBeNull();
        dbEntity!.Name.Should().Be("Xiaomi");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateBrand_ShouldThrowValidationException_WhenNameIsInvalid(string? invalidName)
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new CatalogService(dbContext);
        var request = new CreateBrandRequest(invalidName!);

        // Act
        var act = async () => await service.CreateBrandAsync(request);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateBrand_ShouldThrowValidationException_WhenNameExceeds100Characters()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new CatalogService(dbContext);
        var longName = new string('C', 101);
        var request = new CreateBrandRequest(longName);

        // Act
        var act = async () => await service.CreateBrandAsync(request);

        // Assert
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors["Name"].Should().Contain(e => e.Contains("100"));
    }

    [Fact]
    public async Task CreateBrand_ShouldThrowConflictException_WhenNameAlreadyExists()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var brand = Brand.Create("Sony");
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);
        var request = new CreateBrandRequest("SONY");

        // Act
        var act = async () => await service.CreateBrandAsync(request);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task UpdateBrand_ShouldUpdateBrandSuccessfully()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var brand = Brand.Create("Oppo");
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);
        var request = new UpdateBrandRequest("OPPO Global", true);

        // Act
        var result = await service.UpdateBrandAsync(brand.Id, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("OPPO Global");

        var dbEntity = await dbContext.Brands.FindAsync(brand.Id);
        dbEntity!.Name.Should().Be("OPPO Global");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task UpdateBrand_ShouldThrowValidationException_WhenNameIsInvalid(string? invalidName)
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var brand = Brand.Create("Oppo");
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);
        var request = new UpdateBrandRequest(invalidName!, true);

        // Act
        var act = async () => await service.UpdateBrandAsync(brand.Id, request);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task UpdateBrand_ShouldThrowValidationException_WhenNameExceeds100Characters()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var brand = Brand.Create("Oppo");
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);
        var longName = new string('D', 101);
        var request = new UpdateBrandRequest(longName, true);

        // Act
        var act = async () => await service.UpdateBrandAsync(brand.Id, request);

        // Assert
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors["Name"].Should().Contain(e => e.Contains("100"));
    }

    [Fact]
    public async Task DeleteBrand_ShouldHardDelete_WhenNoProductsReferenceIt()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var brand = Brand.Create("Brand Cũ");
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);

        // Act
        var result = await service.DeleteBrandAsync(brand.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("thành công");

        var dbEntity = await dbContext.Brands.FindAsync(brand.Id);
        dbEntity.Should().BeNull();
    }

    [Fact]
    public async Task DeleteBrand_ShouldSoftDelete_WhenProductsReferenceIt()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var cat = Category.Create("Điện thoại");
        var brand = Brand.Create("Apple");
        dbContext.Categories.Add(cat);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var product = Product.Create(cat.Id, brand.Id, "iPhone 16", "Mô tả", "{}");
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var service = new CatalogService(dbContext);

        // Act
        var result = await service.DeleteBrandAsync(brand.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("vô hiệu hóa");

        var dbEntity = await dbContext.Brands.FindAsync(brand.Id);
        dbEntity.Should().NotBeNull();
        dbEntity!.IsActive.Should().BeFalse();
    }
}
