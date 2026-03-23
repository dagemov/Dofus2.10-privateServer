using Dofus210.Bll;
using Dofus210.Data;
using Dofus210.Host.Auth;
using Dofus210.Host.Game;
using Dofus210.Host.HostedServices;
using Dofus210.Host.Networking;
using Dofus210.Host.Options;

var builder = Host.CreateApplicationBuilder(args);
var serverOptions = builder.Configuration.GetSection(ServerOptions.SectionName).Get<ServerOptions>() ?? new ServerOptions();

try
{
    PortBindingPreflight.ThrowIfPortsUnavailable(serverOptions);
}
catch (InvalidOperationException exception)
{
    Console.Error.WriteLine(exception.Message);
    Environment.ExitCode = 1;
    return;
}

builder.Services.Configure<ServerOptions>(
    builder.Configuration.GetSection(ServerOptions.SectionName));

builder.Services.AddDataAccess(builder.Configuration);
builder.Services.AddBusinessLogic();
builder.Services.AddSingleton<IAuthTrafficRecorder, FileAuthTrafficRecorder>();
builder.Services.AddSingleton<IGameTrafficRecorder, FileGameTrafficRecorder>();
builder.Services.AddSingleton<IAuthHandshakeFactory, AuthHandshakeFactory>();
builder.Services.AddSingleton<IAuthTicketStore, AuthTicketStore>();
builder.Services.AddHostedService<AuthServerHostedService>();
builder.Services.AddHostedService<GameServerHostedService>();

var host = builder.Build();
await host.RunAsync();
