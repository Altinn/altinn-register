using Altinn.Authorization.ProblemDetails;
using Altinn.Register.Contracts;
using Altinn.Register.Contracts.V1;
using Altinn.Register.Core.Mediator;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.UnitOfWork;

namespace Altinn.Register.Core.Operations;

#pragma warning disable SA1649 // File name should match first type name

/// <summary>
/// Request to lookup v1 party names based on a set of identifiers.
/// </summary>
/// <param name="Parties">The party lookup criteria.</param>
/// <param name="PartyComponentOption">The party component options.</param>
public readonly record struct LookupV1PartyNamesRequest(
    IReadOnlyList<PartyLookup> Parties,
    PartyComponentOptions PartyComponentOption)
    : IRequest<PartyNamesLookupResult>;

/// <summary>
/// Lookup v1 party names from A2.
/// </summary>
internal sealed class LookupV1PartyNamesFromA2RequestHandler(IV1PartyService partyService)
    : IRequestHandler<LookupV1PartyNamesRequest, PartyNamesLookupResult>
{
    /// <inheritdoc/>
    public async ValueTask<Result<PartyNamesLookupResult>> Handle(LookupV1PartyNamesRequest request, CancellationToken cancellationToken)
    {
        List<PartyName> items = await partyService.LookupPartyNames(request.Parties, request.PartyComponentOption, cancellationToken).ToListAsync(cancellationToken);
        return new PartyNamesLookupResult
        {
            PartyNames = items,
        };
    }
}

/// <summary>
/// Lookup v1 party names from the local A3 database.
/// </summary>
internal sealed class LookupV1PartyNamesFromDBRequestHandler(IUnitOfWorkManager manager)
    : IRequestHandler<LookupV1PartyNamesRequest, PartyNamesLookupResult>
{
    /// <inheritdoc/>
    public async ValueTask<Result<PartyNamesLookupResult>> Handle(LookupV1PartyNamesRequest request, CancellationToken cancellationToken)
    {
        await using var uow = await manager.CreateAsync(cancellationToken, activityName: "lookup v1 party names");
        var persistence = uow.GetPartyPersistence();

        var organizationIdentifiers = new HashSet<OrganizationIdentifier>();
        var personIdentifiers = new HashSet<PersonIdentifier>();

        foreach (var lookup in request.Parties)
        {
            if (!string.IsNullOrEmpty(lookup.Ssn) && PersonIdentifier.TryParse(lookup.Ssn, provider: null, out var personIdentifier))
            {
                personIdentifiers.Add(personIdentifier);
                continue;
            }

            if (!string.IsNullOrEmpty(lookup.OrgNo) && OrganizationIdentifier.TryParse(lookup.OrgNo, provider: null, out var organizationIdentifier))
            {
                organizationIdentifiers.Add(organizationIdentifier);
            }
        }

        var include = PartyFieldIncludes.Party | PartyFieldIncludes.PersonShortName;
        if (request.PartyComponentOption.HasFlag(PartyComponentOptions.NameComponents))
        {
            include |= PartyFieldIncludes.PersonFirstName | PartyFieldIncludes.PersonMiddleName | PartyFieldIncludes.PersonLastName;
        }

        Dictionary<PersonIdentifier, PersonRecord>? persons = null;
        Dictionary<OrganizationIdentifier, OrganizationRecord>? organizations = null;

        if (personIdentifiers.Count > 0 || organizationIdentifiers.Count > 0)
        {
            await foreach (var party in persistence.LookupParties(
                organizationIdentifiers: organizationIdentifiers.Count > 0 ? organizationIdentifiers.ToList() : null,
                personIdentifiers: personIdentifiers.Count > 0 ? personIdentifiers.ToList() : null,
                include: include,
                cancellationToken: cancellationToken))
            {
                switch (party)
                {
                    case PersonRecord person when person.PersonIdentifier.HasValue:
                        persons ??= new();
                        persons[person.PersonIdentifier.Value] = person;
                        break;

                    case OrganizationRecord organization when organization.OrganizationIdentifier.HasValue:
                        organizations ??= new();
                        organizations[organization.OrganizationIdentifier.Value] = organization;
                        break;
                }
            }
        }

        var includePersonName = request.PartyComponentOption.HasFlag(PartyComponentOptions.NameComponents);
        List<PartyName> items = new(request.Parties.Count);
        foreach (var lookup in request.Parties)
        {
            items.Add(CreateResult(lookup, persons, organizations, includePersonName));
        }

        return new PartyNamesLookupResult
        {
            PartyNames = items,
        };
    }

    private static PartyName CreateResult(
        PartyLookup lookup,
        IReadOnlyDictionary<PersonIdentifier, PersonRecord>? persons,
        IReadOnlyDictionary<OrganizationIdentifier, OrganizationRecord>? organizations,
        bool includePersonName)
    {
        var result = new PartyName
        {
            Ssn = lookup.Ssn,
            OrgNo = lookup.OrgNo,
        };

        if (!string.IsNullOrEmpty(lookup.Ssn)
            && PersonIdentifier.TryParse(lookup.Ssn, provider: null, out var personIdentifier)
            && persons?.TryGetValue(personIdentifier, out var person) == true)
        {
            result.Name = person.ShortName.HasValue ? person.ShortName.Value : person.DisplayName.Value;

            if (includePersonName)
            {
                result.PersonName = new PersonNameComponents
                {
                    FirstName = person.FirstName.Value,
                    MiddleName = person.MiddleName.Value,
                    LastName = person.LastName.Value,
                };
            }

            return result;
        }

        if (!string.IsNullOrEmpty(lookup.OrgNo)
            && OrganizationIdentifier.TryParse(lookup.OrgNo, provider: null, out var organizationIdentifier)
            && organizations?.TryGetValue(organizationIdentifier, out var organization) == true)
        {
            result.Name = organization.DisplayName.Value;
        }

        return result;
    }
}
