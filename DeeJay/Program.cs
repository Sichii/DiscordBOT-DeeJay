using DeeJay;
using DeeJay.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

Environment.SetEnvironmentVariable("DOTNET_ReadyToRun", "0");

var services = new ServiceCollection();

// @formatter:off
var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json")
                    #if DEBUG
                    .AddJsonFile("appsettings.local.json")
                    #else
                    .AddJsonFile("appsettings.prod.json")
                    #endif
                    ;

var configuration = builder.Build();
// @formatter:on

var startup = new Startup(configuration);
var ctx = new CancellationTokenSource();
services.AddSingleton(() => ctx.Token);

startup.ConfigureServices(services);

var provider = services.BuildServiceProvider();

AppDomain.CurrentDomain.ProcessExit += Cleanup;

await Task.Delay(-1);

void Cleanup(object? sender, EventArgs e)
{
    Console.WriteLine("Exiting...");
    ctx.Cancel();

    var guildOptions = provider.GetRequiredService<IGuildOptionsRepository>();
    guildOptions.SaveAsync().GetAwaiter().GetResult();
}