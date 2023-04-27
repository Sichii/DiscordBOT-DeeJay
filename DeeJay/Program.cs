using DeeJay.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DeeJay;

internal class Program
{
    private static CancellationTokenSource? _ctx;
    private static IGuildOptionsRepository? _guildOptionsRepo;
    
    public static async Task Main(string[] args)
    {
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

        SetupControlledExit(provider);
        
        var tasks = hostedServices.Select(hs => hs.StartAsync(ctx.Token));

        await Task.WhenAll(tasks);
        await Task.Delay(-1);
    }

    public static void SetupControlledExit(IServiceProvider provider)
    {
        _ctx = provider.GetRequiredService<CancellationTokenSource>();
        _guildOptionsRepo = provider.GetRequiredService<IGuildOptionsRepository>();
        var handler = new PInvoke.HandlerRoutine(InspectControlType);
        PInvoke.SetConsoleCtrlHandler(handler, true);
    }

    public static bool InspectControlType(PInvoke.ControlTypes ctrlType)
    {
        Console.WriteLine("Exiting...");
        _ctx!.Cancel();
        _guildOptionsRepo!.SaveAsync().GetAwaiter().GetResult();

        return true;
    }
}