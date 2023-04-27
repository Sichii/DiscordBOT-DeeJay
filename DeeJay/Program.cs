using DeeJay;
using DeeJay.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
services.AddSingleton(_ => ctx);

startup.ConfigureServices(services);

var provider = services.BuildServiceProvider();

var hostedServices = provider.GetServices<IHostedService>();

AppDomain.CurrentDomain.ProcessExit += Cleanup;

var tasks = hostedServices.Select(hs => hs.StartAsync(ctx.Token));

await Task.WhenAll(tasks);
await Task.Delay(-1);

void Cleanup(object? sender, EventArgs e)
{
    Console.WriteLine("Exiting...");
    ctx.Cancel();

    var guildOptions = provider.GetRequiredService<IGuildOptionsRepository>();
    guildOptions.SaveAsync().GetAwaiter().GetResult();
}