namespace PhoneStore.Modules.Order.Domain.Enums;

public enum OrderStatus
{
    PENDING,
    CONFIRMED,
    SHIPPING,
    DELIVERED,
    DELIVERY_FAILED,
    CANCELLED
}
