using Altinn.Register.Core.Parties;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Register.Core.UnitOfWork;

/// <summary>
/// Extension methods for unit of work.
/// </summary>
public static class UnitOfWorkExtensions
{
    /// <summary>
    /// Gets a <see cref="IPartyPersistence"/> registered with the unit of work.
    /// </summary>
    /// <param name="uow">The unit of work.</param>
    /// <returns>A <see cref="IPartyPersistence"/>.</returns>
    public static IPartyPersistence GetPartyPersistence(this IUnitOfWork uow)
        => uow.GetRequiredService<IPartyPersistence>();

    /// <summary>
    /// Gets a <see cref="IPartyRolePersistence"/> registered with the unit of work.
    /// </summary>
    /// <param name="uow">The unit of work.</param>
    /// <returns>A <see cref="IPartyRolePersistence"/>.</returns>
    public static IPartyRolePersistence GetRolePersistence(this IUnitOfWork uow)
        => uow.GetRequiredService<IPartyRolePersistence>();
}
