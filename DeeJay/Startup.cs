using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeeJay.Abstractions;
using DeeJay.Extensions;
using DeeJay.Services;
using DeeJay.Services.Factories;
using DeeJay.Services.Options;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;

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
        services.AddSingleton(Configuration);

        services.AddSingleton<JsonSerializerOptions>(
            _ => new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                PropertyNameCaseInsensitive = true,
                IgnoreReadOnlyProperties = true,
                IgnoreReadOnlyFields = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                AllowTrailingCommas = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        
        services.AddOptions();
        
        RegisterStructuredLoggingTransformations();

        services.AddLogging(
            logging =>
            {
                logging.AddConfiguration(Configuration.GetSection(ConfigKeys.Logging.Key));

                logging.AddNLog(
                    Configuration,
                    new NLogProviderOptions
                    {
                        LoggingConfigurationSectionName = ConfigKeys.Logging.NLog.Key
                    });
            });

        services.AddSingleton<IDiscordClient, DiscordSocketClient>();
        services.AddTransient<IStreamingServiceFactory, MusicStreamingServiceFactory>();
        services.AddSingleton<IStreamingServiceProvider, StreamingServiceProvider>();
        
        services.AddOptionsFromConfig<GuildOptionsRepositoryOptions>(ConfigKeys.Options.Key);
        services.AddSingleton<IGuildOptionsRepository, GuildOptionsRepository>();

        services.AddOptionsFromConfig<YtdlSearchServiceOptions>(ConfigKeys.Options.Key);
        services.AddTransient<ISearchService<ISearchResult>, YtdlSearchService>();

        services.AddOptionsFromConfig<FfmpegStreamPlayerOptions>(ConfigKeys.Options.Key);
        services.AddTransient<IStreamPlayerFactory, FfmpegStreamPlayerFactory>();

        services.AddOptionsFromConfig<DiscordClientServiceOptions>(ConfigKeys.Options.Key);
        services.AddHostedService<DiscordClientService>();
        
        services.ConfigureOptions<OptionsConfigurer>();
    }

    private void RegisterStructuredLoggingTransformations() =>
        LogManager.Setup()
                  .SetupSerialization(
                      builder =>
                          builder.RegisterObjectTransformation<ISearchResult>(
                                     obj => new
                                     {
                                         Success = obj.Success,
                                         Error = obj.ErrorMessage,
                                         Title = obj.Title,
                                         Query = obj.OriginalQuery,
                                         Uri = obj.Uri?.ToString(),
                                         Duration = obj.Duration
                                     })
                                 .RegisterObjectTransformation<ISong>(
                                     obj => new
                                     {
                                         Title = obj.Title,
                                         Uri = obj.Uri.ToString(),
                                         IsLive = obj.IsLive,
                                         RequestedBy = obj.RequestedBy,
                                         Duration = obj.Duration
                                     })
                                 .RegisterObjectTransformation<IGuildUser>(
                                     obj => new
                                     {
                                         Name = obj.Username,
                                         DisplayName = obj.DisplayName,
                                         GuildName = obj.Guild.Name
                                     }));

    /// <summary>
    /// A static structure that represents the configuration file
    /// </summary>
    [SuppressMessage("ReSharper", "MemberHidesStaticFromOuterClass")]
    public static class ConfigKeys
    {
        #pragma warning disable CS1591
        public static class Options
        {
            public static string Key => "Options";
        }
        
        public static class Logging
        {
            public static string Key => "Logging";

            public static class NLog
            {
                public static string Key => $"{Logging.Key}:NLog";
            }
        }
        #pragma warning restore CS1591
    }
}