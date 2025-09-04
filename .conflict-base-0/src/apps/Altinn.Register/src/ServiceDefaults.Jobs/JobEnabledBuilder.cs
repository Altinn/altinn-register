using System.Collections.Immutable;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Authorization.ServiceDefaults.Jobs;

/// <summary>
/// Helper for building a job-enabled condition from multiple checks.
/// </summary>
public class JobEnabledBuilder
{
    /// <summary>
    /// Default instance of <see cref="JobEnabledBuilder"/> with no checks.
    /// </summary>
    public static JobEnabledBuilder Default { get; } = new([]);

    private readonly ImmutableList<Func<IServiceProvider, CancellationToken, ValueTask<bool>>> _checks;

    private JobEnabledBuilder(ImmutableList<Func<IServiceProvider, CancellationToken, ValueTask<bool>>> checks)
    {
        _checks = checks;
    }

    /// <summary>
    /// Adds a check to the job enabled condition.
    /// </summary>
    /// <param name="check">The check to add.</param>
    /// <returns>A new <see cref="JobEnabledBuilder"/> with <paramref name="check"/> added.</returns>
    public JobEnabledBuilder WithCheck(Func<IServiceProvider, CancellationToken, ValueTask<bool>> check)
        => new(_checks.Add(check));

    /// <summary>
    /// Creates a check function from this builder.
    /// </summary>
    /// <returns>A check function.</returns>
    public Func<IServiceProvider, CancellationToken, ValueTask<bool>> ToFunc()
    {
        var checks = _checks.ToImmutableArray();

        return ToFunc(checks);

        // This needs to be a static method to make sure we avoid capturing the builder instance
        static Func<IServiceProvider, CancellationToken, ValueTask<bool>> ToFunc(ImmutableArray<Func<IServiceProvider, CancellationToken, ValueTask<bool>>> checks)
        {
            return (sp, ct) => RunChecksSync(checks, sp, ct);
        }

        // This needs to be a static method to make sure we avoid capturing the builder instance
        static ValueTask<bool> RunChecksSync(ImmutableArray<Func<IServiceProvider, CancellationToken, ValueTask<bool>>> checks, IServiceProvider services, CancellationToken cancellationToken)
        {
            for (int index = 0; index < checks.Length; index++)
            {
                var check = checks[index];
                var task = check(services, cancellationToken);
                if (!task.IsCompletedSuccessfully)
                {
                    return RunChecksAsync(task, index, checks, services, cancellationToken);
                }

                if (!task.Result)
                {
                    return ValueTask.FromResult(false);
                }
            }

            return ValueTask.FromResult(true);
        }

        // This needs to be a static method to make sure we avoid capturing the builder instance
        static async ValueTask<bool> RunChecksAsync(ValueTask<bool> task, int index, ImmutableArray<Func<IServiceProvider, CancellationToken, ValueTask<bool>>> checks, IServiceProvider services, CancellationToken cancellationToken)
        {
            if (!await task)
            {
                return false;
            }

            index++;
            for (; index < checks.Length; index++)
            {
                var check = checks[index];
                if (!await check(services, cancellationToken))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Creates a check function from the specified builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static implicit operator Func<IServiceProvider, CancellationToken, ValueTask<bool>>(JobEnabledBuilder builder)
    {
        return builder.ToFunc();
    }
}

/// <summary>
/// Extensions for <see cref="JobEnabledBuilder"/> to allow for a fluent API.
/// </summary>
public static class JobEnabledBuilderExtensions
{
    /// <summary>
    /// Adds a check to the job enabled condition.
    /// </summary>
    /// <param name="builder">The <see cref="JobEnabledBuilder"/>.</param>
    /// <param name="check">The check to add.</param>
    /// <returns>A new <see cref="JobEnabledBuilder"/> with <paramref name="check"/> added.</returns>
    public static JobEnabledBuilder WithCheck(this JobEnabledBuilder builder, Func<IServiceProvider, bool> check)
        => builder.WithCheck((sp, _) => ValueTask.FromResult(check(sp)));

    /// <summary>
    /// Adds a check to the job enabled condition.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="builder">The <see cref="JobEnabledBuilder"/>.</param>
    /// <param name="check">The check to add.</param>
    /// <returns>A new <see cref="JobEnabledBuilder"/> with <paramref name="check"/> added.</returns>
    public static JobEnabledBuilder WithCheck<T>(this JobEnabledBuilder builder, Func<T, CancellationToken, ValueTask<bool>> check)
        where T : notnull
        => builder.WithCheck((sp, ct) => check(sp.GetRequiredService<T>(), ct));

    /// <summary>
    /// Adds a check to the job enabled condition.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="builder">The <see cref="JobEnabledBuilder"/>.</param>
    /// <param name="check">The check to add.</param>
    /// <returns>A new <see cref="JobEnabledBuilder"/> with <paramref name="check"/> added.</returns>
    public static JobEnabledBuilder WithCheck<T>(this JobEnabledBuilder builder, Func<T, bool> check)
        where T : notnull
        => builder.WithCheck((sp, _) => ValueTask.FromResult(check(sp.GetRequiredService<T>())));

    /// <summary>
    /// Adds a check to the job enabled condition that checks the configuration.
    /// </summary>
    /// <param name="builder">The <see cref="JobEnabledBuilder"/>.</param>
    /// <param name="check">The check to add.</param>
    /// <returns>A new <see cref="JobEnabledBuilder"/> with <paramref name="check"/> added.</returns>
    public static JobEnabledBuilder WithConfigurationCheck(this JobEnabledBuilder builder, Func<IConfiguration, bool> check)
        => builder.WithCheck(check);

    /// <summary>
    /// Adds a check to the job enabled condition that checks the configuration for a specific key/value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="builder">The <see cref="JobEnabledBuilder"/>.</param>
    /// <param name="key">The configuration key.</param>
    /// <param name="value">The configuration value.</param>
    /// <returns>A new <see cref="JobEnabledBuilder"/> with configuration-value check added.</returns>
    public static JobEnabledBuilder WithRequireConfigurationValue<T>(this JobEnabledBuilder builder, string key, T value)
        => builder.WithConfigurationCheck(config =>
        {
            var actualValue = config.GetValue<T>(key);
            return EqualityComparer<T>.Default.Equals(actualValue, value);
        });

    /// <summary>
    /// Adds a check to the job enabled condition that checks the configuration that a specific key is set to <see langword="true"/>.
    /// </summary>
    /// <param name="builder">The <see cref="JobEnabledBuilder"/>.</param>
    /// <param name="key">The configuration key.</param>
    /// <returns>A new <see cref="JobEnabledBuilder"/> with configuration-value check added.</returns>
    public static JobEnabledBuilder WithRequireConfigurationValueEnabled(this JobEnabledBuilder builder, string key)
        => builder.WithRequireConfigurationValue(key, true);
}
