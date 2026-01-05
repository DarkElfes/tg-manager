using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;
using tgm.Api.Features.TdClients.Services;

namespace tgm.Api.Features.TdClients.Hubs;

public class TdClientHub(
    TdClientManager _tdClientManager,
    ILogger<TdClientHub> _logger
    ) : Hub<ITdClient>
{
    #region Connection Management
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("User connected with id: {ConnectionId}", Context.ConnectionId);
    }
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("User disconnected with id: {ConnectionId}", Context.ConnectionId);

        string connectionId = Context.ConnectionId;
        await _tdClientManager.DisconnectFromQrStreamAsync(connectionId);
        await _tdClientManager.DisconnectFromAccountsSrteamAsync(connectionId);
    }
    #endregion

    #region QR Stream Management
    public async Task ConnectToQrStreamAsync()
        => await _tdClientManager.ConnectToQrStreamAsync(Context.ConnectionId);
    #endregion


    #region Chats Monitoring
    public async Task ConnectToMonitoring()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, TdClientsConstants.Groups.Monitoring);
    }
    #endregion

    #region Chats Management
    public async Task ConnectToAccountsStreamAsync()
    {
        await _tdClientManager.ConnectToAccountsStreamAsync(Context.ConnectionId);
    }
    public async Task LoadMessagesAsync(Guid accountId, long chatId, bool isFirstLoad)
    {
        var service = _tdClientManager.GetService(accountId) ?? throw new Exception("Account service not found");
        await service.GetMessagesAsync(chatId, 50, isFirstLoad);
    }
    #endregion
}
