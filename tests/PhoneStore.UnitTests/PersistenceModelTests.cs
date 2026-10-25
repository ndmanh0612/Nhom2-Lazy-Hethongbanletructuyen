using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Modules.Cart.Infrastructure.Persistence;
using PhoneStore.Modules.Order.Infrastructure.Persistence;
using Xunit;

namespace PhoneStore.UnitTests;

public class PersistenceModelTests
{
    [Fact]
    public void CartDbContext_ShouldBuildModel_WithoutConfigurationError()
    {
        using var dbContext = CreateCartDbContext();

        var model = dbContext.Model;

        model.FindEntityType(typeof(PhoneStore.Modules.Cart.Domain.Entities.Cart)).Should().NotBeNull();
        model.FindEntityType(typeof(PhoneStore.Modules.Cart.Domain.Entities.CartItem)).Should().NotBeNull();
        model.FindEntityType(typeof(PhoneStore.Modules.Cart.Domain.Entities.Cart))!.GetSchema().Should().Be(CartDbContext.Schema);
    }

    [Fact]
    public void OrderDbContext_ShouldBuildModel_WithoutConfigurationError()
    {
        using var dbContext = CreateOrderDbContext();

        var model = dbContext.Model;

        model.FindEntityType(typeof(PhoneStore.Modules.Order.Domain.Entities.Order)).Should().NotBeNull();
        model.FindEntityType(typeof(PhoneStore.Modules.Order.Domain.Entities.OrderItem)).Should().NotBeNull();
        model.FindEntityType(typeof(PhoneStore.Modules.Order.Domain.Entities.OrderStatusHistory)).Should().NotBeNull();
        model.FindEntityType(typeof(PhoneStore.Modules.Order.Domain.Entities.Order))!.GetSchema().Should().Be(OrderDbContext.Schema);
    }

    private static CartDbContext CreateCartDbContext()
    {
        var options = new DbContextOptionsBuilder<CartDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=phonestore_db;Username=postgres;Password=postgres")
            .Options;

        return new CartDbContext(options);
    }

    private static OrderDbContext CreateOrderDbContext()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=phonestore_db;Username=postgres;Password=postgres")
            .Options;

        return new OrderDbContext(options);
    }
}
