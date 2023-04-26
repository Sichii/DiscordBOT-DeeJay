using DeeJay.Abstractions;
using DeeJay.Extensions;
using DeeJay.Services;
using DeeJay.Services.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeeJay;

/// <summary>
///    The startup class for the program
/// </summary>
public sealed class Startup
{
    /// <summary>
    ///    The configuration for the program
    /// </summary>
    public IConfiguration Configuration { get; set; }
    
    /// <summary>
    ///   Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    public Startup(IConfiguration configuration) => Configuration = configuration;
    
    /// <summary>
    ///   Configures the services for the program
    /// </summary>
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IStreamingServiceFactory, StreamingServiceFactory>();
        services.AddSingleton<IStreamingServiceProvider, StreamingServiceProvider>();
        
        services.AddOptionsFromConfig<GuildOptionsRepositoryOptions>(ConfigKeys.Options.Key);
        services.AddSingleton<IGuildOptionsRepository, GuildOptionsRepository>();

        services.AddOptionsFromConfig<YtdlSearchServiceOptions>(ConfigKeys.Options.Key);
        services.AddTransient<ISearchService<ISearchResult>, YtdlSearchService>();

        services.AddOptionsFromConfig<DiscordClientServiceOptions>(ConfigKeys.Options.Key);
        services.AddHostedService<DiscordClientService>();
    }

    /// <summary>
    /// A static structure that represents the configuration file
    /// </summary>
    public static class ConfigKeys
    {
        #pragma warning disable CS1591
        public static class Options
        {
            public static string Key => "Options";
        }
        #pragma warning restore CS1591
    }
}