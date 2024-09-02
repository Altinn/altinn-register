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

    [Theory]
    [MemberData(nameof(MailingAddresses))]
    public async Task MapsMailingAddress(MailingAddress? address)
    {
        var source = GetRequiredService<NpgsqlDataSource>();
        await using var conn = await source.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = /*strpsql*/"SELECT @p::register.mailing_address";

        var param = cmd.Parameters.Add<MailingAddress?>("p");
        param.TypedValue = address;

        await cmd.PrepareAsync();

        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();

        var result = reader.GetFieldValueOrDefault<MailingAddress>(0);
        result.Should().Be(address);
    }

    public static TheoryData<MailingAddress?> MailingAddresses => new()
    {
        null,
        new MailingAddress { Address = null, City = null, PostalCode = null },
        new MailingAddress { Address = "address", City = "city", PostalCode = "postal_code" },
        new MailingAddress { Address = "address", City = null, PostalCode = null },
        new MailingAddress { Address = null, City = "city", PostalCode = null },
        new MailingAddress { Address = null, City = null, PostalCode = "postal_code" },
    };

    [Theory]
    [MemberData(nameof(StreetAddresses))]
    public async Task MapsStreetAddress(StreetAddress? address)
    {
        var source = GetRequiredService<NpgsqlDataSource>();
        await using var conn = await source.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = /*strpsql*/"SELECT @p::register.street_address";

        var param = cmd.Parameters.Add<StreetAddress?>("p");
        param.TypedValue = address;

        await cmd.PrepareAsync();

        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();

        var result = reader.GetFieldValueOrDefault<StreetAddress>(0);
        result.Should().Be(address);
    }

    public static TheoryData<StreetAddress?> StreetAddresses => new()
    {
        null,
        new StreetAddress 
        {
            MunicipalNumber = "municipal_number",
            MunicipalName = "municipal_name",
            StreetName = "street_name",
            HouseNumber = "house_number",
            HouseLetter = "house_letter",
            PostalCode = "postal_code",
            City = "city",
        },
        new StreetAddress { MunicipalNumber = "municipal_number" },
        new StreetAddress { MunicipalName = "municipal_name" },
        new StreetAddress { StreetName = "street_name" },
        new StreetAddress { HouseNumber = "house_number" },
        new StreetAddress { HouseLetter = "house_letter" },
        new StreetAddress { PostalCode = "postal_code" },
        new StreetAddress { City = "city" },
    };
}
