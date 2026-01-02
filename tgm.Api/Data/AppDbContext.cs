using Microsoft.EntityFrameworkCore;
using tgm.Api.Features.TdClients.Entities;

namespace tgm.Api.Database;

public class AppDbContext(
    DbContextOptions<AppDbContext> options
    ) : DbContext(options)
{
    public DbSet<TgAccount> TgAccounts { get; set; } 
}
