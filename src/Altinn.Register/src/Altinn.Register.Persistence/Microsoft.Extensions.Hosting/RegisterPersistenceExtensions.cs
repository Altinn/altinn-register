using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Altinn.Authorization.ServiceDefaults.Npgsql;
using Altinn.Authorization.ServiceDefaults.Npgsql.TestSeed;
using Altinn.Authorization.ServiceDefaults.Npgsql.Yuniql;
using Altinn.Register.Core.Parties;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Npgsql;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class RegisterPersistenceExtensions
{
    /// <summary>
    /// Adds persistence for the register application.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/>.</param>
    /// <returns><paramref name="builder"/>.</returns>
    public static IHostApplicationBuilder AddRegisterPersistence(this IHostApplicationBuilder builder)
    {
        builder.AddPartyPersistence();

        return builder;
    }

    /// <summary>
    /// Adds persistence for the party component.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/>.</param>
    /// <returns><paramref name="builder"/>.</returns>
    public static IHostApplicationBuilder AddPartyPersistence(this IHostApplicationBuilder builder)
    {
        AddDatabase(builder);

        ////builder.Services.AddTransient<IPartyPersistence, PostgreSqlPartyPersistence>();

        return builder;
    }

    private static void AddDatabase(IHostApplicationBuilder builder)
    {
        if (builder.Services.Contains(Marker.ServiceDescriptor))
        {
            // already added
            return;
        }

        builder.Services.Add(Marker.ServiceDescriptor);

        var descriptor = builder.GetAltinnServiceDescriptor();
        var yuniqlSchema = builder.Configuration.GetValue($"Altinn:Npgsql:{descriptor.Name}:Yuniql:MigrationsTable:Schema", defaultValue: "yuniql");
        var migrationsFs = new ManifestEmbeddedFileProvider(typeof(RegisterPersistenceExtensions).Assembly, "Migration");
        var seedDataFs = new ManifestEmbeddedFileProvider(typeof(RegisterPersistenceExtensions).Assembly, "TestData");
        builder.AddAltinnPostgresDataSource()
            .MapRegisterTypes()
            .SeedFromFileProvider(seedDataFs)
            .AddYuniqlMigrations(y =>
            {
                y.WorkspaceFileProvider = migrationsFs;
                y.MigrationsTable.Schema = yuniqlSchema;
            });
    }

    private static INpgsqlDatabaseBuilder MapRegisterTypes(this INpgsqlDatabaseBuilder builder)
    {
        builder.MapEnum<PartyType>("register.party_type", new EnumNameTranslator<PartyType>(static value => value switch
        {
            PartyType.Organization => "organization",
            PartyType.Person => "person",
            _ => ThrowHelper.ThrowArgumentOutOfRangeException<string>(),
        }));

        builder.MapEnum<PartySource>("register.party_source", new EnumNameTranslator<PartySource>(static value => value switch
        {
            PartySource.CentralCoordinatingRegister => "ccr",
            PartySource.NationalPopulationRegister => "npr",
            _ => ThrowHelper.ThrowArgumentOutOfRangeException<string>(),
        }));

        return builder;
    }

    private sealed class Marker
    {
        public static readonly ServiceDescriptor ServiceDescriptor = ServiceDescriptor.Singleton<Marker, Marker>();
    }

    private sealed class EnumNameTranslator<TEnum>
        : INpgsqlNameTranslator
        where TEnum : struct, Enum
    {
        private readonly ImmutableArray<(string MemberName, string PgName)> _values;

        public EnumNameTranslator(Func<TEnum, string> factory)
        {
            var enumValues = Enum.GetValues<TEnum>();
            var builder = ImmutableArray.CreateBuilder<(string MemberName, string PgName)>(enumValues.Length);

            foreach (var enumValue in enumValues)
            {
                var memberName = enumValue.ToString();
                var pgName = factory(enumValue);
                builder.Add((memberName, pgName));
            }

            builder.Sort(static (l, r) => string.CompareOrdinal(l.MemberName, r.MemberName));
            _values = builder.ToImmutable();
        }

        public string TranslateMemberName(string clrName)
        {
            var index = _values.AsSpan().BinarySearch(new MemberNameMatcher(clrName));

            if (index < 0)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(clrName));
            }

            return _values[index].PgName;
        }

        public string TranslateTypeName(string clrName)
            => ThrowHelper.ThrowNotSupportedException<string>();
    }

    private readonly struct MemberNameMatcher(string memberName)
        : IComparable<(string MemberName, string PgName)>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo((string MemberName, string PgName) other)
            => string.CompareOrdinal(memberName, other.MemberName);
    }
}
