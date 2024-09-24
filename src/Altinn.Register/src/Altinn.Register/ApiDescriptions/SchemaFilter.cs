using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Register.ApiDescriptions;

/// <summary>
/// Base class for schema filters that affect a specific type.
/// </summary>
/// <typeparam name="T">The type this schema filter affects.</typeparam>
public abstract class SchemaFilter<T> : ISchemaFilter
{
    /// <inheritdoc/>
    void ISchemaFilter.Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        if (context.Type is T)
        {
            Apply(schema, context);
        }
    }

    /// <summary>
    /// Applies the schema filter to the given schema.
    /// </summary>
    /// <param name="schema">The schema to apply the filter to.</param>
    /// <param name="context">The context in which the schema is being applied.</param>
    protected abstract void Apply(OpenApiSchema schema, SchemaFilterContext context);
}
