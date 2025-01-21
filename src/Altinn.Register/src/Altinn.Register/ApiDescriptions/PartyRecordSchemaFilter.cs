using Altinn.Register.Core.Parties.Records;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Register.ApiDescriptions;

/// <summary>
/// Schema filter for <see cref="PartyRecord"/>.
/// </summary>
public sealed class PartyRecordSchemaFilter
    : SchemaFilter<PartyRecord>
{
    private static readonly string CommonSchemaId = $"{nameof(PartyRecord)}Common";

    private readonly IOptionsMonitor<SchemaGeneratorOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartyRecordSchemaFilter"/> class.
    /// </summary>
    public PartyRecordSchemaFilter(IOptionsMonitor<SchemaGeneratorOptions> options)
    {
        _options = options;
    }

    /// <inheritdoc/>
    protected override void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (!context.SchemaRepository.Schemas.ContainsKey(CommonSchemaId))
        {
            // clone the generated schema
            var commonSchema = new OpenApiSchema(schema);

            context.SchemaRepository.AddDefinition(CommonSchemaId, commonSchema);
        }

        var commonRef = new OpenApiSchema()
        {
            Reference = new()
            {
                Type = ReferenceType.Schema,
                Id = CommonSchemaId,
            }
        };
        var personRef = EnsureRef(typeof(PersonRecord), context);
        var orgRef = EnsureRef(typeof(OrganizationRecord), context);

        schema.Properties = null;
        schema.Required = null;
        schema.OneOf = [commonRef, personRef, orgRef];
    }

    private OpenApiSchema EnsureRef(Type type, SchemaFilterContext context)
    {
        if (context.SchemaRepository.TryLookupByType(type, out var referenceSchema))
        {
            return referenceSchema;
        }

        var id = _options.CurrentValue.SchemaIdSelector(type);
        var schema = context.SchemaGenerator.GenerateSchema(type, context.SchemaRepository);
        if (!context.SchemaRepository.Schemas.ContainsKey(id))
        {
            referenceSchema = context.SchemaRepository.AddDefinition(id, schema);
            context.SchemaRepository.RegisterType(type, id);
        }
        else
        {
            referenceSchema = new()
            {
                Reference = new()
                {
                    Type = ReferenceType.Schema,
                    Id = id,
                },
            };
        }

        return referenceSchema;
    }
}
