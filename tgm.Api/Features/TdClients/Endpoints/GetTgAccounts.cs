using tgm.Api.Abstractions.Endpoints;
using tgm.Api.Database;
using tgm.Api.Features.TdClients.DTOs;
using tgm.Api.Features.TdClients.Entities;
using tgm.Api.Features.TdClients.Services;

namespace tgm.Api.Features.TdClients.Endpoints;

public static class GetTgAccounts
{
    public sealed record Response(List<TgAccount> Accounts);

    public class GetTgAccountsEndpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("/tdclients/accounts", Handler);
                //.RequireAuthorization();
        }
    }

    public static IResult Handler(
        AppDbContext context,
        TdClientStateManager stateManager
        )
    {
        return Results.Ok(context.TgAccounts.Select(x => new AccountDTO(
            x.Id,
            x.FirstName,
            x.PhoneNumber,
            stateManager.GetClientState(x.Id)
            )));
    }
}
