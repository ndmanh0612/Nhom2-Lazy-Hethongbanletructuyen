namespace PhoneStore.Modules.Order.Domain.Entities;

public record OrderItemDraft(
    long ProductVariantId,
    string ProductName,
    string Sku,
    long UnitPrice,
    int Quantity);
