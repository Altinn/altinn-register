using Altinn.Register.Core.Parties;
using Npgsql;
using Xunit.Abstractions;

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
    public async Task MapsMailingAddress(SerializableMailingAddress? address)
    {
        var value = address?.ToMailingAddress();

        var source = GetRequiredService<NpgsqlDataSource>();
        await using var conn = await source.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = /*strpsql*/"SELECT @p::register.mailing_address";

        var param = cmd.Parameters.Add<MailingAddress?>("p");
        param.TypedValue = value;

        await cmd.PrepareAsync();

        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();

        var result = reader.GetFieldValueOrDefault<MailingAddress>(0);
        result.Should().Be(value);
    }

    public static TheoryData<SerializableMailingAddress?> MailingAddresses => new()
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
    public async Task MapsStreetAddress(SerializableStreetAddress? address)
    {
        var value = address?.ToStreetAddress();

        var source = GetRequiredService<NpgsqlDataSource>();
        await using var conn = await source.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = /*strpsql*/"SELECT @p::register.street_address";

        var param = cmd.Parameters.Add<StreetAddress?>("p");
        param.TypedValue = value;

        await cmd.PrepareAsync();

        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();

        var result = reader.GetFieldValueOrDefault<StreetAddress>(0);
        result.Should().Be(value);
    }

    public static TheoryData<SerializableStreetAddress?> StreetAddresses => new()
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

    public sealed class SerializableMailingAddress
        : IXunitSerializable
    {
        public string? Address { get; set; }

        public string? City { get; set; }

        public string? PostalCode { get; set; }

        public MailingAddress ToMailingAddress() 
            => new MailingAddress
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

        public static implicit operator SerializableMailingAddress(MailingAddress address)
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

        public StreetAddress ToStreetAddress()
            => new StreetAddress
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

        public static implicit operator SerializableStreetAddress(StreetAddress address)
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
