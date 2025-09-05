namespace Altinn.Register.Core.Parties;

/// <summary>
/// The result of <see cref="IPartyPersistence.UpsertUserRecord(Guid, ulong, Authorization.ModelUtils.FieldValue{string}, bool, CancellationToken)"/>
/// </summary>
public sealed record UpsertUserRecordResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the party was updated.
    /// </summary>
    public required bool PartyUpdated { get; set; }
}
