using System.Diagnostics;
using Altinn.Authorization.ModelUtils;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.TestUtils;
using Altinn.Register.TestUtils.TestData;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Register.Persistence.Tests.ImportJobs;

public class PostgresUserIdImportJobServiceTests
    : DatabaseTestBase
{
    protected override bool SeedData => false;

    private IUnitOfWorkManager? _manager;
    private IUnitOfWork? _uow;
    private IPartyPersistence? _party;
    private IImportJobStatePersistence? _jobState;
    private IUserIdImportJobService? _userIdImportJobService;

    protected override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        _manager = GetRequiredService<IUnitOfWorkManager>();
        await NewTransaction(commit: false);
    }

    protected override async ValueTask DisposeAsync()
    {
        if (_uow is { } uow)
        {
            await uow.DisposeAsync();
        }

        await base.DisposeAsync();
    }

    protected async Task NewTransaction(bool commit)
    {
        if (_uow is { } uow)
        {
            if (commit)
            {
                await uow.CommitAsync(CancellationToken);
            }
            else
            {
                await uow.RollbackAsync(CancellationToken);
            }

            await uow.DisposeAsync();
        }

        _uow = await _manager!.CreateAsync(activityName: "test", cancellationToken: CancellationToken);
        _party = _uow.GetRequiredService<IPartyPersistence>();
        _jobState = _uow.GetRequiredService<IImportJobStatePersistence>();
        _userIdImportJobService = _uow.GetRequiredService<IUserIdImportJobService>();
    }

    protected IUnitOfWork UoW
        => _uow!;

    protected IImportJobStatePersistence JobState
        => _jobState!;

    protected IPartyPersistence Party
        => _party!;

    protected IUserIdImportJobService UserIdImportJobService
        => _userIdImportJobService!;

    [Fact]
    public async Task ClearJobStateForPartiesWithUserId_Clears_Expected_State()
    {
        var people = await UoW.CreatePeople(3, cancellationToken: CancellationToken);
        await UoW.ExecuteNonQueries(
            [
                /*strpsql*/"""
                TRUNCATE register."user"
                """,
                /*strpsql*/"""
                TRUNCATE register."import_job_party_state"
                """,
            ],
            cancellationToken: CancellationToken);

        await Party.UpsertPartyUser(
            people[0].PartyUuid.Value,
            new(1U, FieldValue.Null, ImmutableValueArray.Create(1U)),
            cancellationToken: CancellationToken);
        await Party.UpsertPartyUser(
            people[1].PartyUuid.Value,
            new(2U, FieldValue.Null, ImmutableValueArray.Create(2U)),
            cancellationToken: CancellationToken);
        await JobState.SetPartyState(
            "test",
            people[0].PartyUuid.Value,
            new EmptyState(),
            cancellationToken: CancellationToken);
        await JobState.SetPartyState(
            "not-test",
            people[1].PartyUuid.Value,
            new EmptyState(),
            cancellationToken: CancellationToken);
        await JobState.SetPartyState(
            "test",
            people[2].PartyUuid.Value,
            new EmptyState(),
            cancellationToken: CancellationToken);

        // people[0]: has test state, has user id
        // people[1]: no  test state, has user id
        // people[2]: has test state, no  user id
        await UserIdImportJobService.ClearJobStateForPartiesWithUserId("test", cancellationToken: CancellationToken);
        (await JobState.GetPartyState<EmptyState>("test", people[0].PartyUuid.Value, cancellationToken: CancellationToken)).ShouldBeUnset();
        (await JobState.GetPartyState<EmptyState>("not-test", people[1].PartyUuid.Value, cancellationToken: CancellationToken)).ShouldBe(new EmptyState());
        (await JobState.GetPartyState<EmptyState>("test", people[2].PartyUuid.Value, cancellationToken: CancellationToken)).ShouldBe(new EmptyState());
    }

    public class GetPartiesWithoutUserIdAndJobStateTests
        : PostgresUserIdImportJobServiceTests
    {
        private static readonly IReadOnlySet<PartyRecordType> _partyTypes =
            new HashSet<PartyRecordType>
            {
                PartyRecordType.Person,
                PartyRecordType.SelfIdentifiedUser,
            };

        [Fact]
        public async Task EmptyParties_ReturnsEmpty()
        {
            var parties = await UserIdImportJobService.GetPartiesWithoutUserIdAndJobState("test", _partyTypes, cancellationToken: CancellationToken).ToListAsync(CancellationToken);

            parties.ShouldBeEmpty();
        }

        [Fact]
        public async Task NoPartiesWithUserIdOrJobState_ReturnsAll_Small()
        {
            var people = await UoW.CreatePeople(10, cancellationToken: CancellationToken);
            var siUsers = await UoW.CreateSelfIdentifiedUsers(10, cancellationToken: CancellationToken);
            await UoW.CreateOrgs(10, cancellationToken: CancellationToken);
            await UoW.ExecuteNonQueries(
                [
                    /*strpsql*/"""
                    TRUNCATE register."user"
                    """,
                    /*strpsql*/"""
                    TRUNCATE register."import_job_party_state"
                    """,
                ],
                cancellationToken: CancellationToken);

            var parties = await UserIdImportJobService.GetPartiesWithoutUserIdAndJobState("test", _partyTypes, cancellationToken: CancellationToken).ToListAsync(CancellationToken);

            parties.ShouldNotBeEmpty();
            parties.Count.ShouldBe(20);
            foreach (var party in parties)
            {
                _partyTypes.Contains(party.PartyType).ShouldBeTrue();

                switch (party.PartyType)
                {
                    case PartyRecordType.Person:
                        people.Any(p => p.PartyUuid.Value == party.PartyUuid).ShouldBeTrue();
                        break;

                    case PartyRecordType.SelfIdentifiedUser:
                        siUsers.Any(p => p.PartyUuid.Value == party.PartyUuid).ShouldBeTrue();
                        break;

                    default:
                        throw new UnreachableException();
                }
            }
        }

        [Fact]
        public async Task NoPartiesWithUserIdOrJobState_ReturnsAll_Large()
        {
            const int PAGES = 10;

            var dataGenerator = GetRequiredService<RegisterTestDataGenerator>();
            var toInsert = Enumerable.Range(0, PAGES)
                .ToAsyncEnumerable()
                .SelectMany(async (int _, CancellationToken cancellationToken) =>
                {
                    var people = await dataGenerator.GetPeopleData(101, cancellationToken: cancellationToken);
                    var siUsers = await dataGenerator.GetSelfIdentifiedUsersData(102, cancellationToken: cancellationToken);
                    var orgs = await dataGenerator.GetOrgsData(103, cancellationToken: cancellationToken);

                    IEnumerable<PartyRecord> items = [
                        .. people.As<PartyRecord>(),
                        .. siUsers.As<PartyRecord>(),
                        .. orgs.As<PartyRecord>(),
                    ];

                    return items;
                });

            await UoW.GetPartyPersistence().UpsertParties(toInsert, cancellationToken: CancellationToken).LastOrDefaultAsync(CancellationToken);
            await NewTransaction(commit: true);

            await UoW.ExecuteNonQueries(
                [
                    /*strpsql*/"""
                    TRUNCATE register."user"
                    """,
                    /*strpsql*/"""
                    TRUNCATE register."import_job_party_state"
                    """,
                ],
                cancellationToken: CancellationToken);
            await NewTransaction(commit: true);

            var peopleCount = 0U;
            var siUsersCount = 0U;

            await foreach (var party in UserIdImportJobService.GetPartiesWithoutUserIdAndJobState("test", _partyTypes, cancellationToken: CancellationToken))
            {
                _partyTypes.Contains(party.PartyType).ShouldBeTrue();
                switch (party.PartyType)
                {
                    case PartyRecordType.Person:
                        peopleCount++;
                        break;

                    case PartyRecordType.SelfIdentifiedUser:
                        siUsersCount++;
                        break;

                    default:
                        throw new UnreachableException();
                }
            }

            peopleCount.ShouldBe((uint)(PAGES * 101));
            siUsersCount.ShouldBe((uint)(PAGES * 102));
        }

        [Fact]
        public async Task DifferentStates()
        {
            var users = await UoW.CreateSelfIdentifiedUsers(4, cancellationToken: CancellationToken);
            await UoW.ExecuteNonQueries(
                [
                    /*strpsql*/"""
                    TRUNCATE register."user"
                    """,
                    /*strpsql*/"""
                    TRUNCATE register."import_job_party_state"
                    """,
                ],
                cancellationToken: CancellationToken);

            await NewTransaction(commit: true);

            await Party.UpsertParty(
                users[0] with
                {
                    User = new PartyUserRecord(userId: 1U, username: FieldValue.Unset, userIds: ImmutableValueArray.Create(1U)),
                },
                cancellationToken: CancellationToken);
            await JobState.SetPartyState(
                "test",
                users[1].PartyUuid.Value,
                new EmptyState(),
                cancellationToken: CancellationToken);
            await NewTransaction(commit: true);

            var parties = await UserIdImportJobService.GetPartiesWithoutUserIdAndJobState("test", _partyTypes, cancellationToken: CancellationToken).ToListAsync(CancellationToken);

            parties.ShouldNotBeEmpty();
            parties.Count.ShouldBe(2);
            foreach (var party in parties)
            {
                party.PartyType.ShouldBe(PartyRecordType.SelfIdentifiedUser);
                users.Any(p => p.PartyUuid.Value == party.PartyUuid).ShouldBeTrue();
            }
        }
    }

    private sealed record class EmptyState
        : IImportJobState<EmptyState>
    {
        public static string StateType => $"{nameof(EmptyState)}@0";
    }
}
