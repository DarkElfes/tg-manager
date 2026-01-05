using Microsoft.Extensions.Hosting;
using Serilog;
using tgm.Api.Abstractions.Endpoints;
using tgm.Api.Database;
using tgm.Api.Features.TdClients.Hubs;
using tgm.Api.Features.TdClients.Options;
using tgm.Api.Features.TdClients.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

#region Logging
//builder.Host.UseSerilog((context, loggerConfig) =>
//    loggerConfig.ReadFrom.Configuration(context.Configuration));
#endregion

#region Database

builder.Services.AddSqlite<AppDbContext>("Data Source=tgm.db");

#endregion

#region TdService

builder.Services.AddOptions<TdOptions>()
    .Bind(builder.Configuration.GetSection(TdOptions.Td));

builder.Services.AddSingleton<TdClientManager>();
builder.Services.AddSingleton<TdClientStateManager>();

builder.Services.AddSignalR();
#endregion

#region Endpoints

//builder.Services.AddMediatR(config => config.RegisterServicesFromAssemblyContaining<Program>());
//builder.Services.AddCarter();

builder.Services.AddEndpoints();
#endregion

#region Cors

builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

#endregion


var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}


app.UseCors();

app.UseHttpsRedirection();
app.MapEndpoints();
app.MapHub<TdClientHub>("/tdclienthub");


app.Run();