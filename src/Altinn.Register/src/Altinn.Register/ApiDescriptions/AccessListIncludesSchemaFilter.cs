using Altinn.Platform.Register.Models;
using Altinn.Register.ModelBinding;

using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Register.ApiDescriptions;

/// <summary>
/// Schema filter for <see cref="PartyComponentOption"/>.
/// </summary>
public sealed class AccessListIncludesSchemaFilter
    : SchemaFilter<PartyComponentOption>
{
    /// <inheritdoc/>
    protected override void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        schema.Enum = null;
        schema.Format = null;
        schema.Type = "array";
        schema.Items = new OpenApiSchema
        {
            Type = "string",
            Enum = PartyComponentOptionModelBinder.AllowedValues.Select(v => (IOpenApiAny)new OpenApiString(v)).ToList(),
        };
    }
}
