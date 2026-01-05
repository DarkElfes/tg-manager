using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using tgm.Api.Features.TdClients.Enums;
using tgm.Api.Features.TdClients.Hubs;

namespace tgm.Api.Features.TdClients.Services;

public class TdClientStateManager(
    ILogger<TdClientStateManager> _logger,
    IHubContext<TdClientHub, ITdClient> _hubContext
    )
{
    private readonly ConcurrentDictionary<Guid, TdClientState> _clientStates = [];

    /// <summary>
    /// Updates the state of a client account.
    /// </summary>
    public async Task UpdateClientStateAsync(Guid accountId, TdClientState state)
    {
        var oldState = _clientStates.TryGetValue(accountId, out var existing) ? existing : TdClientState.Stopped;

        _clientStates.AddOrUpdate(accountId, state, (_, _) => state);

        _logger.LogInformation(
            "State changed for account {AccountId}: {OldState} -> {NewState}",
            accountId,
            oldState,
            state);

        await _hubContext.Clients
            .Groups(TdClientsConstants.Groups.Monitoring)
            .ReceiveClientStateAsync(accountId, state);
    }

    /// <summary>
    /// Gets the current state of a specific client account.
    /// </summary>
    public TdClientState GetClientState(Guid accountId)
    {
        return _clientStates.TryGetValue(accountId, out var state) ? state : TdClientState.Stopped;
    }

    /// <summary>
    /// Gets all client states.
    /// </summary>
    public Dictionary<Guid, TdClientState> GetAllClientStates()
    {
        return new Dictionary<Guid, TdClientState>(_clientStates);
    }

    /// <summary>
    /// Removes state tracking for an account.
    /// </summary>
    public async Task RemoveClientStateAsync(Guid accountId)
    {
        if (_clientStates.TryRemove(accountId, out _))
        {
            _logger.LogInformation("Client state removed for accountId: {AccountId}", accountId);

            await _hubContext.Clients
                .Groups(TdClientsConstants.Groups.Monitoring)
                .ReceiveClientStateAsync(accountId, TdClientState.Stopped);
        }
    }


}
