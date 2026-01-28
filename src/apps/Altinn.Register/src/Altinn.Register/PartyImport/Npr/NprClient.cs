#nullable enable

using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Altinn.Authorization.ModelUtils;
using Altinn.Register.Contracts;
using Altinn.Register.Core;

namespace Altinn.Register.PartyImport.Npr;

/// <summary>
/// National Population Register (NPR) client.
/// </summary>
internal partial class NprClient
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="NprClient"/> class.
    /// </summary>
    public NprClient(
        HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Retrieves the list of guardianships or future mandates associated with the specified person.
    /// </summary>
    /// <param name="personIdentifier">The identifier of the person whose guardianships are to be retrieved.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>A read-only list of guardianships for the specified person. The list is empty if no guardianships are found.</returns>
    public async Task<IReadOnlyList<Guardianship>> GetGuardianshipsForPerson(
        PersonIdentifier personIdentifier,
        CancellationToken cancellationToken = default) 
    {
        using var activity = RegisterTelemetry.StartActivity("get guardianships");

        using var response = await _httpClient.GetAsync(
            $"folkeregisteret/offentlig-med-hjemmel/api/v1/personer/{personIdentifier}?part=vergemaalEllerFremtidsfullmakt",
            cancellationToken);

        // TODO: Handle errors better
        activity?.AddTag("response.status_code", (int)response.StatusCode);
        if (!response.IsSuccessStatusCode)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, "Failed to get guardianships");
            response.EnsureSuccessStatusCode(); // throws
        }

        var responseDto = await response.Content.ReadFromJsonAsync<GuardianshipResponse>(SourceGenerationContext.Default.Options, cancellationToken);
        if (responseDto is null or { Guardianships: null })
        {
            // TODO: handle errors better
            throw new InvalidOperationException("guardianships returned null");
        }

        return responseDto.Guardianships;
    }

    [JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
    [JsonSerializable(typeof(GuardianshipResponse))]
    [JsonSerializable(typeof(GuardianshipJsonConverter.VergemaalEllerFremtidsfullmakt))]
    private partial class SourceGenerationContext
        : JsonSerializerContext 
    {
    }

    private sealed class GuardianshipResponse 
    {
        [JsonConverter(typeof(GuardianshipJsonConverter))]
        [JsonPropertyName("vergemaalEllerFremtidsfullmakt")]
        public required IReadOnlyList<Guardianship> Guardianships { get; init; }
    }

    /// <summary>
    /// Represents an immutable record for guardianship information.
    /// </summary>
    internal sealed record Guardianship
    {
        /// <summary>
        /// Gets the identifier of the legal guardian associated with this entity.
        /// </summary>
        public required PersonIdentifier Guardian { get; init; }

        /// <summary>
        /// Gets the role-identifiers for the guardianship.
        /// </summary>
        public required IReadOnlyList<string> Roles { get; init; }
    }

    private sealed class GuardianshipJsonConverter
        : JsonConverter<IReadOnlyList<Guardianship>>
    {
        public override void Write(Utf8JsonWriter writer, IReadOnlyList<Guardianship> value, JsonSerializerOptions options)
        {
            throw new NotSupportedException("Not writable");
        }

        public override IReadOnlyList<Guardianship> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException("Expected start of array");
            }

            List<Guardianship> guardianships = new();
            
            while (true)
            {
                if (!reader.Read())
                {
                    throw new JsonException("Unexpected end of JSON");
                }

                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }

                if (TryReadGuardianship(ref reader, options, out var guardianship))
                {
                    guardianships.Add(guardianship);
                }
            }

            return guardianships;
        }

        private bool TryReadGuardianship(ref Utf8JsonReader reader, JsonSerializerOptions options, [NotNullWhen(true)] out Guardianship? guardianship) 
        {
            var dto = JsonSerializer.Deserialize<VergemaalEllerFremtidsfullmakt>(ref reader, options);
            if (dto is null or { IsActive: false } or { Verge.Roles: not { Count: > 0 } })
            {
                guardianship = null;
                return false;
            }

            guardianship = new Guardianship()
            {
                Guardian = dto.Verge.PersonIdentifier,
                Roles = dto.Verge.Roles,
            };
            return true;
        }

        internal sealed class VergemaalEllerFremtidsfullmakt
        {
            [JsonPropertyName("erGjeldende")]
            public required bool IsActive { get; init; }

            [JsonPropertyName("verge")]
            public required VergemaalEllerFremtidsfullmaktVerge Verge { get; init; }
        }

        internal sealed class VergemaalEllerFremtidsfullmaktVerge
        {
            [JsonPropertyName("foedselsEllerDNummer")]
            public required PersonIdentifier PersonIdentifier { get; init; }

            [JsonPropertyName("tjenesteomraade")]
            [JsonConverter(typeof(TjenesteomraadeConverter))]
            public required IReadOnlyList<string> Roles { get; init; }
        }

        internal sealed class TjenesteomraadeConverter
            : JsonConverter<IReadOnlyList<string>>
        {
            public override void Write(Utf8JsonWriter writer, IReadOnlyList<string> value, JsonSerializerOptions options)
            {
                throw new NotSupportedException("Not writable");
            }

            public override IReadOnlyList<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartArray)
                {
                    throw new JsonException("Expected start of array");
                }

                List<string>? identifiers = null;
                while (true)
                {
                    if (!reader.Read())
                    {
                        throw new JsonException("Unexpected end of JSON");
                    }

                    if (reader.TokenType == JsonTokenType.EndArray)
                    {
                        break;
                    }

                    var roleIdentifier = ReadTjenesteomraade(ref reader, options);
                    if (roleIdentifier is not null)
                    {
                        identifiers ??= [];
                        identifiers.Add(roleIdentifier);
                    }
                }

                return ((IReadOnlyList<string>?)identifiers) ?? [];
            }

            private string? ReadTjenesteomraade(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                Utf8JsonReader start = reader;
                Utf8JsonReader vergeTjenestevirksomhetReader = default, vergeTjenesteoppgaveReader = default;
                bool vergeTjenestevirksomhetFound = false, vergeTjenesteoppgaveFound = false;

                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    throw new JsonException("Expected start of object");
                }

                while (true)
                {
                    if (!reader.Read())
                    {
                        throw new JsonException("Unexpected end of JSON");
                    }

                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        break;
                    }

                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        throw new JsonException("Expected property name");
                    }

                    if (reader.ValueTextEquals("vergeTjenestevirksomhet"))
                    {
                        if (!reader.Read())
                        {
                            throw new JsonException("Unexpected end of JSON");
                        }

                        if (reader.TokenType != JsonTokenType.String)
                        {
                            throw new JsonException("Expected 'vergeTjenestevirksomhet' to be a string");
                        }

                        vergeTjenestevirksomhetReader = reader;
                        vergeTjenestevirksomhetFound = true;
                        continue;
                    }

                    if (reader.ValueTextEquals("vergeTjenesteoppgave"))
                    {
                        if (!reader.Read())
                        {
                            throw new JsonException("Unexpected end of JSON");
                        }

                        if (reader.TokenType != JsonTokenType.String)
                        {
                            throw new JsonException("Expected 'vergeTjenesteoppgave' to be a string");
                        }

                        vergeTjenesteoppgaveReader = reader;
                        vergeTjenesteoppgaveFound = true;
                        continue;
                    }

                    if (!reader.Read())
                    {
                        throw new JsonException("Unexpected end of JSON");
                    }

                    if (!reader.TrySkip())
                    {
                        throw new JsonException("Failed to skip");
                    }
                }

                if (!vergeTjenestevirksomhetFound || !vergeTjenesteoppgaveFound)
                {
                    reader = start;
                    if (!reader.TrySkip())
                    {
                        throw new JsonException("Failed to skip");
                    }

                    return null;
                }

                return TryFindRole(in vergeTjenestevirksomhetReader, in vergeTjenesteoppgaveReader);
            }

            private string? TryFindRole(in Utf8JsonReader vergeTjenestevirksomhetReader, in Utf8JsonReader vergeTjenesteoppgaveReader)
            {
                byte[]? vergeTjenestevirksomhetRented = null, vergeTjenesteoppgaveRented = null;

                try
                {
                    ReadOnlySpan<byte> vergeTjenestevirksomhet = ReadUtf8(in vergeTjenestevirksomhetReader, ref vergeTjenestevirksomhetRented);
                    ReadOnlySpan<byte> vergeTjenesteoppgave = ReadUtf8(in vergeTjenesteoppgaveReader, ref vergeTjenesteoppgaveRented);

                    if (GuardianshipRoleMapper.TryFindRoleByNprValues(vergeTjenestevirksomhet, vergeTjenesteoppgave, out var role))
                    {
                        return role.Identifier;
                    }

                    // TODO: better exception type
                    var vergeTjenestevirksomhetString = vergeTjenestevirksomhetReader.GetString();
                    var vergeTjenesteoppgaveString = vergeTjenesteoppgaveReader.GetString();
                    throw new JsonException($"Failed to find role for virksomhet='{vergeTjenestevirksomhetString}', oppgave='{vergeTjenesteoppgaveString}'");
                }
                finally
                {
                    if (vergeTjenesteoppgaveRented is not null)
                    {
                        ArrayPool<byte>.Shared.Return(vergeTjenesteoppgaveRented);
                    }

                    if (vergeTjenestevirksomhetRented is not null)
                    {
                        ArrayPool<byte>.Shared.Return(vergeTjenestevirksomhetRented);
                    }
                }

                static ReadOnlySpan<byte> ReadUtf8(in Utf8JsonReader reader, ref byte[]? rented)
                {
                    if (!reader.HasValueSequence && !reader.ValueIsEscaped)
                    {
                        return reader.ValueSpan;
                    }

                    var length = reader.HasValueSequence ? (int)reader.ValueSequence.Length : reader.ValueSpan.Length;
                    rented = ArrayPool<byte>.Shared.Rent(length);

                    length = reader.CopyString(rented);
                    return rented.AsSpan(0, length);
                }
            }
        }
    }
}
