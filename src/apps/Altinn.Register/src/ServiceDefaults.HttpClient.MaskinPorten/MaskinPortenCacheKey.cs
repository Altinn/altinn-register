namespace Altinn.Authorization.ServiceDefaults.HttpClient.MaskinPorten;

/// <summary>
/// Represents a MaskinPorten client cache-key.
/// </summary>
/// <notes>
/// This is used as a cache-key and a service-key, so it's important that all properties are immutable and
/// equatable. Care should be taken when modifying this class to ensure that equality semantics remain correct.
/// </notes>
internal sealed record class MaskinPortenCacheKey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MaskinPortenCacheKey"/> class.
    /// </summary>
    /// <param name="clientName">See <see cref="ClientName"/>.</param>
    /// <param name="clientId">See <see cref="ClientId"/>.</param>
    /// <param name="scope">See <see cref="Scope"/>.</param>
    /// <param name="resource">See <see cref="Resource"/>.</param>
    /// <param name="consumerOrg">See <see cref="ConsumerOrg"/>.</param>
    public MaskinPortenCacheKey(
        string clientName,
        string clientId,
        string scope,
        string? resource,
        string? consumerOrg)
    {
        ClientName = clientName;
        ClientId = clientId;
        Scope = scope;
        Resource = resource;
        ConsumerOrg = consumerOrg;
    }

    /// <summary>
    /// Gets the name of the MaskinPorten client. Not sent to MaskinPorten.
    /// </summary>
    public string ClientName { get; }

    /// <summary>
    /// Gets the <c>client_id</c> used when getting tokens.
    /// </summary>
    /// <remarks>
    /// The client ID is also used as a configuration key.
    /// </remarks>
    public string ClientId { get; }

    /// <summary>
    /// Gets the <c>scope</c> used when getting tokens.
    /// </summary>
    public string Scope { get; }

    /// <summary>
    /// Gets the <c>resource</c> used when getting tokens (optional).
    /// </summary>
    public string? Resource { get; }

    /// <summary>
    /// Gets the <c>consumer_org</c> used when getting tokens (optional).
    /// </summary>
    public string? ConsumerOrg { get; }
}
