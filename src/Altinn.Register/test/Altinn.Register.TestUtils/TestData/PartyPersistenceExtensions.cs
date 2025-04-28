using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Altinn.Authorization.ModelUtils;
using Altinn.Register.Contracts.ExternalRoles;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.Core.Utils;
using Altinn.Register.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Register.TestUtils.TestData;

/// <summary>
/// Test helpers for <see cref="IPartyPersistence"/>, <see cref="IPartyExternalRolePersistence"/>.
/// </summary>
public static class PartyPersistenceExtensions
{
    public static async Task<OrganizationIdentifier> GetNewOrgNumber(this IUnitOfWork uow, CancellationToken cancellationToken = default)
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
        await cmd.PrepareAsync(cancellationToken);

        OrganizationIdentifier id;
        do
        {
            id = GenerateOrganizationIdentifier();
        }
        while (await InUse(id, cancellationToken));

        return id;

        async Task<bool> InUse(OrganizationIdentifier id, CancellationToken cancellationToken)
        {
            param.TypedValue = id.ToString();

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var exists = await reader.ReadAsync(cancellationToken);
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

    public static async Task<PersonIdentifier> GetNewPersonIdentifier(this IUnitOfWork uow, DateOnly birthDate, bool isDNumber, CancellationToken cancellationToken = default)
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
        await cmd.PrepareAsync(cancellationToken);

        PersonIdentifier id;
        do
        {
            id = GeneratePersonIdentifier(birthDate, isDNumber);
        }
        while (await InUse(id, cancellationToken));

        return id;

        async Task<bool> InUse(PersonIdentifier id, CancellationToken cancellationToken)
        {
            param.TypedValue = id.ToString();

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var exists = await reader.ReadAsync(cancellationToken);
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

    public static async Task<uint> GetNextPartyId(this IUnitOfWork uow, CancellationToken cancellationToken = default)
    {
        var connection = uow.GetRequiredService<NpgsqlConnection>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            /*strpsql*/"""
            SELECT COALESCE(MAX(id), 0) FROM register.party
            """;

        return Convert.ToUInt32(await cmd.ExecuteScalarAsync(cancellationToken)) + 1;
    }

    public static async Task<IEnumerable<int>> GetNewUserIds(this IUnitOfWork uow, int count = 1, CancellationToken cancellationToken = default)
    {
        var connection = uow.GetRequiredService<NpgsqlConnection>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            /*strpsql*/"""
            SELECT COALESCE(MAX(user_id), 0) FROM register.user
            """;

        var max = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
        return Enumerable.Range(max + 1, count);
    }

    public static async Task<OrganizationRecord> CreateOrg(
        this IUnitOfWork uow,
        FieldValue<Guid> uuid = default,
        FieldValue<uint> id = default,
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
        FieldValue<MailingAddress> businessAddress = default,
        CancellationToken cancellationToken = default)
    {
        var connection = uow.GetRequiredService<NpgsqlConnection>();

        if (!id.HasValue)
        {
            id = await uow.GetNextPartyId(cancellationToken);
        }

        if (!identifier.HasValue)
        {
            identifier = await uow.GetNewOrgNumber(cancellationToken);
        }

        var result = await uow.GetRequiredService<IPartyPersistence>().UpsertParty(
            new OrganizationRecord
            {
                PartyUuid = uuid.HasValue ? uuid.Value : Guid.NewGuid(),
                PartyId = id,
                DisplayName = name.HasValue ? name.Value : "Test",
                PersonIdentifier = null,
                OrganizationIdentifier = identifier,
                CreatedAt = createdAt.HasValue ? createdAt.Value : uow.GetRequiredService<TimeProvider>().GetUtcNow(),
                ModifiedAt = modifiedAt.HasValue ? modifiedAt.Value : uow.GetRequiredService<TimeProvider>().GetUtcNow(),
                IsDeleted = isDeleted.HasValue ? isDeleted.Value : false,
                User = FieldValue.Unset,
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
            },
            cancellationToken);

        Assert.True(result.IsSuccess);
        return (OrganizationRecord)result.Value;
    }

    public static async Task<ImmutableArray<OrganizationRecord>> CreateOrgs(
        this IUnitOfWork uow,
        int count,
        CancellationToken cancellationToken = default)
    {
        var builder = ImmutableArray.CreateBuilder<OrganizationRecord>(count);
        for (var i = 0; i < count; i++)
        {
            builder.Add(await uow.CreateOrg(cancellationToken: cancellationToken));
        }

        return builder.MoveToImmutable();
    }

    public static async Task<PersonRecord> CreatePerson(
        this IUnitOfWork uow,
        FieldValue<Guid> uuid = default,
        FieldValue<uint> id = default,
        FieldValue<PersonIdentifier> identifier = default,
        FieldValue<DateTimeOffset> createdAt = default,
        FieldValue<DateTimeOffset> modifiedAt = default,
        FieldValue<PersonName> name = default,
        FieldValue<StreetAddress> address = default,
        FieldValue<MailingAddress> mailingAddress = default,
        FieldValue<DateOnly> dateOfBirth = default,
        FieldValue<DateOnly> dateOfDeath = default,
        FieldValue<PartyUserRecord> user = default,
        CancellationToken cancellationToken = default)
    {
        var connection = uow.GetRequiredService<NpgsqlConnection>();

        if (!id.HasValue)
        {
            id = await uow.GetNextPartyId(cancellationToken);
        }

        if (!dateOfBirth.IsSet)
        {
            // 10% chance of having no date of birth
            dateOfBirth = Random.Shared.NextDouble() > 0.1 
                ? uow.GetRandomBirthDate()
                : FieldValue.Null;
        }

        if (!identifier.HasValue)
        {
            var dateOfBirthValue = dateOfBirth.HasValue ? dateOfBirth.Value : uow.GetRandomBirthDate();
            identifier = await uow.GetNewPersonIdentifier(dateOfBirthValue, isDNumber: false, cancellationToken);
        }

        if (!address.HasValue)
        {
            address = new StreetAddress
            {
                MunicipalNumber = "0001",
                MunicipalName = "Test",
                StreetName = "Testveien",
                HouseNumber = "1",
                HouseLetter = null,
                PostalCode = "0001",
                City = "Testby",
            };
        }

        if (!mailingAddress.HasValue)
        {
            mailingAddress = new MailingAddress
            {
                Address = $"{address.Value!.StreetName} {address.Value.HouseNumber}",
                PostalCode = address.Value.PostalCode,
                City = address.Value.City,
            };
        }

        if (!name.HasValue)
        {
            name = PersonName.Create("Test", "Mid", "Testson");
        }

        if (dateOfDeath.IsUnset)
        {
            dateOfDeath = FieldValue.Null;
        }

        if (user.IsUnset)
        {
            var userIdsEnumerable = await uow.GetNewUserIds(3, cancellationToken);
            user = new PartyUserRecord
            {
                UserIds = userIdsEnumerable.Select(static id => (uint)id).OrderByDescending(static id => id).ToImmutableValueArray(),
            };
        }

        // TODO: Generate user and historical user data
        var result = await uow.GetRequiredService<IPartyPersistence>().UpsertParty(
            new PersonRecord
            {
                PartyUuid = uuid.HasValue ? uuid.Value : Guid.NewGuid(),
                PartyId = id,
                DisplayName = name.Value!.DisplayName,
                PersonIdentifier = identifier,
                OrganizationIdentifier = null,
                CreatedAt = createdAt.HasValue ? createdAt.Value : uow.GetRequiredService<TimeProvider>().GetUtcNow(),
                ModifiedAt = modifiedAt.HasValue ? modifiedAt.Value : uow.GetRequiredService<TimeProvider>().GetUtcNow(),
                IsDeleted = dateOfDeath.HasValue,
                User = user,
                VersionId = FieldValue.Unset,
                FirstName = name.Value.FirstName,
                MiddleName = name.Value.MiddleName,
                LastName = name.Value.LastName,
                ShortName = name.Value.ShortName,
                Address = address,
                MailingAddress = mailingAddress,
                DateOfBirth = dateOfBirth,
                DateOfDeath = dateOfDeath,
            },
            cancellationToken);

        Assert.True(result.IsSuccess);
        return (PersonRecord)result.Value;
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

    public static async Task<ImmutableDictionary<ExternalRoleSource, ImmutableArray<ExternalRoleDefinition>>> CreateFakeRoleDefinitions(
        this IUnitOfWork uow,
        CancellationToken cancellationToken = default)
    {
        var builder = ImmutableDictionary.CreateBuilder<ExternalRoleSource, ImmutableArray<ExternalRoleDefinition>>();
        builder.Add(ExternalRoleSource.CentralCoordinatingRegister, await uow.CreateFakeRoleDefinitions(ExternalRoleSource.CentralCoordinatingRegister, cancellationToken));
        builder.Add(ExternalRoleSource.NationalPopulationRegister, await uow.CreateFakeRoleDefinitions(ExternalRoleSource.NationalPopulationRegister, cancellationToken));

        return builder.ToImmutable();
    }

    public static async Task<ImmutableArray<ExternalRoleDefinition>> CreateFakeRoleDefinitions(
        this IUnitOfWork uow,
        ExternalRoleSource source,
        CancellationToken cancellationToken)
    {
        const int COUNT = 40;

        var builder = ImmutableArray.CreateBuilder<ExternalRoleDefinition>(COUNT);
        for (var i = 0; i < 40; i++)
        {
            builder.Add(await uow.CreateFakeRoleDefinition(source, $"fake-{i:D2}", cancellationToken));
        }

        return builder.MoveToImmutable();
    }

    public static async Task<ExternalRoleDefinition> CreateFakeRoleDefinition(
        this IUnitOfWork uow,
        ExternalRoleSource source,
        string identifier,
        CancellationToken cancellationToken = default)
    {
        const string QUERY =
            /*strpsql*/"""
            INSERT INTO register.external_role_definition (source, identifier, name, description)
            VALUES (@source, @identifier, @name, @name)
            RETURNING *
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

        await cmd.PrepareAsync(cancellationToken);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var def = await PostgreSqlExternalRoleDefinitionPersistence.Cache.ReadExternalRoleDefinitions(reader, cancellationToken).FirstOrDefaultAsync(cancellationToken);

        if (def is null)
        {
            throw new InvalidOperationException("Failed to create fake role definition");
        }

        return def;
    }
}
