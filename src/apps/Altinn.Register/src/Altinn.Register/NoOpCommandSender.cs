using Altinn.Authorization.ServiceDefaults.MassTransit;

namespace Altinn.Register;

/// <summary>
/// Fallback <see cref="ICommandSender"/> used when MassTransit is not wired
/// (test hosts, init-only runs). The real <c>CommandSender</c> registered by
/// <c>AddAltinnMassTransit</c> wins over this via <c>TryAddScoped</c>.
/// </summary>
internal sealed class NoOpCommandSender : ICommandSender
{
    /// <inheritdoc/>
    public Task Send<T>(T command, CancellationToken cancellationToken = default)
        where T : CommandBase
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task Send<T>(IEnumerable<T> commands, CancellationToken cancellationToken = default)
        where T : CommandBase
        => Task.CompletedTask;
}
