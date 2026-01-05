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
    private readonly TdOptions _tdOptions;
    private readonly ILogger<TdAccountClient> _logger;
    private readonly TdClientStateManager _stateManager;

    private readonly ConcurrentDictionary<string, TaskCompletionSource> _pending = [];

    private readonly TgAccount _account;

    protected override string FolderPath { get; }

    public TdAccountClient(
        IServiceScopeFactory serviceScopeFactory,
        TgAccount tgAccount,
        string[] connectionIds)
    {
        _account = tgAccount;

        var scope = serviceScopeFactory.CreateScope();
        _hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<TdClientHub, ITdClient>>();
        _tdOptions = scope.ServiceProvider.GetRequiredService<IOptions<TdOptions>>().Value;
        _logger = scope.ServiceProvider.GetRequiredService<ILogger<TdAccountClient>>();
        _stateManager = scope.ServiceProvider.GetRequiredService<TdClientStateManager>();

        _ = _stateManager.UpdateClientStateAsync(_account.Id, TdClientState.Starting);

        FolderPath = Path.Combine(_tdOptions.Directory, _account.PhoneNumber);
        Array.ForEach(connectionIds, x => AddConnection(x));

        _pending["isReadyState"] = new();

        try
        {
            StartListening();
        }
        catch (Exception ex)
        {
            _ = _stateManager.UpdateClientStateAsync(_account.Id, TdClientState.Error);
            _logger.LogError(ex, "Error starting TdAccountClient for account {AccountName}", _account.FirstName);
        }
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
                _logger.LogInformation("Account: {AccountName} is ready", _account.FirstName);
                var tsc = _pending["isReadyState"];
                tsc.SetResult();

                _ = _stateManager.UpdateClientStateAsync(_account.Id, TdClientState.Running);
                break;
        }
    }
    protected override void HandleNewChatUpdate(TdApi.Update.UpdateNewChat newChatUpdate)
    {
        var newChat = newChatUpdate.Chat;

        _hubContext.Clients
            .Clients(_connectionIds)
            .ReceiveChatAsync(_account.Id, new ChatDTO(newChat.Id, newChat.Title))
            .Wait();

        try
        {
            if (newChat.Photo?.Small is not TdApi.File file)
            {
                return;
            }

            if (file.Local.IsDownloadingCompleted)
            {
                var fileData = File.ReadAllBytes(file.Local.Path);

                _ = _hubContext.Clients
                    .Clients(_connectionIds)
                    .ReceiveChatPhotoAsync(_account.Id, newChat.Id, fileData);
            }
            else
            {
                _ = Task.Run(async () =>
                {
                    var downloadedFile = await _client.DownloadFileAsync(file.Id, priority: 1, synchronous: true);
                    var fileData = File.ReadAllBytes(downloadedFile.Local.Path);

                    await _hubContext.Clients
                        .Clients(_connectionIds)
                        .ReceiveChatPhotoAsync(_account.Id, newChat.Id, fileData);
                });
            }

        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error downloading chat photo for chat {ChatId} for account {AccountName}", newChat.Id, _account.FirstName);
        }


    }


    #endregion


    #region Public Methods
    public async Task GetChatsAsync()
    {
        try
        {
            if (_pending.TryGetValue("isReadyState", out var isReadyStateTsc))
            {
                await isReadyStateTsc.Task.WaitAsync(_cts.Token);
            }

            _logger.LogInformation("Execute request chats command for account: {AccountName}", _account.FirstName);

            var res = await _client.LoadChatsAsync(chatList: null, limit: 100);

            _logger.LogInformation("Successful executed request to receiving chats for account: {AccountName} in count: {ChatCount}", _account.FirstName, res.Extra);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogInformation(ex, "GetChatsAsync operation was cancelled for account: {AccountName}", _account.FirstName);
        }
        catch (TdException ex)
        {
            _logger.LogError(ex, "Error getting chats for account: {AccountName}", _account.FirstName);
        }
        catch(Exception)
        {

        }
        finally
        {
            await _hubContext.Clients
                .Clients(_connectionIds)
                .ReceiveErrorMessageAsync($"Failure to get chats for account: {_account.FirstName}");
        }
    }
    public async Task GetMessagesAsync(long chatId, int limit = 50, bool isFirstLoad = false)
    {

        try
        {
            if (_pending.TryGetValue("isReadyState", out var isReadyStateTsc))
            {
                await isReadyStateTsc.Task.WaitAsync(_cts.Token);
            }


            var messages = await _client.GetChatHistoryAsync(
                    chatId: chatId,
                    fromMessageId: 0,      // 0 = починаємо з останнього
                    offset: 0,             // 0 = без зсуву
                    limit: limit,          // Кількість повідомлень
                    onlyLocal: false       // Завантажуємо з сервера
                ).WaitAsync(_cts.Token);

            if (!isFirstLoad)
            {
                messages = await _client.GetChatHistoryAsync(
                   chatId: chatId,
                   fromMessageId: 0,      // 0 = починаємо з останнього
                   offset: 0,             // 0 = без зсуву
                   limit: limit,          // Кількість повідомлень
                   onlyLocal: false       // Завантажуємо з сервера
               ).WaitAsync(_cts.Token);
            }

            //_logger.LogInformation("Received messages for account: {AccountName}, chat: {ChatId} count: {MessageCount}", _account.FirstName, chatId, messages.TotalCount); 

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
        catch (OperationCanceledException ex)
        {
            _logger.LogInformation(ex, "GetMessageAsync operation was cancelled for account: {AccountName}", _account.FirstName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting messages for chat {ChatId} for account {AccountName}", chatId, _account.FirstName);

            await _hubContext.Clients
                .Clients(_connectionIds)
                .ReceiveErrorMessageAsync($"Error getting messages: {ex.Message}");
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



    #endregion


    #region Dispose

    private readonly Lock _disposedLock = new();
    private bool _isDisposed;
    protected override async ValueTask DisposeAsync(bool disposing)
    {
        lock (_disposedLock)
        {
            if (_isDisposed)
                return;
            _isDisposed = true;
        }

        if (disposing)
        {
            await _stateManager.RemoveClientStateAsync(_account.Id);
        }

        await base.DisposeAsync(disposing);
    }
    #endregion

}
