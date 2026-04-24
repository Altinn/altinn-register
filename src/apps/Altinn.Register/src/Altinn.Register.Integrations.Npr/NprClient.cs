using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Altinn.Authorization.ProblemDetails;
using Altinn.Authorization.ProblemDetails.Validation;
using Altinn.Register.Contracts;
using Altinn.Register.Core.Errors;
using Altinn.Register.Core.Location;
using Altinn.Register.Core.Npr;
using Altinn.Register.Integrations.Npr.Person;

namespace Altinn.Register.Integrations.Npr;

/// <summary>
/// Implementation of <see cref="INprClient"/>.
/// </summary>
internal sealed class NprClient
    : INprClient
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    private const string BasePath = "folkeregisteret/offentlig-med-hjemmel/api/v1/personer";
    private const string PartsQueryParams = "part=navn&part=foedsel&part=bostedsadresse&part=doedsfall&part=status&part=oppholdsadresse&part=familierelasjon&part=postadresse&part=postadresseIUtlandet&part=vergemaalEllerFremtidsfullmakt&part=sivilstand&part=statsborgerskap&part=historikk&part=identifikasjonsnummer&part=deltBosted&part=adressebeskyttelse&part=utenlandskPersonidentifikasjon&part=foreldreansvar&part=innflytting&part=utflytting&part=foedselINorge&part=opphold&part=RettsligHandleevne&part=bibehold&part=brukAvSamiskSpraak";

    private readonly HttpClient _client;
    private readonly ILocationLookupProvider _lookupProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="NprClient"/> class.
    /// </summary>
    public NprClient(
        HttpClient client,
        ILocationLookupProvider lookupProvider)
    {
        _client = client;
        _lookupProvider = lookupProvider;
    }

    /// <inheritdoc/>
    public async Task<Result<NprPerson>> GetPerson(PersonIdentifier personIdentifier, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync($"{BasePath}/{personIdentifier}?{PartsQueryParams}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return Problems.PersonNotFound.Create([new("person.source", "npr")]);
        }

        if (!response.IsSuccessStatusCode)
        {
            return Problems.PartyFetchFailed.Create(
                detail: $"NPR API responded with status code {response.StatusCode}",
                [
                    new("person.source", "npr"),
                    new("http.status_code", ((int)response.StatusCode).ToString()),
                ]);
        }

        PersonDocument? personDocument;
        try
        {
            personDocument = await response.Content.ReadFromJsonAsync<PersonDocument>(_options, cancellationToken);
        }
        catch (JsonException ex)
        {
            return Problems.PartyFetchFailed.Create(
                detail: "Response deserialization failed",
                [
                    new("person.source", "npr"),
                    new("json.error", ex.Message),
                ]);
        }

        if (personDocument is null)
        {
            return Problems.PartyFetchFailed.Create(
                detail: "Response deserialization resulted in null",
                [
                    new("person.source", "npr"),
                ]);
        }

        var lookup = await _lookupProvider.GetLocationLookup(cancellationToken);

        ValidationProblemBuilder builder = default;
        builder.TryValidate(path: "/", personDocument, new PersonDocumentValidator(lookup), out NprPerson? validated);

        if (builder.TryBuild(out var error))
        {
            return error;
        }

        Debug.Assert(validated is not null);
        return validated;
    }
}
