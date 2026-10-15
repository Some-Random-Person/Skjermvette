using ScreenTime.Agent.Service;

// Commands get no command-line configuration: "pair 123456" isn't a setting.
var builder = Host.CreateApplicationBuilder(Commands.IsCommand(args) ? [] : args);
if (Commands.IsCommand(args))
    return await Commands.RunAsync(args, builder.Configuration);

builder.Services.AddWindowsService(options => options.ServiceName = "ScreenTime Agent");
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<UsageTracker>();
builder.Services.AddSingleton<BudgetState>();
builder.Services.AddHostedService<SyncWorker>();
builder.Services.AddHostedService<HelperPipeServer>();

await builder.Build().RunAsync();
return Environment.ExitCode;
