////#nullable enable

////using Altinn.Register.Core;
////using System.Collections.Frozen;
////using System.Collections.Immutable;
////using System.Diagnostics;
////using System.Text.Json;

////namespace Altinn.Register.PartyImport.A2;

////public interface IA2PartyEnricher
////{
////    public Task<A2PartyEnrichmentContext> Enrich(A2PartyEnrichmentContext context);
////}

////public class A2PartyImportEnricherProvider
////{
////    //private readonly ImmutableArray
////}

////public sealed class A2PartyEnricherRegistry
////{
////    private readonly FrozenDictionary<string, A2PartyEnricherRegistration> _registrations;

////    /// <summary>
////    /// Initializes a new instance of the <see cref="A2PartyEnricherRegistry"/> class.
////    /// </summary>
////    public A2PartyEnricherRegistry(IEnumerable<A2PartyEnricherRegistration> registrations)
////    {
////        _registrations = registrations.ToFrozenDictionary(
////            registration => registration.Name.Value,
////            registration => registration);
////    }

////    /// <summary>
////    /// Get candidate enrichers for the given context.
////    /// </summary>
////    /// <param name="context">The context.</param>
////    /// <returns>A set of candidate enrichers.</returns>
////    public IEnumerable<JsonEncodedText> GetCandidates(A2PartyEnrichmentContext context)
////    {
////        foreach (var registration in _registrations.Values)
////        {
////            if (registration.IsCandidate(context))
////            {
////                yield return registration.Name;
////            }
////        }
////    }

////    /// <summary>
////    /// Enrich party being imported from A2.
////    /// </summary>
////    /// <param name="context">The enrichment context.</param>
////    /// <param name="enricherName">The enricher name.</param>
////    /// <param name="services">The <see cref="IServiceProvider"/>.</param>
////    /// <returns>The modified context.</returns>
////    public async Task<A2PartyEnrichmentContext> Enrich(A2PartyEnrichmentContext context, string enricherName, IServiceProvider services)
////    {
////        if (!_registrations.TryGetValue(enricherName, out var registration))
////        {
////            throw new ArgumentException($"No enricher named '{enricherName}' found", nameof(enricherName));
////        }

////        using var activity = RegisterTelemetry.StartActivity(
////            "enrich A2 party", 
////            kind: ActivityKind.Internal, 
////            tags: [
////                new("enricher.name", enricherName),
////            ]);

////        IA2PartyEnricher? enricher = null;
////        try
////        {
////            enricher = registration.CreateEnricher(services);
////            return await enricher.Enrich(context);
////        }
////        catch (Exception ex) when (activity is not null && ex is not OperationCanceledException)
////        {
////            activity.SetStatus(ActivityStatusCode.Error, ex.Message);
////            throw;
////        }
////        finally
////        {
////            if (enricher is IAsyncDisposable asyncDisposable)
////            {
////                await asyncDisposable.DisposeAsync();
////            }
////            else if (enricher is IDisposable disposable)
////            {
////                disposable.Dispose();
////            }
////        }
////    }
////}

////public abstract record A2PartyEnricherRegistration
////{
////    private readonly JsonEncodedText _name;
////    private readonly Func<IServiceProvider, IA2PartyEnricher> _factory;
////    private readonly Func<A2PartyEnrichmentContext, bool> _predicate;

////    /// <summary>
////    /// Initializes a new instance of the A2PartyEnricherRegistration class with the specified name, factory, and
////    /// predicate.
////    /// </summary>
////    /// <param name="name">The name that identifies the enricher registration. This value is used to distinguish different enrichers.</param>
////    /// <param name="factory">A delegate that creates an instance of an IA2PartyEnricher using the provided IServiceProvider. Cannot be null.</param>
////    /// <param name="predicate">A function that determines whether the enricher should be applied for a given A2PartyEnrichmentContext. Cannot
////    /// be null.</param>
////    private protected A2PartyEnricherRegistration(
////        JsonEncodedText name,
////        Func<IServiceProvider, IA2PartyEnricher> factory,
////        Func<A2PartyEnrichmentContext, bool> predicate)
////    {
////        _name = name;
////        _factory = factory;
////        _predicate = predicate;
////    }

////    /// <summary>
////    /// Gets the registration name.
////    /// </summary>
////    public JsonEncodedText Name => _name;

////    /// <summary>
////    /// Determines whether the specified enrichment context qualifies as a candidate based on the defined predicate.
////    /// </summary>
////    /// <param name="context">The enrichment context to evaluate. Cannot be null.</param>
////    /// <returns>true if the context meets the candidate criteria; otherwise, false.</returns>
////    public bool IsCandidate(A2PartyEnrichmentContext context) => _predicate(context);

////    /// <summary>
////    /// Creates a new instance of an object that implements the IA2PartyEnricher interface using the specified service
////    /// provider.
////    /// </summary>
////    /// <param name="services">The service provider used to resolve dependencies required by the enricher instance. Cannot be null.</param>
////    /// <returns>An instance of IA2PartyEnricher created with the provided service provider.</returns>
////    public IA2PartyEnricher CreateEnricher(IServiceProvider services) => _factory(services);

////    private sealed record Typed<T>
////        : A2PartyEnricherRegistration
////        where T : IA2PartyEnricher
////    {
////        public Typed(JsonEncodedText name, Func<A2PartyEnrichmentContext, bool> predicate)
////            : base(name, CreateFactory(), predicate)
////        {
////        }

////        private static Func<IServiceProvider, IA2PartyEnricher> CreateFactory()
////        {
////            var objectFactory = ActivatorUtilities.CreateFactory<T>([]);
////            return serviceProvider => objectFactory(serviceProvider, []);
////        }
////    }
////}

////public sealed class A2PartyEnrichmentContext
////{
////}
