using System.Text.Json;
using Altinn.Authorization.ModelUtils;
using CommunityToolkit.Diagnostics;

namespace Altinn.Platform.Models.Register;

/// <summary>
/// Represents a party in Altinn Register.
/// </summary>
[PolymorphicFieldValueRecord(IsRoot = true)]
[PolymorphicDerivedType(typeof(Person), PartyType.Person)]
[PolymorphicDerivedType(typeof(Organization), PartyType.Organization)]
[PolymorphicDerivedType(typeof(SelfIdentifiedUser), PartyType.SelfIdentifiedUser)]
[PolymorphicDerivedType(typeof(SystemUser), PartyType.SystemUser)]
[PolymorphicDerivedType(typeof(EnterpriseUser), PartyType.EnterpriseUser)]
public record Party
    : IHasExtensionData
{
    [JsonExtensionData]
    private readonly JsonElement _extensionData;

    private readonly Guid _uuid;
    private readonly PartyUrn.PartyUuid _urn = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="Party"/> class.
    /// </summary>
    /// <param name="partyType">The type of the party.</param>
    public Party(NonExhaustiveEnum<PartyType> partyType)
    {
        Type = partyType;
    }

    /// <summary>
    /// Gets the UUID of the party.
    /// </summary>
    [JsonPropertyName("partyUuid")]
    public required Guid Uuid
    {
        get => _uuid;
        init
        {
            Guard.IsNotDefault(value);
            _uuid = value;
            _urn = PartyUrn.PartyUuid.Create(value);
        }
    }

    /// <summary>
    /// Gets the version ID of the party.
    /// </summary>
    [JsonPropertyName("versionId")]
    public required ulong VersionId { get; init; }

    /// <summary>
    /// Gets the canonical URN of the party.
    /// </summary>
    [JsonPropertyName("urn")]
    public PartyUrn.PartyUuid Urn
        => _urn;

    /// <summary>
    /// Gets the ID of the party.
    /// </summary>
    [JsonPropertyName("partyId")]
    public required FieldValue<uint> PartyId { get; init; }

    /// <summary>
    /// Gets the type of the party.
    /// </summary>
    [JsonPropertyName("partyType")]
    [PolymorphicDiscriminatorProperty]
    public NonExhaustiveEnum<PartyType> Type { get; }

    /// <summary>
    /// Gets the display-name of the party.
    /// </summary>
    [JsonPropertyName("displayName")]
    public required FieldValue<string> DisplayName { get; init; }

    /// <summary>
    /// Gets when the party was created in Altinn 3.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public required FieldValue<DateTimeOffset> CreatedAt { get; init; }

    /// <summary>
    /// Gets when the party was last modified in Altinn 3.
    /// </summary>
    [JsonPropertyName("modifiedAt")]
    public required FieldValue<DateTimeOffset> ModifiedAt { get; init; }

    /// <summary>
    /// Gets whether the party is deleted.
    /// </summary>
    [JsonPropertyName("isDeleted")]
    public required FieldValue<bool> IsDeleted { get; init; }

    /// <summary>
    /// Gets user information for the party.
    /// </summary>
    [JsonPropertyName("user")]
    public required FieldValue<PartyUser> User { get; init; }

    /// <inheritdoc/>
    JsonElement IHasExtensionData.JsonExtensionData
        => _extensionData;
}
