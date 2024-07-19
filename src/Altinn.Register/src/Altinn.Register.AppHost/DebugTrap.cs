using System.Diagnostics;
using Aspire.Hosting.Lifecycle;

/// <summary>
/// Debug trap
/// </summary>
internal class DebugTrap
    : IDistributedApplicationLifecycleHook 
{
    /// <inheritdoc/>
    public Task AfterEndpointsAllocatedAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
    {
        if (Debugger.IsAttached)
        {
            Debugger.Break();
        }

        return Task.CompletedTask;
    }
}
