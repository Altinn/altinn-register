﻿using System.Globalization;
using Altinn.Urn;

namespace Altinn.Register.Contracts;

/// <summary>
/// A unique reference to a party in the form of an URN.
/// </summary>
[KeyValueUrn]
public abstract partial record PartyUrn
{
    /// <summary>
    /// Try to get the urn as a party id.
    /// </summary>
    /// <param name="partyId">The resulting party id.</param>
    /// <returns><see langword="true"/> if this party reference is a party id, otherwise <see langword="false"/>.</returns>
    [UrnKey("altinn:party:id", Canonical = true)]
    [UrnKey("altinn:partyid")]
    public partial bool IsPartyId(out uint partyId);

    /// <summary>
    /// Try to get the urn as a party uuid.
    /// </summary>
    /// <param name="partyUuid">The resulting party uuid.</param>
    /// <returns><see langword="true"/> if this party reference is a party uuid, otherwise <see langword="false"/>.</returns>
    [UrnKey("altinn:party:uuid", Canonical = true)]
    [UrnKey("altinn:person:uuid")]
    [UrnKey("altinn:organization:uuid")]
    [UrnKey("altinn:systemuser:uuid")]
    [UrnKey("altinn:user:uuid")]
    public partial bool IsPartyUuid(out Guid partyUuid);

    /// <summary>
    /// Try to get the urn as an organization number.
    /// </summary>
    /// <param name="organizationIdentifier">The resulting organization identifier.</param>
    /// <returns><see langword="true"/> if this party reference is an organization identifier, otherwise <see langword="false"/>.</returns>
    [UrnKey("altinn:organization:identifier-no", Canonical = true)]
    public partial bool IsOrganizationId(out OrganizationIdentifier organizationIdentifier);

    /// <summary>
    /// Try to get the urn as a person identifier.
    /// </summary>
    /// <param name="personIdentifier">The resulting person identifier.</param>
    /// <returns><see langword="true"/> if this party reference is an person identifier, otherwise <see langword="false"/>.</returns>
    [UrnKey("altinn:person:identifier-no", Canonical = true)]
    public partial bool IsPersonId(out PersonIdentifier personIdentifier);

    /// <summary>
    /// Try to get the urn as a user id.
    /// </summary>
    /// <param name="userId">The resulting user id.</param>
    /// <returns><see langword="true"/> if this party reference is a user id, otherwise <see langword="false"/>.</returns>
    [UrnKey("altinn:user:id", Canonical = true)]
    [UrnKey("altinn:userid")]
    public partial bool IsUserId(out uint userId);

    // Manually overridden to disallow negative party ids
    private static bool TryParsePartyId(ReadOnlySpan<char> segment, IFormatProvider? provider, out uint value)
        => uint.TryParse(segment, NumberStyles.None, provider, out value);

    // Manually overridden to disallow negative user ids
    private static bool TryParseUserId(ReadOnlySpan<char> segment, IFormatProvider? provider, out uint value)
        => uint.TryParse(segment, NumberStyles.None, provider, out value);
}
