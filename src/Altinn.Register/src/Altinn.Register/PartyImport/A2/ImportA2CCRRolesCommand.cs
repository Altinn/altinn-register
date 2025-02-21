#nullable enable

using Altinn.Authorization.ServiceDefaults.MassTransit;

namespace Altinn.Register.PartyImport.A2;

/// <summary>
/// A command for importing external roles from A2.
/// </summary>
public sealed record ImportA2CCRRolesCommand
    : CommandBase
{
    /// <summary>
    /// Gets the party ID.
    /// </summary>
    /// <remarks>
    /// It's the callers responsibility to ensure that <see cref="PartyId"/> and <see cref="PartyUuid"/>
    /// is for the same party. Failing to do so will result in undefined behavior.
    /// </remarks>
    public required int PartyId { get; init; }

    /// <summary>
    /// Gets the party UUID.
    /// </summary>
    /// <remarks>
    /// It's the callers responsibility to ensure that <see cref="PartyId"/> and <see cref="PartyUuid"/>
    /// is for the same party. Failing to do so will result in undefined behavior.
    /// </remarks>
    public required Guid PartyUuid { get; init; }

    /// <summary>
    /// Gets the change ID.
    /// </summary>
    public required uint ChangeId { get; init; }

    /// <summary>
    /// Gets when the change was registered.
    /// </summary>
    public required DateTimeOffset ChangedTime { get; init; }
}
