﻿using Altinn.Authorization.ModelUtils;

namespace Altinn.Register.Contracts;

/// <summary>
/// Represents a system user party in Altinn Register.
/// </summary>
[PolymorphicFieldValueRecord]
public sealed record SystemUser()
    : Party(PartyType.SystemUser)
{
    /// <summary>
    /// Gets the owner of the system user.
    /// </summary>
    [JsonPropertyName("owner")]
    public required FieldValue<PartyRef> Owner { get; init; }
}
