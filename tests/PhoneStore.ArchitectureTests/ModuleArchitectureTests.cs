using FluentAssertions;
using PhoneStore.Modules.Cart;
using PhoneStore.Modules.Catalog;
using PhoneStore.Modules.Identity;
using PhoneStore.Modules.Order;
using Xunit;

namespace PhoneStore.ArchitectureTests;

public class ModuleArchitectureTests
{
    [Fact]
    public void IdentityModule_ShouldNotReference_OtherModules()
    {
        var identityAssembly = typeof(IdentityModuleRegistration).Assembly;
        var referencedAssemblies = identityAssembly.GetReferencedAssemblies().Select(a => a.Name);

        referencedAssemblies.Should().NotContain("PhoneStore.Modules.Catalog");
        referencedAssemblies.Should().NotContain("PhoneStore.Modules.Cart");
        referencedAssemblies.Should().NotContain("PhoneStore.Modules.Order");
        referencedAssemblies.Should().NotContain("PhoneStore.Modules.Inventory");
    }

    [Fact]
    public void CatalogModule_ShouldNotReference_OtherModules()
    {
        var catalogAssembly = typeof(CatalogModuleRegistration).Assembly;
        var referencedAssemblies = catalogAssembly.GetReferencedAssemblies().Select(a => a.Name);

        referencedAssemblies.Should().NotContain("PhoneStore.Modules.Identity");
        referencedAssemblies.Should().NotContain("PhoneStore.Modules.Cart");
        referencedAssemblies.Should().NotContain("PhoneStore.Modules.Order");
        referencedAssemblies.Should().NotContain("PhoneStore.Modules.Inventory");
    }

    [Fact]
    public void CartModule_ShouldNotReference_OtherModules()
    {
        var cartAssembly = typeof(CartModuleRegistration).Assembly;
        var referencedAssemblies = cartAssembly.GetReferencedAssemblies().Select(a => a.Name);

        referencedAssemblies.Should().NotContain("PhoneStore.Modules.Identity");
        referencedAssemblies.Should().NotContain("PhoneStore.Modules.Catalog");
        referencedAssemblies.Should().NotContain("PhoneStore.Modules.Order");
        referencedAssemblies.Should().NotContain("PhoneStore.Modules.Inventory");
    }

    [Fact]
    public void OrderModule_ShouldNotReference_OtherModules()
    {
        var orderAssembly = typeof(OrderModuleRegistration).Assembly;
        var referencedAssemblies = orderAssembly.GetReferencedAssemblies().Select(a => a.Name);

        referencedAssemblies.Should().NotContain("PhoneStore.Modules.Identity");
        referencedAssemblies.Should().NotContain("PhoneStore.Modules.Catalog");
        referencedAssemblies.Should().NotContain("PhoneStore.Modules.Cart");
        referencedAssemblies.Should().NotContain("PhoneStore.Modules.Inventory");
    }
}
