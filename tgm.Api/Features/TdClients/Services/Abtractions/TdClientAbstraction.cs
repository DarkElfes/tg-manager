using System.Diagnostics;
using TdLib;
using tgm.Api.Abstractions.ConcurrectHashSet;

namespace tgm.Api.Features.TdClients.Services.Abtractions;

public abstract class TdClientAbstraction : IAsyncDisposable
{
    protected readonly TdClient _client;
    protected readonly ConcurrentHashSet<string> _connectionIds = [];
    protected abstract string FolderPath { get; }

    private bool _disposed;

    public TdClientAbstraction()
    {
        _client = new();
        _client.SetLogVerbosityLevelAsync(0);
        _client.UpdateReceived += OnUpdateReceived;
    }


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
        Debug.WriteLine(fileUpdate); 
    }
    protected virtual void HandleFileDownloadUpdate(TdApi.Update.UpdateFileDownload fileDownloadUpdate)
    {
        // Implementation in derived class
    }

    #endregion

    #region Dispose

    public async ValueTask DisposeAsync()
    {
        await DisposeAsync(true);
        GC.SuppressFinalize(this);
    }
    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _client.UpdateReceived -= OnUpdateReceived;
            _client?.Dispose();
        }


        _disposed = true;
    }

    #endregion
}