﻿using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.Utils;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Register.Persistence;

/// <content>
/// Contains the party role query builder.
/// </content>
internal partial class PostgreSqlPartyPersistence
{
    [SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1516:Elements should be separated by blank line", Justification = "This class is long enough already")]
    private sealed class PartyRoleQuery
    {
        private static ImmutableDictionary<(PartyRoleFieldIncludes Includes, PartyRoleFilter FilterBy), PartyRoleQuery> _queries
            = ImmutableDictionary<(PartyRoleFieldIncludes Includes, PartyRoleFilter FilterBy), PartyRoleQuery>.Empty;

        public static PartyRoleQuery Get(PartyRoleFieldIncludes includes, PartyRoleFilter filterBy)
        {
            return ImmutableInterlocked.GetOrAdd(ref _queries, (Includes: includes, FilterBy: filterBy), static (key) => Builder.Create(key.Includes, key.FilterBy));
        }

        private PartyRoleQuery(
            string commandText,
            PartyRoleFields fields,
            FilterParameter paramFromParty,
            FilterParameter paramToParty)
        {
            CommandText = commandText;
            _fields = fields;
            _paramFromParty = paramFromParty;
            _paramToParty = paramToParty;
        }

        private readonly PartyRoleFields _fields;
        private readonly FilterParameter _paramFromParty;
        private readonly FilterParameter _paramToParty;

        public string CommandText { get; }

        public NpgsqlParameter<Guid> AddFromPartyParameter(NpgsqlCommand cmd, Guid value)
            => AddParameter(cmd, in _paramFromParty, value);

        public NpgsqlParameter<Guid> AddToPartyParameter(NpgsqlCommand cmd, Guid value)
            => AddParameter(cmd, in _paramToParty, value);

        private NpgsqlParameter<T> AddParameter<T>(NpgsqlCommand cmd, in FilterParameter config, T value)
        {
            Debug.Assert(config.HasValue, "Parameter must be configured");
            Debug.Assert(config.Type == typeof(T), "Parameter type mismatch");

            var param = cmd.Parameters.Add<T>(config.Name, config.DbType);
            param.TypedValue = value;

            return param;
        }

        public PartyRoleRecord ReadRole(NpgsqlDataReader reader)
            => ReadRole(reader, in _fields);

        private PartyRoleRecord ReadRole(NpgsqlDataReader reader, in PartyRoleFields fields)
        {
            return new PartyRoleRecord
            {
                Source = reader.GetConditionalFieldValue<PartySource>(fields.RoleSource),
                Identifier = reader.GetConditionalFieldValue<string>(fields.RoleIdentifier),
                FromParty = reader.GetConditionalFieldValue<Guid>(fields.RoleFromParty),
                ToParty = reader.GetConditionalFieldValue<Guid>(fields.RoleToParty),
                Name = reader.GetConditionalConvertibleFieldValue<Dictionary<string, string>, TranslatedText>(fields.RoleDefinitionName),
                Description = reader.GetConditionalConvertibleFieldValue<Dictionary<string, string>, TranslatedText>(fields.RoleDefinitionDescription),
            };
        }

        private sealed class Builder
        {
            public static PartyRoleQuery Create(PartyRoleFieldIncludes includes, PartyRoleFilter filterBy)
            {
                Builder builder = new();
                builder.Populate(includes, filterBy);

                PartyRoleFields fields = new(
                    roleSource: builder._roleSource,
                    roleIdentifier: builder._roleIdentifier,
                    roleFromParty: builder._roleFromParty,
                    roleToParty: builder._roleToParty,
                    roleDefinitionName: builder._roleDefinitionName,
                    roleDefinitionDescription: builder._roleDefinitionDescription);

                var commandText = builder._builder.ToString();
                return new(
                    commandText,
                    fields,
                    paramFromParty: builder._paramFromParty,
                    paramToParty: builder._paramToParty);
            }

            private readonly StringBuilder _builder = new();

            // parameters
            private FilterParameter _paramFromParty;
            private FilterParameter _paramToParty;

            // fields
            private sbyte _fieldIndex = 0;

            // register.external_role
            private sbyte _roleSource = -1;
            private sbyte _roleIdentifier = -1;
            private sbyte _roleFromParty = -1;
            private sbyte _roleToParty = -1;

            // register.external_role_definition
            private sbyte _roleDefinitionName = -1;
            private sbyte _roleDefinitionDescription = -1;

            public void Populate(PartyRoleFieldIncludes includes, PartyRoleFilter filterBy)
            {
                _builder.Append(/*strpsql*/"SELECT");

                _roleSource = AddField("r.source", "role_source", includes.HasFlag(PartyRoleFieldIncludes.RoleSource));
                _roleIdentifier = AddField("r.identifier", "role_identifier", includes.HasFlag(PartyRoleFieldIncludes.RoleIdentifier));
                _roleFromParty = AddField("r.from_party", "role_from_party", includes.HasFlag(PartyRoleFieldIncludes.RoleFromParty));
                _roleToParty = AddField("r.to_party", "role_to_party", includes.HasFlag(PartyRoleFieldIncludes.RoleToParty));

                _roleDefinitionName = AddField("d.name", "role_definition_name", includes.HasFlag(PartyRoleFieldIncludes.RoleDefinitionName));
                _roleDefinitionDescription = AddField("d.description", "role_definition_description", includes.HasFlag(PartyRoleFieldIncludes.RoleDefinitionDescription));

                _builder.AppendLine().Append(/*strpsql*/"FROM register.external_role r");

                if (includes.HasAnyFlags(PartyRoleFieldIncludes.RoleDefinition))
                {
                    _builder.AppendLine().Append(/*strpsql*/"FULL JOIN register.external_role_definition d USING (source, identifier)");
                }

                var first = true;
                switch (filterBy)
                {
                    case PartyRoleFilter.FromParty:
                        _paramFromParty = AddFilter(typeof(Guid), "fromParty", "r.from_party =", NpgsqlDbType.Uuid, ref first);
                        break;

                    case PartyRoleFilter.ToParty:
                        _paramToParty = AddFilter(typeof(Guid), "toParty", "r.to_party =", NpgsqlDbType.Uuid, ref first);
                        break;
                }
            }

            private sbyte AddField(string sourceSql, string fieldAlias, bool include)
            {
                if (!include)
                {
                    return -1;
                }

                if (_fieldIndex > 0)
                {
                    _builder.Append(',');
                }

                _builder.AppendLine();
                _builder.Append("    ").Append(sourceSql).Append(' ').Append(fieldAlias);

                return _fieldIndex++;
            }

            private FilterParameter AddFilter(Type type, string name, string sourceSql, NpgsqlDbType dbType, ref bool first)
            {
                if (first)
                {
                    _builder.AppendLine().Append(/*strpsql*/"WHERE ");
                    first = false;
                }
                else
                {
                    _builder.AppendLine().Append(/*strpsql*/"    OR ");
                }

                _builder.Append(sourceSql);

                if (dbType.HasFlag(NpgsqlDbType.Array))
                {
                    _builder.Append('(');
                }
                else
                {
                    _builder.Append(' ');
                }

                _builder.Append('@').Append(name);

                if (dbType.HasFlag(NpgsqlDbType.Array))
                {
                    _builder.Append(')');
                }

                return new(type, name, dbType);
            }
        }

        private readonly struct FilterParameter(
            Type type,
            string name,
            NpgsqlDbType dbType)
        {
            [MemberNotNullWhen(true, nameof(Type))]
            [MemberNotNullWhen(true, nameof(Name))]
            public bool HasValue => Type is not null;

            public Type? Type => type;

            public string? Name => name;

            public NpgsqlDbType DbType => dbType;
        }

        [SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1515:Single-line comment should be preceded by blank line", Justification = "This rule makes no sense here")]
        private readonly struct PartyRoleFields(
            // register.external_role
            sbyte roleSource,
            sbyte roleIdentifier,
            sbyte roleFromParty,
            sbyte roleToParty,
            
            // register.external_role_definition
            sbyte roleDefinitionName,
            sbyte roleDefinitionDescription)
        {
            // register.external_role
            public int RoleSource => roleSource;
            public int RoleIdentifier => roleIdentifier;
            public int RoleFromParty => roleFromParty;
            public int RoleToParty => roleToParty;

            // register.external_role_definition
            public int RoleDefinitionName => roleDefinitionName;
            public int RoleDefinitionDescription => roleDefinitionDescription;
        }
    }
}