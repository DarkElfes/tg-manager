using tgm.Api.Features.TdClients.Enums;

namespace tgm.Api.Features.TdClients.DTOs;

public record AccountDTO(
    Guid Id,
    string FirstName,
    string PhoneNumber,
    TdClientState State
    )
{ }