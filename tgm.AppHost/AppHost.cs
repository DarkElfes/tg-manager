var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.tgm_Api>("tgm-api");

builder.Build().Run();
