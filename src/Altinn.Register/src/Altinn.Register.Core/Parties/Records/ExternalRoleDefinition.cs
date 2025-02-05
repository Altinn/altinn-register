using System.Diagnostics;

namespace Altinn.Register.Core.Parties.Records;

/// <summary>
/// Represents an external role.
/// </summary>
[DebuggerDisplay("{Identifier,nq} ({Source,nq})")]
public sealed record ExternalRoleDefinition
{ 
    /// <summary>
    /// Gets the source of the external role.
    /// </summary>
    public required PartySource Source { get; init; }

    /// <summary>
    /// Gets the identifier of the external role.
    /// </summary>
    /// <remarks>
    /// The identifier is unique within the source.
    /// </remarks>
    public required string Identifier { get; init; }

    /// <summary>
    /// Gets the name of the external role.
    /// </summary>
    public required TranslatedText Name { get; init; }

    /// <summary>
    /// Gets the description of the external role.
    /// </summary>
    public required TranslatedText Description { get; init; }

    /// <summary>
    /// Gets the legacy role-code of the external role, if it has one.
    /// </summary>
    public required string? Code { get; init; }
}
