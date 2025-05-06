using System.Diagnostics;
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.Parties;
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
    private IImportJobStatePersistence? _jobState;
    private IUserIdImportJobService? _userIdImportJobService;

    protected override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        _manager = GetRequiredService<IUnitOfWorkManager>();
        _uow = await _manager.CreateAsync(activityName: "test");
        _jobState = _uow.GetRequiredService<IImportJobStatePersistence>();
        _userIdImportJobService = _uow.GetRequiredService<IUserIdImportJobService>();
    }

    protected override async ValueTask DisposeAsync()
    {
        if (_uow is { } uow)
        {
            await uow.DisposeAsync();
        }

        await base.DisposeAsync();
    }

    protected IUnitOfWork UoW
        => _uow!;

    protected IImportJobStatePersistence JobState
        => _jobState!;

    protected IUserIdImportJobService UserIdImportJobService
        => _userIdImportJobService!;

    public class GetPartiesWithoutUserIdAndJobStateTests
        : PostgresUserIdImportJobServiceTests
    {
        private static readonly IReadOnlySet<PartyType> _partyTypes = 
            new HashSet<PartyType>
            {
                PartyType.Person,
                PartyType.SelfIdentifiedUser,
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
                    case PartyType.Person:
                        people.Should().Contain(p => p.PartyUuid.Value == party.PartyUuid);
                        break;

                    case PartyType.SelfIdentifiedUser:
                        siUsers.Should().Contain(p => p.PartyUuid.Value == party.PartyUuid);
                        break;

                    default:
                        throw new UnreachableException();
                }
            });
        }
    }
}
