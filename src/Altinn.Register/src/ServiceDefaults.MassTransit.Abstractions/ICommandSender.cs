namespace Altinn.Authorization.ServiceDefaults.MassTransit;

/// <summary>
/// A command sender.
/// </summary>
public interface ICommandSender
{
    /// <summary>
    /// Sends a command.
    /// </summary>
    /// <typeparam name="T">The command type.</typeparam>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    Task Send<T>(T command, CancellationToken cancellationToken = default)
        where T : CommandBase;
}
