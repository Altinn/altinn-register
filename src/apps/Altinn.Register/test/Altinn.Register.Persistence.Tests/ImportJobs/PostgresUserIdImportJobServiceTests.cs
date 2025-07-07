using System.Collections.Immutable;
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
                await uow.CommitAsync();
            }
            else
            {
                await uow.RollbackAsync();
            }

            await uow.DisposeAsync();
        }

        _uow = await _manager!.CreateAsync(activityName: "test");
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
            var parties = await UserIdImportJobService.GetPartiesWithoutUserIdAndJobState("test", _partyTypes).ToListAsync();

            parties.Should().BeEmpty();
        }

        [Fact]
        public async Task NoPartiesWithUserIdOrJobState_ReturnsAll_Small()
        {
            var people = await UoW.CreatePeople(10);
            var siUsers = await UoW.CreateSelfIdentifiedUsers(10);
            await UoW.CreateOrgs(10);
            await UoW.ExecuteNonQueries([
                /*strpsql*/"""
                TRUNCATE register."user"
                """,
                /*strpsql*/"""
                TRUNCATE register."import_job_party_state"
                """,
            ]);

            var parties = await UserIdImportJobService.GetPartiesWithoutUserIdAndJobState("test", _partyTypes).ToListAsync();

            parties.Should().NotBeEmpty();
            parties.Should().HaveCount(20);
            parties.Should().AllSatisfy(party =>
            {
                party.PartyType.Should().BeOneOf(_partyTypes);

                switch (party.PartyType)
                {
                    case PartyRecordType.Person:
                        people.Should().Contain(p => p.PartyUuid.Value == party.PartyUuid);
                        break;

                    case PartyRecordType.SelfIdentifiedUser:
                        siUsers.Should().Contain(p => p.PartyUuid.Value == party.PartyUuid);
                        break;

                    default:
                        throw new UnreachableException();
                }
            });
        }

        [Fact]
        public async Task NoPartiesWithUserIdOrJobState_ReturnsAll_Large()
        {
            const int PAGES = 10;

            var dataGenerator = GetRequiredService<RegisterTestDataGenerator>();
            var toInsert = Enumerable.Range(0, PAGES)
                .ToAsyncEnumerable()
                .SelectManyAwait(async _ =>
                {
                    var people = await dataGenerator.GetPeopleData(101);
                    var siUsers = await dataGenerator.GetSelfIdentifiedUsersData(102);
                    var orgs = await dataGenerator.GetOrgsData(103);

                    IEnumerable<PartyRecord> items = [
                        .. people.As<PartyRecord>(),
                        .. siUsers.As<PartyRecord>(),
                        .. orgs.As<PartyRecord>(),
                    ];

                    return items.ToAsyncEnumerable();
                });

            await UoW.GetPartyPersistence().UpsertParties(toInsert).LastOrDefaultAsync();
            await NewTransaction(commit: true);

            await UoW.ExecuteNonQueries([
                /*strpsql*/"""
                TRUNCATE register."user"
                """,
                /*strpsql*/"""
                TRUNCATE register."import_job_party_state"
                """,
            ]);
            await NewTransaction(commit: true);

            var peopleCount = 0U;
            var siUsersCount = 0U;

            await foreach (var party in UserIdImportJobService.GetPartiesWithoutUserIdAndJobState("test", _partyTypes))
            {
                party.PartyType.Should().BeOneOf(_partyTypes);
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

            peopleCount.Should().Be(PAGES * 101);
            siUsersCount.Should().Be(PAGES * 102);
        }

        [Fact]
        public async Task DifferentStates()
        {
            var users = await UoW.CreateSelfIdentifiedUsers(4);
            await UoW.ExecuteNonQueries([
                /*strpsql*/"""
                TRUNCATE register."user"
                """,
                /*strpsql*/"""
                TRUNCATE register."import_job_party_state"
                """,
            ]);

            await NewTransaction(commit: true);

            await Party.UpsertParty(users[0] with
            {
                User = new PartyUserRecord(userId: 1U, username: FieldValue.Unset, userIds: ImmutableValueArray.Create(1U)),
            });
            await JobState.SetPartyState("test", users[1].PartyUuid.Value, new EmptyState());
            await NewTransaction(commit: true);

            var parties = await UserIdImportJobService.GetPartiesWithoutUserIdAndJobState("test", _partyTypes).ToListAsync();

            parties.Should().NotBeEmpty();
            parties.Should().HaveCount(2);
            parties.Should().AllSatisfy(party =>
            {
                party.PartyType.Should().Be(PartyRecordType.SelfIdentifiedUser);
                users.Should().Contain(p => p.PartyUuid.Value == party.PartyUuid);
            });
        }

        private sealed record class EmptyState
            : IImportJobState<EmptyState>
        {
            public static string StateType => $"{nameof(EmptyState)}@0";
        }
    }
}
