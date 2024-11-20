#nullable enable

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.PartyImport.A2;
using Altinn.Register.Utils;

using V1Models = Altinn.Platform.Register.Models;

namespace Altinn.Register.PartyImport.A2;

/// <summary>
/// Implementation of <see cref="IA2PartyImportService"/>.
/// </summary>
internal sealed class A2PartyImportService
    : IA2PartyImportService
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="A2PartyImportService"/> class.
    /// </summary>
    public A2PartyImportService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public IA2PartyChanges GetChanges(uint fromExclusive = 0, CancellationToken cancellationToken = default)
        => new A2PartyChanges(this, fromExclusive, cancellationToken);

    /// <inheritdoc />
    public async Task<PartyRecord> GetParty(Guid partyUuid, CancellationToken cancellationToken = default)
    {
        var url = $"parties/{partyUuid}";

        var response = await _httpClient.GetFromJsonAsync<V1Models.Party>(url, _options, cancellationToken);

        if (response is null)
        {
            throw new PartyNotFoundException(partyUuid);
        }

        return MapParty(response);
    }

    private async Task<PartyChangesResponse> GetChangesPage(uint fromExclusive, CancellationToken cancellationToken)
    {
        var url = $"parties/partychanges/{fromExclusive}";

        var response = await _httpClient.GetFromJsonAsync<PartyChangesResponse>(url, _options, cancellationToken);
        if (response is null)
        {
            throw new InvalidOperationException("Failed to parse party changes.");
        }

        return response;
    }

    private sealed class A2PartyChanges
        : IA2PartyChanges
        , IAsyncEnumerator<A2PartyChange>
    {
        private readonly AsyncLock _lock = new();
        private readonly A2PartyImportService _service;
        private uint _fromExclusive;
        private bool _endOfData;
        private CancellationTokenSource? _combinedTokens;
        private CancellationToken _cancellationToken;
        private PartyChangesResponse? _response;
        private IEnumerator<PartyChange>? _enumerator;
        private A2PartyChange? _current;

        public A2PartyChanges(A2PartyImportService service, uint fromExclusive, CancellationToken cancellationToken)
        {
            _service = service;
            _fromExclusive = fromExclusive;
            _cancellationToken = cancellationToken;
        }

        [DebuggerHidden]
        A2PartyChange IAsyncEnumerator<A2PartyChange>.Current
            => _current!;

        ValueTask IAsyncDisposable.DisposeAsync()
        {
            _combinedTokens?.Dispose();

            return ValueTask.CompletedTask;
        }

        IAsyncEnumerator<A2PartyChange> IAsyncEnumerable<A2PartyChange>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            if (_cancellationToken.Equals(default))
            {
                _cancellationToken = cancellationToken;
            }
            else if (cancellationToken.Equals(_cancellationToken) || cancellationToken.Equals(default))
            {
                // same or default token, do nothing
            }
            else
            {
                _combinedTokens = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, cancellationToken);
                _cancellationToken = _combinedTokens.Token;
            }

            return this;
        }

        ValueTask<bool> IAsyncEnumerator<A2PartyChange>.MoveNextAsync()
        {
            if (_endOfData)
            {
                return new(false);
            }

            // happy path - we already have local data
            if (_enumerator is { } enumerator && enumerator.MoveNext())
            {
                _current = Map(enumerator.Current);
                return new(true);
            }

            return new(MoveNextAsyncCore());
        }

        ValueTask<uint> IA2PartyChanges.GetLastChangeId(CancellationToken cancellationToken)
        {
            if (_response is { } response)
            {
                return new(response.LastAvailableChange);
            }

            return new(GetLastChangeIdCore(cancellationToken));
        }

        private async Task<bool> MoveNextAsyncCore()
        {
            await EnsurePage(fetchNext: true);
            return await ((IAsyncEnumerator<A2PartyChange>)this).MoveNextAsync();
        }

        private async Task<uint> GetLastChangeIdCore(CancellationToken cancellationToken)
        {
            if (_response is { } response)
            {
                return response.LastAvailableChange;
            }

            await EnsurePage(fetchNext: true).WaitAsync(cancellationToken);
            return await GetLastChangeIdCore(cancellationToken);
        }

        private async Task EnsurePage(bool fetchNext)
        {
            var cancellationToken = _cancellationToken;
            using var ticket = await _lock.Acquire(cancellationToken);

            // if we don't need to fetch the next page, and a response is already present,
            // somebody else has already done the work for us
            if (!fetchNext && _response is not null)
            {
                return;
            }

            _enumerator?.Dispose();
            _enumerator = null;
            _response = null;

            var fromExclusive = _fromExclusive;
            var response = await _service.GetChangesPage(fromExclusive, cancellationToken);

            _fromExclusive = response.LastChangeInSegment;
            _response = response;
            _endOfData = response.PartyChangeList.Count == 0;
            _enumerator = response.PartyChangeList.GetEnumerator();
        }

        private static A2PartyChange Map(PartyChange change) 
            => new()
            {
                ChangeId = change.ChangeId,
                PartyId = change.PartyId,
                PartyUuid = change.PartyUuid,
                ChangeTime = change.LastChangedTime
            };
    }

    private sealed class PartyChange
    {
        [JsonPropertyName("ChangeId")]
        public required uint ChangeId { get; init; }

        [JsonPropertyName("PartyId")]
        public required int PartyId { get; init; }

        [JsonPropertyName("PartyUuid")]
        public required Guid PartyUuid { get; init; }

        [JsonPropertyName("LastChangedTime")]
        public required DateTimeOffset LastChangedTime { get; init; }
    }

    private sealed class PartyChangesResponse
    {
        [JsonPropertyName("PartyChangeList")]
        public required IReadOnlyList<PartyChange> PartyChangeList { get; init; }

        [JsonPropertyName("LastAvailableChange")]
        public required uint LastAvailableChange { get; init; }

        [JsonPropertyName("LastChangeInSegment")]
        public required uint LastChangeInSegment { get; init; }
    }

    private sealed class PartyNotFoundException(Guid partyUuid)
        : InvalidOperationException($"Party {partyUuid} not found")
    {
        /// <summary>
        /// Gets the UUID of the party that was not found.
        /// </summary>
        public Guid PartyUuid => partyUuid;
    }
}
