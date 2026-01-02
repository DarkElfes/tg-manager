using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Diagnostics;
using tgm.Api.Database;
using tgm.Api.Features.TdClients.Hubs;
using tgm.Api.Features.TdClients.Services.TdClients;

namespace tgm.Api.Features.TdClients.Services;

public class TdClientManager(
    IServiceScopeFactory _serviceScopeFactory,
    IHubContext<TdClientHub, ITdClient> _hubContext,
    ILogger<TdClientManager> _logger
    )
{
    private readonly ConcurrentDictionary<string, TdManageClient> _tdManageServices = [];
    private readonly ConcurrentDictionary<Guid, TdAccountClient> _tdAccountServices = [];


    #region QR Stream Managment
    public async Task ConnectToQrStreamAsync(string connectionId)
    {
        var service = new TdManageClient(_serviceScopeFactory, connectionId);
        _tdManageServices.TryAdd(connectionId, service);
    }
    public async Task DisconnectFromQrStreamAsync(string connectionId)
    {
        if (_tdManageServices.GetValueOrDefault(connectionId) is TdManageClient existingService)
        {
            await existingService.DisposeAsync();
            _tdManageServices.TryRemove(connectionId, out _);
        }
    }
    #endregion

    #region Accounts Stream Managment
    public async Task ConnectToAccountsStreamAsync(string connectionId)
    {
        _logger.LogInformation("Connecting to accounts stream for connectionId: {ConnectionId}", connectionId);

        var accounts = GetDbContext().TgAccounts.ToList();

        if (accounts.Count == 0)
        {
            return;
        }


        await _hubContext.Clients.Client(connectionId).ReceiveAccountsAsync(accounts);

        foreach (var tgAccount in accounts)
        {
            if (!_tdAccountServices.TryGetValue(tgAccount.Id, out var service))
            {
                _logger.LogInformation("Creating new TdAccountService for account with name: {AccountName} and id: {AccountId}", tgAccount.FirstName, tgAccount.Id);
                service = new TdAccountClient(_serviceScopeFactory, tgAccount, [connectionId]);
                _tdAccountServices.TryAdd(tgAccount.Id, service);
            }
            else
            {
                service.AddUserConnection(connectionId);
            }

            var _ = service.GetChatsAsync();
        }
    }
    public async Task DisconnectFromAccountsSrteamAsync(string connectionId)
    {
        var accounts = GetDbContext().TgAccounts.ToList();

        foreach (var account in accounts)
        {
            if (_tdAccountServices.TryGetValue(account.Id, out var service))
            {
                // Remove currect user connection from service connections
                service.RemoveUserConnection(connectionId);

                // If current user was last in collection - stop this service
                if (service.GetConnectionCount() == 0)
                {
                    await service.DisposeAsync();
                    _tdAccountServices.Remove(account.Id, out _);
                    _logger.LogInformation("Disposing TdAccountService for accountId: {AccountId}", account.Id);
                }
            }
        }
    }
    public TdAccountClient? GetService(Guid accountId)
    {
        if (_tdAccountServices.TryGetValue(accountId, out var service))
        {
            return service;
        }
        return null;
    }
    #endregion

    #region Additional
    private AppDbContext GetDbContext()
        => _serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();
    #endregion
}
