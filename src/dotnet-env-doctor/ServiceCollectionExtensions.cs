using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace DotnetEnvDoctor;

/// <summary>
/// Extension methods for registering dotnet-env-doctor services with an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the dotnet-env-doctor services as singletons and binds <see cref="EnvDoctorOptions"/>.
    /// Services that are already registered are left unchanged.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <param name="configure">Optional delegate that configures <see cref="EnvDoctorOptions"/>.</param>
    /// <returns>The same <paramref name="services"/> instance, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The options are validated when they are first resolved. An invalid configuration throws
    /// the exception from <see cref="EnvDoctorOptions.Validate"/> at that point.
    /// </remarks>
    public static IServiceCollection AddEnvDoctor(this IServiceCollection services, Action<EnvDoctorOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<EnvDoctorOptions>()
            .Configure(configure ?? (_ => { }))
            .Validate(o => { o.Validate(); return true; });

        services.TryAddSingleton<IConfigSchemaLoader, JsonSchemaLoader>();
        services.TryAddSingleton<IConfigValidator, ConfigValidator>();
        services.TryAddSingleton<IEnvironmentDiffer, EnvironmentDiffer>();
        services.TryAddSingleton<ISecretDetector>(sp =>
            new SecretDetector(sp.GetRequiredService<IOptions<EnvDoctorOptions>>().Value.Secrets));
        services.TryAddSingleton<EnvDoctorRunner>();

        return services;
    }
}
