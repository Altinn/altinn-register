namespace Altinn.Register.Integrations.Ccr.Xml;

/// <summary>
/// Represents an abstract base class for operations that describe changes to a party in a Norwegian Central Coordinating Register for Legal Entities (CCR) (Enhetsregisteret ER) system.
/// </summary>
/// <remarks>This class serves as the root for specific delta operation types, such as updating a party's email or
/// name. Use derived types to represent concrete changes to party data when constructing or processing CCR
/// deltas.</remarks>
public abstract class CcrPartyDeltaOperation
{
    // TODO: define delta operations, e.g. UpdateEmail, UpdateName, etc.

    /// <summary>
    /// Represents an operation that updates the email address of a party entity.
    /// </summary>
    /// <remarks>Use this type to specify a change to a party's email address as part of a delta operation.
    /// This operation is typically applied in scenarios where incremental updates to party information are tracked or
    /// processed.</remarks>
    public sealed class UpdateEmail
        : CcrPartyDeltaOperation
    {
        /// <summary>
        /// Gets the new email address to be set for the user.
        /// </summary>
        public required string NewEmail { get; init; }
    }
}
