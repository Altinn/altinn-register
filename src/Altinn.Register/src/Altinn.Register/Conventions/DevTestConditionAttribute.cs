#nullable enable

using System.Collections.Immutable;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Altinn.Register.Conventions;

/// <summary>
/// A condition that disables a controller if the application is not running in a development environment.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class DevTestConditionAttribute
    : Attribute, IControllerCondition
{
    private static readonly ImmutableArray<string> _devTestEnvironments
        = ImmutableArray.Create("development", "test", "at22", "at23", "at24");

    /// <inheritdoc />
    public bool ShouldDisable(ControllerModel controller, IServiceProvider services)
    {
        var env = services.GetRequiredService<IHostEnvironment>();

        foreach (var validEnv in _devTestEnvironments)
        {
            if (string.Equals(env.EnvironmentName, validEnv, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
