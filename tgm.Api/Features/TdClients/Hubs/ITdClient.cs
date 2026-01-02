using TdLib;
using tgm.Api.Features.TdClients.DTOs;
using tgm.Api.Features.TdClients.Entities;
using tgm.Api.Features.TdClients.Enums;

namespace tgm.Api.Features.TdClients.Hubs;

public interface ITdClient
{
    Task ReceiveErrorMessageAsync(string errorMessage);

    Task ReceiveQrString(string qrString);
    Task ReceiveConfirmSuccessfulSignInAsync(bool isSuccess = true);


    // Chating
    Task ReceiveAccountsAsync(List<TgAccount> accounts);
    Task ReceiveChatAsync(Guid accountId, ChatDTO chat);
    Task ReceiveChatPhotoAsync(Guid accoundId, long chatId, byte[] photoData);
    Task ReceiveMessagesAsync(Guid accountId, long chatId, List<MessageDTO> messages);

    // Monitoring && Management
    Task ReceiveClientStateAsync(Guid accountId, TdClientState clientState);

}
