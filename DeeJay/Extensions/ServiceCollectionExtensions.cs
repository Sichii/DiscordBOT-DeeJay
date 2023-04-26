using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DeeJay.Extensions;

/// <summary>
///   Extension methods for <see cref="IServiceCollection"/>
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///  Adds an option object based on a configuration section to the service collection
    /// </summary>
    public static OptionsBuilder<T> AddOptionsFromConfig<T>(
        this IServiceCollection services,
        string? section = null,
        string? optionsName = null
    ) where T: class
    {
        var path = optionsName ?? typeof(T).Name;

        if (!string.IsNullOrWhiteSpace(section))
            path = $"{section}:{path}";
        
        return services.AddOptions<T>()
                       .BindConfiguration(path, binder => binder.ErrorOnUnknownConfiguration = true);
    }
}