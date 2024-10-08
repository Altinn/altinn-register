﻿#nullable enable

using CommunityToolkit.Diagnostics;

using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Altinn.Register.ModelBinding;

/// <summary>
/// Base class for model-binders.
/// </summary>
public abstract class ModelBinder
    : IModelBinder
    , IModelBinderProvider
{
    /// <inheritdoc/>
    public abstract Task BindModelAsync(ModelBindingContext bindingContext);

    /// <summary>
    /// Gets whether or not this binder can be used to bind the current model.
    /// </summary>
    /// <param name="metadata">The model metadata.</param>
    /// <returns><see langword="true"/> if the current model binder can handle the provided <see cref="ModelMetadata"/>.</returns>
    protected abstract bool Handles(ModelMetadata metadata);

    /// <inheritdoc/>
    IModelBinder? IModelBinderProvider.GetBinder(ModelBinderProviderContext context)
    {
        Guard.IsNotNull(context);

        return Handles(context.Metadata) ? this : null;
    }
}

/// <summary>
/// Base class for model-binders that handle a specific type.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public abstract class ModelBinder<TModel>
    : ModelBinder
{
    /// <inheritdoc/>
    protected override sealed bool Handles(ModelMetadata metadata)
        => metadata.ModelType == typeof(TModel);
}
