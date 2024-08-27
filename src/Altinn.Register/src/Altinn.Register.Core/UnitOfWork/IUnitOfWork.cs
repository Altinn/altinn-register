using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Register.Core.UnitOfWork;

/// <summary>
/// Represents an atomic unit of work.
/// </summary>
public interface IUnitOfWork
    : IAsyncDisposable
    , IServiceProvider
    , ISupportRequiredService
{
    /// <summary>
    /// Gets the status of the unit of work.
    /// </summary>
    UnitOfWorkStatus Status { get; }

    /// <summary>
    /// Commits the unit of work.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    ValueTask CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the unit of work.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <remarks>
    /// This is also performed by calling <see cref="IAsyncDisposable.DisposeAsync"/> 
    /// if the unit of work has not already been committed or rolled back.
    /// </remarks>
    ValueTask RollbackAsync(CancellationToken cancellationToken = default);
}
