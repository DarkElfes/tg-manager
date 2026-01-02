using tgm.Api.Features.Shared;

namespace tgm.Api.Features.TdClients.Entities;

public class TgAccount(
    string phoneNumber,
    string firstName,
    string? lastName
    ) : Entity
{
    public string PhoneNumber { get; set; } = phoneNumber;
    public string FirstName { get; set; } = firstName;
    public string? LastName { get; set; } = lastName;
}
