using TdLib;
using tgm.Api.Abstractions.ConcurrectHashSet;
using tgm.Api.Features.TdClients.Options;

namespace tgm.Api.Features.TdClients.Services.Abtractions;

public abstract class TdClientAbstraction : IAsyncDisposable
{
    protected readonly TdClient _client = new();
    protected readonly ConcurrentHashSet<string> _connectionIds = [];
    protected readonly CancellationTokenSource _cts = new();

    protected abstract string FolderPath { get; }

    public TdClientAbstraction()
    {
        _ =_client.SetLogVerbosityLevelAsync(0);
    }

    protected void StartListening()
    {
        EnsureOrCreateIsDirExist();
        _client.UpdateReceived += OnUpdateReceived;
    }

    private void EnsureOrCreateIsDirExist()
    {
        // Ensure client-specific directory exists
        if (!Directory.Exists(FolderPath))
        {
            throw new Exception("Client folder not exist");
        }
    }
    protected (string, string) GetDirPaths()
        => (Path.Combine(FolderPath, "db"), Path.Combine(FolderPath, "files"));


    #region Updates Handling

    private void OnUpdateReceived(object? sender, TdApi.Update update)
    {
        switch (update)
        {
            case TdApi.Update.UpdateAuthorizationState authUpdate:
                HandleAuthStateUpdate(authUpdate);
                break;
            case TdApi.Update.UpdateOption optionsUpdate:
                HandleOptionsUpdate(optionsUpdate);
                break;
            case TdApi.Update.UpdateUser userUpdate:
                HandleUserUpdate(userUpdate);
                break;
            case TdApi.Update.UpdateUserStatus userStatusUpdate:
                HandleUserStatusUpdate(userStatusUpdate);
                break;
            case TdApi.Update.UpdateNewChat newChatUpdate:
                HandleNewChatUpdate(newChatUpdate);
                break;
            case TdApi.Update.UpdateChatPosition chatPositionUpdate:
                HandleChatPositionUpdate(chatPositionUpdate);
                break;
            case TdApi.Update.UpdateNewMessage newMessageUpdate:
                HandleNewMessageUpdate(newMessageUpdate);
                break;
            case TdApi.Update.UpdateChatPhoto chatPhotoUpdate:
                HandleChatPhotoUpdate(chatPhotoUpdate);
                break;

            // Files
            case TdApi.Update.UpdateFile fileUpdate:
                HandleFileUpdate(fileUpdate);
                break;
            case TdApi.Update.UpdateFileDownload fileDownloadUpdate:
                HandleFileDownloadUpdate(fileDownloadUpdate);
                break;

        }
    }


    protected virtual void HandleAuthStateUpdate(TdApi.Update.UpdateAuthorizationState authUpdate)
    {
        // Implementation in derived class
    }
    protected virtual void HandleNewChatUpdate(TdApi.Update.UpdateNewChat newChatUpdate)
    {
        // Implementation in derived class
    }
    protected virtual void HandleChatPositionUpdate(TdApi.Update.UpdateChatPosition chatPositionUpdate)
    {
        // Implementation in derived class
    }
    protected virtual void HandleNewMessageUpdate(TdApi.Update.UpdateNewMessage newMessageUpdate)
    {
        // Implementation in derived class
    }
    protected virtual void HandleChatPhotoUpdate(TdApi.Update.UpdateChatPhoto chatPhotoUpdate)
    {
        // Implementation in derived class
    }
    protected virtual void HandleOptionsUpdate(TdApi.Update.UpdateOption optionsUpdate)
    {
        // Implementation in derived class
    }
    protected virtual void HandleUserUpdate(TdApi.Update.UpdateUser userUpdate)
    {
        // Implementation in derived class
    }
    protected virtual void HandleUserStatusUpdate(TdApi.Update.UpdateUserStatus userStatusUpdate)
    {
        // Implementation in derived class
    }

    // Files
    protected virtual void HandleFileUpdate(TdApi.Update.UpdateFile fileUpdate)
    {
        // Implementation in derived class
    }
    protected virtual void HandleFileDownloadUpdate(TdApi.Update.UpdateFileDownload fileDownloadUpdate)
    {
        // Implementation in derived class
    }

    #endregion

    #region Connections Management
    public int GetConnectionCount() => _connectionIds.Count;
    public bool AddConnection(string connectionId)
        => _connectionIds.TryAdd(connectionId);
    public bool RemoveConnection(string connectionId)
        => _connectionIds.TryRemove(connectionId);

    #endregion

    #region Dispose
    private readonly Lock _disposedLock = new();
    private bool _isDisposed;

    public async ValueTask DisposeAsync()
    {
        await DisposeAsync(true);
        GC.SuppressFinalize(this);
    }
    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        lock (_disposedLock)
        {
            if (_isDisposed)
            {
                return;
            }
            _isDisposed = true;
        }

        if (disposing)
        {
            await _cts.CancelAsync();

            _client.UpdateReceived -= OnUpdateReceived;
            await _client.CloseAsync();
            _client?.Dispose();
        }
    }

    #endregion
}