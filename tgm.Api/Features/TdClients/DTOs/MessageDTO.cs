namespace tgm.Api.Features.TdClients.DTOs;

public record MessageDTO(
    long Id,
    string Content,
    int Date,
    bool IsOutgoing)
{ }
