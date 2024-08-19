using System.Runtime.CompilerServices;

namespace Altinn.Register.Core.UnitOfWork;

/// <summary>
/// A manager for creating units of work.
/// </summary>
public interface IUnitOfWorkManager
{
    /// <summary>
    /// Creates a new unit of work.
    /// </summary>
    /// <param name="activityName">The name of the activity, used in telemetry.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>A <see cref="IUnitOfWork"/>.</returns>
    public ValueTask<IUnitOfWork> CreateAsync(
        [CallerMemberName] string activityName = "",
        CancellationToken cancellationToken = default);
}
