using Altinn.Register.Core.Parties;
using Npgsql;

namespace Altinn.Register.Persistence.Tests;

public class DataSourceMappingsTests
    : DatabaseTestBase
{
    [Theory]
    [InlineData(PartyType.Person)]
    [InlineData(PartyType.Organization)]
    public async Task MapsPartyType(PartyType partyType)
    {
        var source = GetRequiredService<NpgsqlDataSource>();
        await using var conn = await source.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = /*strpsql*/"SELECT @p::register.party_type";

        var param = cmd.Parameters.Add<PartyType>("p");
        param.TypedValue = partyType;

        await cmd.PrepareAsync();

        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();

        var result = reader.GetFieldValue<PartyType>(0);
        result.Should().Be(partyType);
    }

    [Theory]
    [InlineData(PartySource.NationalPopulationRegister)]
    [InlineData(PartySource.CentralCoordinatingRegister)]
    public async Task MapsPartySource(PartySource partySource)
    {
        var source = GetRequiredService<NpgsqlDataSource>();
        await using var conn = await source.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = /*strpsql*/"SELECT @p::register.party_source";

        var param = cmd.Parameters.Add<PartySource>("p");
        param.TypedValue = partySource;

        await cmd.PrepareAsync();

        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();

        var result = reader.GetFieldValue<PartySource>(0);
        result.Should().Be(partySource);
    }
}
