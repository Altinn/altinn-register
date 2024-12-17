using MassTransit;
using MassTransit.Testing;

namespace Altinn.Register.TestUtils;

/// <summary>
/// Base class for consumer tests.
/// </summary>
/// <typeparam name="T">The consumer type.</typeparam>
public abstract class ConsumerTestBase<T>
    : BusTestBase
    where T : class, IConsumer
{
    private IConsumerTestHarness<T>? _consumerHarness;

    /// <summary>
    /// Gets the consumer test harness.
    /// </summary>
    protected IConsumerTestHarness<T> ConsumerHarness => _consumerHarness!;

    /// <inheritdoc/>
    protected override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _consumerHarness = Harness.GetConsumerHarness<T>();
    }
}
