using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using TdLib;
using tgm.Api.Database;
using tgm.Api.Features.TdClients.Entities;
using tgm.Api.Features.TdClients.Hubs;
using tgm.Api.Features.TdClients.Options;
using static TdLib.TdApi;
using static TdLib.TdApi.Update;

namespace tgm.Api.Features.TdClients.Services.TdClients;

public class TdManageClient : IAsyncDisposable
{
    private readonly IHubContext<TdClientHub, ITdClient> _hubContext;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly TdOptions _tdOptions;

    private readonly string _connectionId;
    private readonly string _folderPath;
    private readonly TdClient _client = new();

    private TaskCompletionSource? _authorizationTcs;
    private bool _disposed;
    private bool _isSuccessful;


    public TdManageClient(
        IServiceScopeFactory serviceScopeFactory,
        string connectionId
        )
    {
        _serviceScopeFactory = serviceScopeFactory;

        var scope = serviceScopeFactory.CreateScope();
        _hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<TdClientHub, ITdClient>>();
        _tdOptions = scope.ServiceProvider.GetRequiredService<IOptions<TdOptions>>().Value;


        _connectionId = connectionId;
        _folderPath = Path.Combine(_tdOptions.Directory, _connectionId);
        EnsureOrCreateIsDirExist();

        // Subscribe to client updates
        _client.UpdateReceived += HandleUpdate;
    }



    private void HandleUpdate(object? sender, Update update)
    {
        switch (update)
        {
            case UpdateAuthorizationState authUpdate:
                HandleAuthUpdate(authUpdate);
                break;
            default:
                break;
        }
    }
    private void HandleAuthUpdate(UpdateAuthorizationState authUpdate)
    {
        switch (authUpdate.AuthorizationState)
        {
            case TdApi.AuthorizationState.AuthorizationStateWaitOtherDeviceConfirmation qrState:
                _hubContext.Clients.Client(_connectionId).ReceiveQrString(qrState.Link);
                break;
            case TdApi.AuthorizationState.AuthorizationStateWaitTdlibParameters:
                var (dbPath, filesPath) = GetDirPaths();

                _client.SetTdlibParametersAsync(
                    //useTestDc: true,
                    databaseDirectory: dbPath,
                    filesDirectory: filesPath,
                    apiId: _tdOptions.ApiId,
                    apiHash: _tdOptions.ApiHash,
                    systemLanguageCode: "en",
                    deviceModel: "Server",
                    systemVersion: "ASP.Net Core",
                    applicationVersion: "1.0"
                    );
                break;
            case TdApi.AuthorizationState.AuthorizationStateWaitPhoneNumber:
                var _ = _client.RequestQrCodeAuthenticationAsync();
                break;
            case TdApi.AuthorizationState.AuthorizationStateReady authStateReady:
                _authorizationTcs = new();
                var __ = HandleReadyAsync();
                break;
            default:
                //throw new ArgumentOutOfRangeException(authUpdate.AuthorizationState.GetType().ToString());
                break;
        }
    }

    private async Task HandleReadyAsync()
    {
        var me = await _client.GetMeAsync();

        var newPath = Path.Combine(_tdOptions.Directory, me.PhoneNumber.ToString());


        var result = true;

        if (Directory.Exists(newPath))
        {
            await _client.LogOutAsync();
            result = false;
        }

        await _client.CloseAsync();
        _client.Dispose();

        try
        {
            Directory.Move(_folderPath ?? throw new ArgumentNullException("Folder path not exist"), newPath);

            var tgAccount = new TgAccount(
                me.PhoneNumber,
                me.FirstName,
                me.LastName
            );

            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.TgAccounts.AddAsync(tgAccount);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception)
        {
            result = false;
            await _hubContext.Clients.Client(_connectionId).ReceiveErrorMessageAsync("This account is already signed in.");
        }

        _isSuccessful = result;
        await _hubContext.Clients.Client(_connectionId).ReceiveConfirmSuccessfulSignInAsync(result);

        _authorizationTcs?.SetResult();
    }

    private void EnsureOrCreateIsDirExist()
    {
        // Ensure base directory exists
        if (!Directory.Exists(_tdOptions.Directory))
        {
            Directory.CreateDirectory(_tdOptions.Directory);
        }

        //
        //TODO: Ensure that existed folder is empty for new client
        //
        // Ensure client-specific directory exists
        if (!Directory.Exists(_folderPath))
        {
            Directory.CreateDirectory(_folderPath);
        }
        else
        {
            throw new Exception("Client folder already exists");
        }
    }
    private (string, string) GetDirPaths()
        => (Path.Combine(_folderPath, "db"), Path.Combine(_folderPath, "files"));




    #region Disposable
    public async ValueTask DisposeAsync()
    {
        await DisposeAsync(true);
        GC.SuppressFinalize(this);
    }

    protected virtual async Task DisposeAsync(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            var tsc = _authorizationTcs;

            if (tsc is not null)
            {
                await tsc.Task.WaitAsync(TimeSpan.FromSeconds(60));
            }

            if (tsc is null || !_isSuccessful)
            {
                // Clean up directory if authorization was not successful
                if (Directory.Exists(_folderPath))
                {
                    Directory.Delete(_folderPath, true);
                }
            }

            _client?.Dispose();
        }
        // Free any unmanaged objects here.
        //

        _disposed = true;
    }

    #endregion
}
