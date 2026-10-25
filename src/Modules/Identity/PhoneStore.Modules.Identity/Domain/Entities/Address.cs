using PhoneStore.BuildingBlocks.Domain;

namespace PhoneStore.Modules.Identity.Domain.Entities;

public class Address : BaseEntity<long>
{
    public long UserId { get; private set; }
    public string ReceiverName { get; private set; } = string.Empty;
    public string ReceiverPhone { get; private set; } = string.Empty;
    public string AddressText { get; private set; } = string.Empty;

    protected Address() { }

    public static Address Create(long userId, string receiverName, string receiverPhone, string addressText)
    {
        return new Address
        {
            UserId = userId,
            ReceiverName = receiverName.Trim(),
            ReceiverPhone = receiverPhone.Trim(),
            AddressText = addressText.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Update(string receiverName, string receiverPhone, string addressText)
    {
        ReceiverName = receiverName.Trim();
        ReceiverPhone = receiverPhone.Trim();
        AddressText = addressText.Trim();
        Touch();
    }
}
