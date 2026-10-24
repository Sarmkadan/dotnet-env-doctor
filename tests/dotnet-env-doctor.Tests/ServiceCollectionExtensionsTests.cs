using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DotnetEnvDoctor.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddEnvDoctor_ResolvesAllServices()
    {
        var services = new ServiceCollection();
        services.AddEnvDoctor();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<JsonSchemaLoader>(provider.GetRequiredService<IConfigSchemaLoader>());
        Assert.IsType<ConfigValidator>(provider.GetRequiredService<IConfigValidator>());
        Assert.IsType<EnvironmentDiffer>(provider.GetRequiredService<IEnvironmentDiffer>());
        Assert.IsType<SecretDetector>(provider.GetRequiredService<ISecretDetector>());
        Assert.IsType<EnvDoctorRunner>(provider.GetRequiredService<EnvDoctorRunner>());
        Assert.Same(provider.GetRequiredService<EnvDoctorRunner>(), provider.GetRequiredService<EnvDoctorRunner>());
    }

    [Fact]
    public void AddEnvDoctor_Configure_AppliesSchemaPath()
    {
        var services = new ServiceCollection();
        services.AddEnvDoctor(o => o.SchemaPath = "schema.json");

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<EnvDoctorOptions>>().Value;

        Assert.Equal("schema.json", options.SchemaPath);
    }
}
