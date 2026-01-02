using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using TdLib;
using tgm.Api.Features.TdClients.DTOs;
using tgm.Api.Features.TdClients.Entities;
using tgm.Api.Features.TdClients.Enums;
using tgm.Api.Features.TdClients.Hubs;
using tgm.Api.Features.TdClients.Options;
using tgm.Api.Features.TdClients.Services.Abtractions;

namespace tgm.Api.Features.TdClients.Services.TdClients;

public class TdAccountClient : TdClientAbstraction, IAsyncDisposable
{
    private readonly IHubContext<TdClientHub, ITdClient> _hubContext;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly TdOptions _tdOptions;
    private readonly ILogger<TdAccountClient> _logger;

    private readonly ConcurrentDictionary<string, TaskCompletionSource> _pending = [];

    private readonly TgAccount _account;

    protected override string FolderPath { get; }

    public TdAccountClient(
        IServiceScopeFactory serviceScopeFactory,
        TgAccount tgAccount,
        string[] connectionIds)
    {
        (_serviceScopeFactory, _account) = (serviceScopeFactory, tgAccount);

        var scope = _serviceScopeFactory.CreateScope();
        _hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<TdClientHub, ITdClient>>();
        _tdOptions = scope.ServiceProvider.GetRequiredService<IOptions<TdOptions>>().Value;
        _logger = scope.ServiceProvider.GetRequiredService<ILogger<TdAccountClient>>();

        _ = _hubContext.Clients.Groups("Monitoring").ReceiveClientStateAsync(_account.Id, TdClientState.Starting);

        FolderPath = Path.Combine(_tdOptions.Directory, _account.PhoneNumber);
        Array.ForEach(connectionIds, x => _connectionIds.TryAdd(x));

        EnsureOrCreateIsDirExist();

        _pending["isReadyState"] = new();
    }

    #region Update Handlers

    protected override void HandleAuthStateUpdate(TdApi.Update.UpdateAuthorizationState authUpdate)
    {
        switch (authUpdate.AuthorizationState)
        {
            case TdApi.AuthorizationState.AuthorizationStateWaitTdlibParameters:
                var (dbPath, filesPath) = GetDirPaths();

                _ = _client.ExecuteAsync(new TdApi.SetTdlibParameters()
                {
                    DatabaseDirectory = dbPath,
                    FilesDirectory = filesPath,
                    ApiId = _tdOptions.ApiId,
                    ApiHash = _tdOptions.ApiHash,
                    SystemLanguageCode = "en",

                    DeviceModel = "Server",
                    SystemVersion = "ASP.NET Core",
                    ApplicationVersion = "1.0",
                });
                _logger.LogInformation("Send tdlibParameters for account: {AccountName}", _account.FirstName);
                break;
            case TdApi.AuthorizationState.AuthorizationStateWaitPhoneNumber:
                throw new Exception("account signed out");
            case TdApi.AuthorizationState.AuthorizationStateReady authStateReady:
                var tsc = _pending["isReadyState"];
                tsc.SetResult();
                _logger.LogInformation("Account: {AccountName} is ready", _account.FirstName);
                _ = _hubContext.Clients.Groups("Monitoring").ReceiveClientStateAsync(_account.Id, TdClientState.Running);
                break;
            default:
                //throw new Exception(authUpdate.AuthorizationState.GetType().ToString());
                break;
        }
    }
    protected override void HandleNewChatUpdate(TdApi.Update.UpdateNewChat newChatUpdate)
    {
        var newChat = newChatUpdate.Chat;
        _ = _hubContext.Clients.Clients(_connectionIds).ReceiveChatAsync(_account.Id, new ChatDTO(newChat.Id, newChat.Title));

        try
        {
            if (newChat.Photo?.Small is TdApi.File file)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    var fileData = File.ReadAllBytes(file.Local.Path);

                    _hubContext.Clients.Clients(_connectionIds).ReceiveChatPhotoAsync(_account.Id, newChat.Id, fileData);
                }
                else
                {
                    _ = Task.Run(async () =>
                    {
                        var downloadedFile = await _client.DownloadFileAsync(file.Id, priority: 1, synchronous: true);
                        var fileData = File.ReadAllBytes(downloadedFile.Local.Path);
                        
                        await _hubContext.Clients.Clients(_connectionIds).ReceiveChatPhotoAsync(_account.Id, newChat.Id, fileData);
                    });
                    //_ = _client.DownloadFileAsync(file.Id, 1);
                }
            }
        }
        catch (Exception e)
        {

            _logger.LogError(e, "Error downloading chat photo for chat {ChatId} for account {AccountName}", newChat.Id, _account.FirstName);
        }


    }

    protected override void HandleFileUpdate(TdApi.Update.UpdateFile fileUpdate)
    {
        //var file = fileUpdate.File;
        //if (fileUpdate.File.Local.IsDownloadingCompleted)
        //{

        //    var fileData = File.ReadAllBytes(file.Local.Path);

        //    _hubContext.Clients.Clients(_connectionIds).ReceiveChatPhotoAsync(_account.Id, 1, fileData);

        //}
    }
    protected override void HandleFileDownloadUpdate(TdApi.Update.UpdateFileDownload fileDownloadUpdate)
    {
        base.HandleFileDownloadUpdate(fileDownloadUpdate);
    }

    #endregion


    #region Additional Methods
    private void EnsureOrCreateIsDirExist()
    {
        // Ensure base directory exists
        if (!Directory.Exists(_tdOptions.Directory))
        {
            Directory.CreateDirectory(_tdOptions.Directory);
        }

        // Ensure client-specific directory exists
        if (!Directory.Exists(FolderPath))
        {
            throw new Exception("Client folder not exist");
        }
    }
    private (string, string) GetDirPaths()
        => (Path.Combine(FolderPath, "db"), Path.Combine(FolderPath, "files"));

    #endregion


    #region Public Methods
    public async Task GetChatsAsync()
    {
        _logger.LogInformation("Start loading chats process for account: {AccountName}", _account.FirstName);
        if (_pending.TryGetValue("isReadyState", out var isReadyStateTsc))
        {
            await isReadyStateTsc.Task;
        }

        try
        {
            _logger.LogInformation("Execute loading chats command for account: {AccountName}", _account.FirstName);
            var count = await _client.LoadChatsAsync(chatList: null, limit: 100);

            _logger.LogInformation("Successful executed request to receiving chats for account: {AccountName} in count: {ChatCount}", _account.FirstName, count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chats for account {AccountName}", _account.FirstName);
            await _hubContext.Clients.Clients(_connectionIds).ReceiveErrorMessageAsync($"Error gettings chats: {ex.Message}");
        }
    }
    public async Task GetChatMessagesAsync(long chatId, int limit = 50, bool isFirstLoad = false)
    {
        //_logger.LogInformation("Start loading messages for chat {ChatId} for account {AccountName}", chatId, _account.FirstName);
        if (_pending.TryGetValue("isReadyState", out var isReadyStateTsc))
        {
            await isReadyStateTsc.Task;
        }

        try
        {
            //await _client.OpenChatAsync(chatId);


            var messages = await _client.GetChatHistoryAsync(
                    chatId: chatId,
                    fromMessageId: 0,      // 0 = починаємо з останнього
                    offset: 0,             // 0 = без зсуву
                    limit: limit,          // Кількість повідомлень
                    onlyLocal: false       // Завантажуємо з сервера
                );

            if (!isFirstLoad)
            {
                messages = await _client.GetChatHistoryAsync(
                   chatId: chatId,
                   fromMessageId: 0,      // 0 = починаємо з останнього
                   offset: 0,             // 0 = без зсуву
                   limit: limit,          // Кількість повідомлень
                   onlyLocal: false       // Завантажуємо з сервера
               );
            }

            _logger.LogInformation("Received messages for account: {AccountName}, chat: {ChatId} count: {MessageCount}", _account.FirstName, chatId, messages.TotalCount); 

            var messagesDtos = messages?.Messages_?.Select(x => new MessageDTO(
                x.Id,
                FormatMessageContent(x.Content),
                x.Date,
                x.IsOutgoing
                )).ToList();

            await _hubContext.Clients
                .Clients(_connectionIds)
                .ReceiveMessagesAsync(_account.Id, chatId, messagesDtos!);


        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting messages for chat {ChatId} for account {AccountName}", chatId, _account.FirstName);
            await _hubContext.Clients.Clients(_connectionIds).ReceiveErrorMessageAsync($"Error getting messages: {ex.Message}");
        }
    }


    private static string FormatMessageContent(TdApi.MessageContent content)
    {
        return content switch
        {
            TdApi.MessageContent.MessageText text => text.Text.Text,
            TdApi.MessageContent.MessagePhoto photo => $"[Photo] {photo.Caption.Text}",
            TdApi.MessageContent.MessageVideo video => $"[Video] {video.Caption.Text}",
            TdApi.MessageContent.MessageDocument doc => $"[Document: {doc.Document.FileName}]",
            TdApi.MessageContent.MessageSticker sticker => $"[Sticker: {sticker.Sticker.Emoji}]",
            TdApi.MessageContent.MessageVoiceNote voice => "[Voice message]",
            TdApi.MessageContent.MessageAudio audio => $"[Audio: {audio.Audio.Title}]",
            TdApi.MessageContent.MessageAnimation animation => "[GIF]",
            TdApi.MessageContent.MessageLocation location => $"[Location: {location.Location.Latitude}, {location.Location.Longitude}]",
            TdApi.MessageContent.MessageContact contact => $"[Contact: {contact.Contact.PhoneNumber}]",
            TdApi.MessageContent.MessagePoll poll => $"[Poll: {poll.Poll.Question}]",
            _ => $"[{content.GetType().Name}]"
        };
    }

    public int GetConnectionCount() => _connectionIds.Count;
    public void AddUserConnection(string connectionId)
    {
        if (_connectionIds.TryAdd(connectionId))
        {
            _logger.LogInformation("Connection: {ConnectionId} added to account: {AccountName}", connectionId, _account.FirstName);
        }
        else
        {
            _logger.LogWarning("Failure to add connection: {ConnectionId} to account {AccountName}", connectionId, _account.FirstName);
        }
    }
    public void RemoveUserConnection(string connectionId)
    {
        if (_connectionIds.TryRemove(connectionId))
        {
            _logger.LogInformation("Connection: {ConnectionId} removed from account: {AccountName}", connectionId, _account.FirstName);
        }
    }

    #endregion


    #region Dispose

    //~TdAccountClient()
    //{
    //    _ = DisposeAsync(false);
    //}
    protected override async ValueTask DisposeAsync(bool disposing)
    {
        await base.DisposeAsync(disposing);
        _ = _hubContext.Clients.Groups("Monitoring").ReceiveClientStateAsync(_account.Id, TdClientState.Stopped);
    }
    #endregion

}
