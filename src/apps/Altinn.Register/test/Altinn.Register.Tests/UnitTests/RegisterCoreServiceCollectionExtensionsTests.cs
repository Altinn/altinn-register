using Altinn.Register.Core.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Altinn.Register.Tests.UnitTests;

public class RegisterCoreServiceCollectionExtensionsTests
{
    [Fact]
    public void AddRateLimitPolicy_BindsNamedConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([
                new("Altinn:Register:RateLimit:Policy:test:Limit", "3"),
                new("Altinn:Register:RateLimit:Policy:test:WindowDuration", "01:00:00"),
                new("Altinn:Register:RateLimit:Policy:test:WindowBehavior", "TrailingEdge"),
                new("Altinn:Register:RateLimit:Policy:test:BlockDuration", "00:30:00"),
                new("Altinn:Register:RateLimit:Policy:test:BlockedRequestBehavior", "Renew"),
            ])
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddRateLimitPolicy("test");

        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptionsMonitor<RateLimitPolicySettings>>().Get("test");
        options.IsConfigured.Should().BeTrue();
        options.Limit.Should().Be(3);
        options.WindowDuration.Should().Be(TimeSpan.FromHours(1));
        options.WindowBehavior.Should().Be(RateLimitWindowBehavior.TrailingEdge);
        options.BlockDuration.Should().Be(TimeSpan.FromMinutes(30));
        options.BlockedRequestBehavior.Should().Be(BlockedRequestBehavior.Renew);
    }

    [Fact]
    public void AddRegisterRateLimiting_UnconfiguredPolicy_ThrowsValidationException()
    {
        var services = new ServiceCollection();
        services.AddRegisterRateLimiting();

        using var serviceProvider = services.BuildServiceProvider();

        var act = () => serviceProvider.GetRequiredService<IOptionsMonitor<RateLimitPolicySettings>>().Get("test");
        var exn = Assert.Throws<OptionsValidationException>(act);

        exn.Failures.Should().ContainSingle()
            .Which.Should().Be("Rate limit policy 'test' has not been configured.");
    }

    [Fact]
    public void AddRateLimitPolicy_WithInvalidConfiguration_ThrowsValidationException()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([
                new("Altinn:Register:RateLimit:Policy:test:Limit", "0"),
                new("Altinn:Register:RateLimit:Policy:test:WindowDuration", "00:00:30"),
                new("Altinn:Register:RateLimit:Policy:test:BlockDuration", "00:00:30"),
            ])
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddRateLimitPolicy("test");

        using var serviceProvider = services.BuildServiceProvider();

        var act = () => serviceProvider.GetRequiredService<IOptionsMonitor<RateLimitPolicySettings>>().Get("test");
        var exn = Assert.Throws<OptionsValidationException>(act);

        exn.Failures.Should().Contain(failure => failure.Contains(nameof(RateLimitPolicySettings.Limit)));
        exn.Failures.Should().Contain(failure => failure.Contains(nameof(RateLimitPolicySettings.WindowDuration)));
        exn.Failures.Should().Contain(failure => failure.Contains(nameof(RateLimitPolicySettings.BlockDuration)));
        exn.Failures.Should().NotContain(failure => failure.Contains("has not been configured"));
    }

    [Fact]
    public void AddRateLimitPolicy_WhenCalledTwiceForSameName_DoesNotAddDuplicateBindings()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddRateLimitPolicy("test");

        var configureCount = services.Count(static sd => sd.ServiceType == typeof(IConfigureOptions<RateLimitPolicySettings>));
        var changeTokenCount = services.Count(static sd => sd.ServiceType == typeof(IOptionsChangeTokenSource<RateLimitPolicySettings>));

        services.AddRateLimitPolicy("test");

        services.Count(static sd => sd.ServiceType == typeof(IConfigureOptions<RateLimitPolicySettings>)).Should().Be(configureCount);
        services.Count(static sd => sd.ServiceType == typeof(IOptionsChangeTokenSource<RateLimitPolicySettings>)).Should().Be(changeTokenCount);
    }
}
