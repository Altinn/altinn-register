#nullable enable

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Altinn.Register.Conventions;

/// <summary>
/// A convention that allows controllers to be conditionally added to the application.
/// </summary>
internal sealed class ConditionalControllerConvention
    : IControllerModelConvention
{
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConditionalControllerConvention"/> class.
    /// </summary>
    public ConditionalControllerConvention(IServiceProvider services)
    {
        _services = services;
    }

    /// <inheritdoc />
    public void Apply(ControllerModel controller)
    {
        foreach (var condition in controller.Attributes.OfType<IControllerCondition>())
        {
            if (condition.ShouldDisable(controller, _services))
            {
                DisableController(controller);
                return;
            }
        }
    }

    private void DisableController(ControllerModel controller)
    {
        controller.ApiExplorer.IsVisible = false;
        controller.Actions.Clear();
        controller.Selectors.Clear();
        controller.ControllerProperties.Clear();
        controller.Filters.Clear();
        controller.Properties.Clear();

        controller.Filters.Add(DisabledFilter.Instance);
    }

    private sealed class DisabledFilter
        : IResourceFilter
    {
        public static DisabledFilter Instance { get; } = new DisabledFilter();

        private DisabledFilter()
        {
        }

        public void OnResourceExecuting(ResourceExecutingContext context)
        {
            context.Result = new NotFoundResult();
        }

        public void OnResourceExecuted(ResourceExecutedContext context)
        {
        }
    }
}
