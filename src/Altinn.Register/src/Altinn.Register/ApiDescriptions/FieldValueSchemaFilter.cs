#nullable enable

using Altinn.Register.Core.Utils;
using CommunityToolkit.Diagnostics;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Register.ApiDescriptions;

/// <summary>
/// Schema filter for <see cref="FieldValue{T}"/>.
/// </summary>
public sealed class FieldValueSchemaFilter
    : ISchemaFilter
{
    private static readonly string IsFieldValueKey = $"{nameof(FieldValueSchemaFilter)}:IsFieldValue";

    /// <inheritdoc/>
    void ISchemaFilter.Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        Guard.IsNotNull(schema);
        Guard.IsNotNull(context);

        if (!context.Type.IsConstructedGenericType || context.Type.GetGenericTypeDefinition() != typeof(FieldValue<>))
        {
            CheckFieldValueRequiredProperties(schema, context);
            return;
        }

        var innerType = context.Type.GetGenericArguments()[0];
        var generated = context.SchemaGenerator.GenerateSchema(innerType, context.SchemaRepository);

        schema.Type = generated.Type;
        schema.Format = generated.Format;
        schema.Description = generated.Description;
        schema.Maximum = generated.Maximum;
        schema.ExclusiveMaximum = generated.ExclusiveMaximum;
        schema.Minimum = generated.Minimum;
        schema.ExclusiveMinimum = generated.ExclusiveMinimum;
        schema.MaxLength = generated.MaxLength;
        schema.MinLength = generated.MinLength;
        schema.Pattern = generated.Pattern;
        schema.MultipleOf = generated.MultipleOf;
        schema.Default = generated.Default;
        schema.ReadOnly = generated.ReadOnly;
        schema.WriteOnly = generated.WriteOnly;
        schema.AllOf = generated.AllOf;
        schema.OneOf = generated.OneOf;
        schema.AnyOf = generated.AnyOf;
        schema.Not = generated.Not;
        schema.Required = generated.Required;
        schema.Items = generated.Items;
        schema.MaxItems = generated.MaxItems;
        schema.MinItems = generated.MinItems;
        schema.UniqueItems = generated.UniqueItems;
        schema.Properties = generated.Properties;
        schema.MaxProperties = generated.MaxProperties;
        schema.MinProperties = generated.MinProperties;
        schema.AdditionalPropertiesAllowed = generated.AdditionalPropertiesAllowed;
        schema.AdditionalProperties = generated.AdditionalProperties;
        schema.Discriminator = generated.Discriminator;
        schema.Example = generated.Example;
        schema.Enum = generated.Enum;
        schema.ExternalDocs = generated.ExternalDocs;
        schema.Deprecated = generated.Deprecated;
        schema.Xml = generated.Xml;
        schema.Extensions = generated.Extensions;
        schema.UnresolvedReference = generated.UnresolvedReference;
        schema.Reference = generated.Reference;
        schema.Annotations = generated.Annotations ?? new Dictionary<string, object>();
        schema.Annotations[IsFieldValueKey] = true;

        schema.Nullable = true;
    }

    private void CheckFieldValueRequiredProperties(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema.Required is null)
        {
            return;
        }

        List<string>? toRemove = null;
        foreach (var requiredPropName in schema.Required)
        {
            if (schema.Properties.TryGetValue(requiredPropName, out var prop))
            {
                foreach (var propSchema in ReferenceChain(prop, context))
                {
                    if (propSchema.Annotations is not null && propSchema.Annotations.TryGetValue(IsFieldValueKey, out var annotation) && annotation is true)
                    {
                        toRemove ??= new List<string>(schema.Required.Count);
                        toRemove.Add(requiredPropName);
                        break;
                    }
                }
            }
        }

        if (toRemove is not null)
        {
            foreach (var item in toRemove)
            {
                schema.Required.Remove(item);
            }
        }
    }

    private IEnumerable<OpenApiSchema> ReferenceChain(OpenApiSchema schema, SchemaFilterContext context)
    {
        while (true)
        {
            yield return schema;

            if (schema.Reference is null)
            {
                yield break;
            }

            if (schema.Reference.Type != ReferenceType.Schema)
            {
                yield break;
            }

            if (schema.Reference.Id is null)
            {
                yield break;
            }

            if (!context.SchemaRepository.Schemas.TryGetValue(schema.Reference.Id, out var referenced))
            {
                yield break;
            }

            if (referenced is null)
            {
                yield break;
            }

            schema = referenced;
        }
    }
}
