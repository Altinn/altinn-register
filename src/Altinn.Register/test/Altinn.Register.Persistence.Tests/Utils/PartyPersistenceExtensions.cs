using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Altinn.Register.Contracts.ExternalRoles;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Register.Persistence.Tests.Utils;

/// <summary>
/// Test helpers for <see cref="IPartyPersistence"/>, <see cref="IPartyExternalRolePersistence"/>.
/// </summary>
public static class PartyPersistenceExtensions
{
    public static async Task<OrganizationIdentifier> GetNewOrgNumber(this IUnitOfWork uow)
    {
        var connection = uow.GetRequiredService<NpgsqlConnection>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            /*strpsql*/"""
            SELECT organization_identifier 
            FROM register.party 
            WHERE organization_identifier = @id
            """;

        var param = cmd.Parameters.Add<string>("id", NpgsqlDbType.Text);
        await cmd.PrepareAsync();

        OrganizationIdentifier id;
        do
        {
            id = GenerateOrganizationIdentifier();
        }
        while (await InUse(id));

        return id;

        async Task<bool> InUse(OrganizationIdentifier id)
        {
            param.TypedValue = id.ToString();

            await using var reader = await cmd.ExecuteReaderAsync();
            var exists = await reader.ReadAsync();
            return exists;
        }

        static OrganizationIdentifier GenerateOrganizationIdentifier()
        {
            Vector128<ushort> weights = Vector128.Create((ushort)3, 2, 7, 6, 5, 4, 3, 2);

            while (true)
            {
                // 8 digit random number
                var random = Random.Shared.Next(10_000_000, 99_999_999);
                Span<char> s = stackalloc char[9];
                Debug.Assert(random.TryFormat(s, out var written, provider: CultureInfo.InvariantCulture));
                Debug.Assert(written == 8);

                ReadOnlySpan<ushort> chars = MemoryMarshal.Cast<char, ushort>(s);

                Vector128<ushort> zeroDigit = Vector128.Create('0', '0', '0', '0', '0', '0', '0', '0');
                Vector128<ushort> charsVec = Vector128.Create(chars);

                var sum = Vector128.Sum((charsVec - zeroDigit) * weights);

                var ctrlDigit = 11 - (sum % 11);
                if (ctrlDigit == 11)
                {
                    ctrlDigit = 0;
                }

                if (ctrlDigit == 10)
                {
                    continue;
                }

                Debug.Assert(ctrlDigit is >= 0 and <= 9, $"ctrlDigit was {ctrlDigit}");
                s[8] = (char)('0' + ctrlDigit);

                return OrganizationIdentifier.Parse(new string(s));
            }
        }
    }

    public static async Task<PersonIdentifier> GetNewPersonIdentifier(this IUnitOfWork uow, DateOnly birthDate, bool isDNumber)
    {
        var connection = uow.GetRequiredService<NpgsqlConnection>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            /*strpsql*/"""
            SELECT person_identifier 
            FROM register.party 
            WHERE person_identifier = @id
            """;

        var param = cmd.Parameters.Add<string>("id", NpgsqlDbType.Text);
        await cmd.PrepareAsync();

        PersonIdentifier id;
        do
        {
            id = GeneratePersonIdentifier(birthDate, isDNumber);
        }
        while (await InUse(id));

        return id;

        async Task<bool> InUse(PersonIdentifier id)
        {
            param.TypedValue = id.ToString();

            await using var reader = await cmd.ExecuteReaderAsync();
            var exists = await reader.ReadAsync();
            return exists;
        }

        static PersonIdentifier GeneratePersonIdentifier(DateOnly dateComp, bool isDNumber)
        {
            Vector256<ushort> k1weights = Vector256.Create((ushort)3, 7, 6, 1, 8, 9, 4, 5, 2, 0, 0, 0, 0, 0, 0, 0);
            Vector256<ushort> k2weights = Vector256.Create((ushort)5, 4, 3, 2, 7, 6, 5, 4, 3, 2, 0, 0, 0, 0, 0, 0);
            Span<ushort> k1_candidates = stackalloc ushort[4];

            var random = Random.Shared;

            var dayOffset = isDNumber ? 40 : 0;
            int written;

            while (true)
            {
                var individualNumber = random.Next(0, 1000);
                Span<char> s = stackalloc char[11];
                s.Fill('0');

                var day = dateComp.Day + dayOffset;
                var month = dateComp.Month;
                var year = dateComp.Year % 100;

                Debug.Assert(day.TryFormat(s, out written, "D2", provider: CultureInfo.InvariantCulture));
                Debug.Assert(written == 2);

                Debug.Assert(month.TryFormat(s.Slice(2), out written, "D2", provider: CultureInfo.InvariantCulture));
                Debug.Assert(written == 2);

                Debug.Assert(year.TryFormat(s.Slice(4), out written, "D2", provider: CultureInfo.InvariantCulture));
                Debug.Assert(written == 2);

                Debug.Assert(individualNumber.TryFormat(s.Slice(6), out written, "D3", provider: CultureInfo.InvariantCulture));
                Debug.Assert(written == 3);

                Vector256<ushort> digits = CreateVector(s);

                var k1c_base = (ushort)(Vector256.Sum(digits * k1weights) % 11);
                var k1c_1 = (ushort)((11 - k1c_base) % 11);
                var k1c_2 = (ushort)((12 - k1c_base) % 11);
                var k1c_3 = (ushort)((13 - k1c_base) % 11);
                var k1c_4 = (ushort)((14 - k1c_base) % 11);

                var idx = 0;
                AddIfValid(k1_candidates, ref idx, k1c_1);
                AddIfValid(k1_candidates, ref idx, k1c_2);
                AddIfValid(k1_candidates, ref idx, k1c_3);
                AddIfValid(k1_candidates, ref idx, k1c_4);

                var k1 = k1_candidates[random.Next(0, idx)];
                Debug.Assert(k1.TryFormat(s.Slice(9), out written, "D1", provider: CultureInfo.InvariantCulture));
                Debug.Assert(written == 1);

                digits = CreateVector(s);
                var k2 = (ushort)((11 - (Vector256.Sum(digits * k2weights) % 11)) % 11);

                if (k2 == 10)
                {
                    continue;
                }

                Debug.Assert(k2.TryFormat(s.Slice(10), out written, "D1", provider: CultureInfo.InvariantCulture));
                Debug.Assert(written == 1);

                if (!PersonIdentifier.TryParse(s, provider: null, out var result))
                {
                    Assert.Fail($"Generated illegal person identifier: {new string(s)}");
                }

                return result;
            }
        }

        static void AddIfValid(Span<ushort> candidates, ref int idx, ushort value)
        {
            if (value != 10)
            {
                candidates[idx++] = value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector256<ushort> CreateVector(ReadOnlySpan<char> s)
        {
            Debug.Assert(s.Length == 11);

            Span<ushort> c = stackalloc ushort[16];
            c.Clear(); // zero out the vector
            MemoryMarshal.Cast<char, ushort>(s).CopyTo(c);

            var chars = Vector256.Create<ushort>(c);
            var zeros = Vector256.Create('0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', 0, 0, 0, 0, 0);

            return chars - zeros;
        }
    }

    public static DateOnly GetRandomBirthDate(this IUnitOfWork uow)
    {
        var min = new DateOnly(1940, 01, 01);
        var maxExl = new DateOnly(2024, 01, 01);
        var value = Random.Shared.Next(min.DayNumber, maxExl.DayNumber);

        return DateOnly.FromDayNumber(value);
    }

    public static async Task<int> GetNextPartyId(this IUnitOfWork uow)
    {
        var connection = uow.GetRequiredService<NpgsqlConnection>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            /*strpsql*/"""
            SELECT MAX(id) FROM register.party
            """;

        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) + 1;
    }

    public static async Task<OrganizationRecord> CreateOrg(
        this IUnitOfWork uow,
        FieldValue<Guid> uuid = default,
        FieldValue<int> id = default,
        FieldValue<string> name = default,
        FieldValue<OrganizationIdentifier> identifier = default,
        FieldValue<DateTimeOffset> createdAt = default,
        FieldValue<DateTimeOffset> modifiedAt = default,
        FieldValue<bool> isDeleted = default,
        FieldValue<string> unitStatus = default,
        FieldValue<string> unitType = default,
        FieldValue<string> telephoneNumber = default,
        FieldValue<string> mobileNumber = default,
        FieldValue<string> faxNumber = default,
        FieldValue<string> emailAddress = default,
        FieldValue<string> internetAddress = default,
        FieldValue<MailingAddress> mailingAddress = default,
        FieldValue<MailingAddress> businessAddress = default)
    {
        var connection = uow.GetRequiredService<NpgsqlConnection>();

        if (!id.HasValue)
        {
            id = await GetNextPartyId(uow);
        }

        if (!identifier.HasValue)
        {
            identifier = await GetNewOrgNumber(uow);
        }

        var result = await uow.GetRequiredService<IPartyPersistence>().UpsertParty(new OrganizationRecord
        {
            PartyUuid = uuid.HasValue ? uuid.Value : Guid.NewGuid(),
            PartyId = id,
            DisplayName = name.HasValue ? name.Value : "Test",
            PersonIdentifier = null,
            OrganizationIdentifier = identifier,
            CreatedAt = createdAt.HasValue ? createdAt.Value : uow.GetRequiredService<TimeProvider>().GetUtcNow(),
            ModifiedAt = modifiedAt.HasValue ? modifiedAt.Value : uow.GetRequiredService<TimeProvider>().GetUtcNow(),
            IsDeleted = isDeleted.HasValue ? isDeleted.Value : false,
            VersionId = FieldValue.Unset,
            UnitStatus = unitStatus.HasValue ? unitStatus.Value : "N",
            UnitType = unitType.HasValue ? unitType.Value : "AS",
            TelephoneNumber = telephoneNumber.HasValue ? telephoneNumber.Value : null,
            MobileNumber = mobileNumber.HasValue ? mobileNumber.Value : null,
            FaxNumber = faxNumber.HasValue ? faxNumber.Value : null,
            EmailAddress = emailAddress.HasValue ? emailAddress.Value : null,
            InternetAddress = internetAddress.HasValue ? internetAddress.Value : null,
            MailingAddress = mailingAddress.HasValue ? mailingAddress.Value : null,
            BusinessAddress = businessAddress.HasValue ? businessAddress.Value : null,
        });

        Assert.True(result.IsSuccess);
        return (OrganizationRecord)result.Value;
    }

    public static async Task AddRole(
        this IUnitOfWork uow,
        ExternalRoleSource roleSource,
        string roleIdentifier,
        Guid from,
        Guid to)
    {
        var connection = uow.GetRequiredService<NpgsqlConnection>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            /*strpsql*/"""
            INSERT INTO register.external_role_assignment (source, identifier, from_party, to_party)
            VALUES (@source, @identifier, @from, @to)
            """;

        cmd.Parameters.Add<ExternalRoleSource>("source").TypedValue = roleSource;
        cmd.Parameters.Add<string>("identifier", NpgsqlDbType.Text).TypedValue = roleIdentifier;
        cmd.Parameters.Add<Guid>("from", NpgsqlDbType.Uuid).TypedValue = from;
        cmd.Parameters.Add<Guid>("to", NpgsqlDbType.Uuid).TypedValue = to;

        await cmd.PrepareAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task CreateFakeRoleDefinitions(
        this IUnitOfWork uow)
    {
        await CreateFakeRoleDefinitions(uow, ExternalRoleSource.CentralCoordinatingRegister);
        await CreateFakeRoleDefinitions(uow, ExternalRoleSource.NationalPopulationRegister);
    }

    public static async Task CreateFakeRoleDefinitions(
        this IUnitOfWork uow,
        ExternalRoleSource source)
    {
        for (var i = 0; i < 40; i++)
        {
            await CreateFakeRoleDefinition(uow, source, $"fake-{i:D2}");
        }
    }

    public static async Task CreateFakeRoleDefinition(
        this IUnitOfWork uow,
        ExternalRoleSource source,
        string identifier)
    {
        const string QUERY =
            /*strpsql*/"""
            INSERT INTO register.external_role_definition (source, identifier, name, description)
            VALUES (@source, @identifier, @name, @name)
            """;

        var conn = uow.GetRequiredService<NpgsqlConnection>();
        var name = new Dictionary<string, string>
        {
            ["en"] = $"Fake role {identifier}",
            ["nb"] = $"Falsk rolle {identifier}",
            ["nn"] = $"Falsk rolle {identifier}",
        };

        var cmd = conn.CreateCommand();
        cmd.CommandText = QUERY;

        cmd.Parameters.Add<ExternalRoleSource>("source").TypedValue = source;
        cmd.Parameters.Add<string>("identifier", NpgsqlDbType.Text).TypedValue = identifier;
        cmd.Parameters.Add<Dictionary<string, string>>("name", NpgsqlDbType.Hstore).TypedValue = name;

        await cmd.PrepareAsync();
        await cmd.ExecuteNonQueryAsync();
    }
}
