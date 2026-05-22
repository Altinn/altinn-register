using System.Text.Json.Serialization;
using Altinn.Authorization.ModelUtils;

namespace Altinn.Register.Integrations.Sire.Organization
{
    /// <summary>
    /// Specifies the source of a person-party.
    /// </summary>
    [StringEnumConverter]
    public enum IdentifierType
    {
        /// <summary>
        /// The Norwegian National Population Register.
        /// </summary>
        [JsonStringEnumMemberName("taxIdentificationNumber")]
        TaxIdentificationNumber = 1,
    }
}
