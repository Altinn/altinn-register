using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.TestUtils;
using Npgsql;
using Xunit.Abstractions;

namespace Altinn.Register.Persistence.Tests;

public class DataSourceMappingsTests
    : DatabaseTestBase
{
    [Theory]
    [InlineData(PartyRecordType.Person)]
    [InlineData(PartyRecordType.Organization)]
    public async Task MapsPartyType(PartyRecordType partyType)
    {
        var source = GetRequiredService<NpgsqlDataSource>();
        await using var conn = await source.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = /*strpsql*/"SELECT @p::register.party_type";

        var param = cmd.Parameters.Add<PartyRecordType>("p");
        param.TypedValue = partyType;

        await cmd.PrepareAsync();

        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();

        var result = await reader.GetFieldValueAsync<PartyRecordType>(0);
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

        var result = await reader.GetFieldValueAsync<PartySource>(0);
        result.Should().Be(partySource);
    }

    [Theory]
    [MemberData(nameof(MailingAddresses))]
    public async Task MapsMailingAddress(SerializableMailingAddress? address)
    {
        var value = address?.ToMailingAddress();

        var source = GetRequiredService<NpgsqlDataSource>();
        await using var conn = await source.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = /*strpsql*/"SELECT @p::register.mailing_address";

        var param = cmd.Parameters.Add<MailingAddressRecord?>("p");
        param.TypedValue = value;

        await cmd.PrepareAsync();

        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();

        var result = await reader.GetFieldValueOrDefaultAsync<MailingAddressRecord>(0);
        result.Should().Be(value);
    }

    public static TheoryData<SerializableMailingAddress?> MailingAddresses => new()
    {
        null,
        new MailingAddressRecord { Address = null, City = null, PostalCode = null },
        new MailingAddressRecord { Address = "address", City = "city", PostalCode = "postal_code" },
        new MailingAddressRecord { Address = "address", City = null, PostalCode = null },
        new MailingAddressRecord { Address = null, City = "city", PostalCode = null },
        new MailingAddressRecord { Address = null, City = null, PostalCode = "postal_code" },
    };

    [Theory]
    [MemberData(nameof(StreetAddresses))]
    public async Task MapsStreetAddress(SerializableStreetAddress? address)
    {
        var value = address?.ToStreetAddress();

        var source = GetRequiredService<NpgsqlDataSource>();
        await using var conn = await source.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = /*strpsql*/"SELECT @p::register.street_address";

        var param = cmd.Parameters.Add<StreetAddressRecord?>("p");
        param.TypedValue = value;

        await cmd.PrepareAsync();

        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();

        var result = await reader.GetFieldValueOrDefaultAsync<StreetAddressRecord>(0);
        result.Should().Be(value);
    }

    public static TheoryData<SerializableStreetAddress?> StreetAddresses => new()
    {
        null,
        new StreetAddressRecord
        {
            MunicipalNumber = "municipal_number",
            MunicipalName = "municipal_name",
            StreetName = "street_name",
            HouseNumber = "house_number",
            HouseLetter = "house_letter",
            PostalCode = "postal_code",
            City = "city",
        },
        new StreetAddressRecord { MunicipalNumber = "municipal_number" },
        new StreetAddressRecord { MunicipalName = "municipal_name" },
        new StreetAddressRecord { StreetName = "street_name" },
        new StreetAddressRecord { HouseNumber = "house_number" },
        new StreetAddressRecord { HouseLetter = "house_letter" },
        new StreetAddressRecord { PostalCode = "postal_code" },
        new StreetAddressRecord { City = "city" },
    };

    public sealed class SerializableMailingAddress
        : IXunitSerializable
    {
        public string? Address { get; set; }

        public string? City { get; set; }

        public string? PostalCode { get; set; }

        public MailingAddressRecord ToMailingAddress() 
            => new MailingAddressRecord
            {
                Address = Address,
                City = City,
                PostalCode = PostalCode,
            };

        public void Deserialize(IXunitSerializationInfo info)
        {
            Address = info.GetValue<string?>(nameof(Address));
            City = info.GetValue<string?>(nameof(City));
            PostalCode = info.GetValue<string?>(nameof(PostalCode));
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(Address), Address);
            info.AddValue(nameof(City), City);
            info.AddValue(nameof(PostalCode), PostalCode);
        }

        public static implicit operator SerializableMailingAddress(MailingAddressRecord address)
            => new SerializableMailingAddress
            {
                Address = address.Address,
                City = address.City,
                PostalCode = address.PostalCode,
            };
    }

    public sealed class SerializableStreetAddress
        : IXunitSerializable
    {
        public string? MunicipalNumber { get; set; }

        public string? MunicipalName { get; set; }

        public string? StreetName { get; set; }

        public string? HouseNumber { get; set; }

        public string? HouseLetter { get; set; }

        public string? PostalCode { get; set; }

        public string? City { get; set; }

        public StreetAddressRecord ToStreetAddress()
            => new StreetAddressRecord
            {
                MunicipalNumber = MunicipalNumber,
                MunicipalName = MunicipalName,
                StreetName = StreetName,
                HouseNumber = HouseNumber,
                HouseLetter = HouseLetter,
                PostalCode = PostalCode,
                City = City,
            };

        public void Deserialize(IXunitSerializationInfo info)
        {
            MunicipalNumber = info.GetValue<string?>(nameof(MunicipalNumber));
            MunicipalName = info.GetValue<string?>(nameof(MunicipalName));
            StreetName = info.GetValue<string?>(nameof(StreetName));
            HouseNumber = info.GetValue<string?>(nameof(HouseNumber));
            HouseLetter = info.GetValue<string?>(nameof(HouseLetter));
            PostalCode = info.GetValue<string?>(nameof(PostalCode));
            City = info.GetValue<string?>(nameof(City));
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(MunicipalNumber), MunicipalNumber);
            info.AddValue(nameof(MunicipalName), MunicipalName);
            info.AddValue(nameof(StreetName), StreetName);
            info.AddValue(nameof(HouseNumber), HouseNumber);
            info.AddValue(nameof(HouseLetter), HouseLetter);
            info.AddValue(nameof(PostalCode), PostalCode);
            info.AddValue(nameof(City), City);
        }

        public static implicit operator SerializableStreetAddress(StreetAddressRecord address)
            => new SerializableStreetAddress
            {
                MunicipalNumber = address.MunicipalNumber,
                MunicipalName = address.MunicipalName,
                StreetName = address.StreetName,
                HouseNumber = address.HouseNumber,
                HouseLetter = address.HouseLetter,
                PostalCode = address.PostalCode,
                City = address.City,
            };
    }
}
