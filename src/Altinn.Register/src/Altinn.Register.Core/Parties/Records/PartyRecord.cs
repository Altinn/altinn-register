﻿using Altinn.Register.Core.Utils;

namespace Altinn.Register.Core.Parties.Records;

/// <summary>
/// A database record for a party.
/// </summary>
public class PartyRecord
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PartyRecord"/> class.
    /// </summary>
    public PartyRecord(FieldValue<PartyType> partyType)
    {
        PartyType = partyType;
    }

    /// <summary>
    /// Gets the UUID of the party.
    /// </summary>
    public required FieldValue<Guid> PartyUuid { get; init; }

    /// <summary>
    /// Gets the ID of the party.
    /// </summary>
    public required FieldValue<int> PartyId { get; init; }

    /// <summary>
    /// Gets the type of the party.
    /// </summary>
    public FieldValue<PartyType> PartyType { get; private init; }

    /// <summary>
    /// Gets the (display) name of the party.
    /// </summary>
    public required FieldValue<string> Name { get; init; }

    /// <summary>
    /// Gets the person identifier of the party, or <see langword="null"/> if the party is not a person.
    /// </summary>
    public required FieldValue<PersonIdentifier> PersonIdentifier { get; init; }

    /// <summary>
    /// Gets the organization identifier of the party, or <see langword="null"/> if the party is not an organization.
    /// </summary>
    public required FieldValue<OrganizationIdentifier> OrganizationIdentifier { get; init; }

    /// <summary>
    /// Gets when the party was created.
    /// </summary>
    public required FieldValue<DateTimeOffset> CreatedAt { get; init; }

    /// <summary>
    /// Gets when the party was last modified.
    /// </summary>
    public required FieldValue<DateTimeOffset> ModifiedAt { get; init; }
}