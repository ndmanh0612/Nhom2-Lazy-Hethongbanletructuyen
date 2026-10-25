using FluentAssertions;
using PhoneStore.Modules.Catalog;
using PhoneStore.Modules.Identity;
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
}
