using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;
using tgm.Api.Features.TdClients.Services;

namespace tgm.Api.Features.TdClients.Hubs;

public class TdClientHub(
    TdClientManager _tdClientManager
    ) : Hub<ITdClient>
{
    private const string MONITORING_GROUP = "Monitoring";

    #region Connection Management
    public override async Task OnConnectedAsync()
    {
        // No specific action on connection
        Debug.WriteLine("User connected with id" + Context.ConnectionId);
    }
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Debug.WriteLine("User disconnected with id" + Context.ConnectionId);

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
        await Groups.AddToGroupAsync(Context.ConnectionId, MONITORING_GROUP);
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
        await service.GetChatMessagesAsync(chatId, 50, isFirstLoad);
    }
    #endregion
}
