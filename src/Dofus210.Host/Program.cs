using Dofus210.Bll;
using Dofus210.Data;
using Dofus210.Host.HostedServices;
using Dofus210.Host.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<ServerOptions>(
    builder.Configuration.GetSection(ServerOptions.SectionName));

builder.Services.AddDataAccess(builder.Configuration);
builder.Services.AddBusinessLogic();
builder.Services.AddHostedService<GameServerHostedService>();

var host = builder.Build();
await host.RunAsync();
