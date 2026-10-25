using PhoneStore.BuildingBlocks.Domain;

namespace PhoneStore.Modules.Catalog.Domain.Entities;

public class Category : BaseEntity<long>
{
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    protected Category() { }

    public static Category Create(string name)
    {
        return new Category
        {
            Name = name.Trim(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Update(string name, bool isActive)
    {
        Name = name.Trim();
        IsActive = isActive;
        Touch();
    }
}

public class Brand : BaseEntity<long>
{
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    protected Brand() { }

    public static Brand Create(string name)
    {
        return new Brand
        {
            Name = name.Trim(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Update(string name, bool isActive)
    {
        Name = name.Trim();
        IsActive = isActive;
        Touch();
    }
}
