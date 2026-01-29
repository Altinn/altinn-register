#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Net;
using Altinn.Register.Contracts;
using Altinn.Register.PartyImport.Npr;
using Altinn.Register.Tests.Utils;
using Altinn.Register.TestUtils.Http;
using Microsoft.Extensions.FileProviders;
using Xunit.Abstractions;

namespace Altinn.Register.Tests.PartyImport.Npr;

public class NprClientTests
{
    private static readonly TestDataFileProvider GuardianshipsFileProvider = TestDataFileProvider.For("Npr/Guardianships");

    private FakeHttpMessageHandler _handler;
    private NprClient _sut;

    public NprClientTests()
    {
        _handler = new();
        _sut = new(_handler.CreateClient());
    }

    [Theory]
    [MemberData(nameof(Guardianships))]
    public async Task GetGuardianshipsForPerson_CallsNprApi_AndMapsCorrectly(string personIdentifier, ExpectedGuardianship[] guardianships)
    {
        ConfigureHandler(personIdentifier);

        var result = await _sut.GetGuardianshipsForPerson(PersonIdentifier.Parse(personIdentifier));
        result.Should().HaveCount(guardianships.Length);

        for (var i = 0; i < result.Count; i++)
        {
            var actual = result[i];
            var expected = guardianships[i];

            actual.Guardian.Should().Be(expected.Guardian);
            actual.Roles.Should().BeEquivalentTo(expected.Roles);
        }
    }

    private void ConfigureHandler(string personIdentifier)
    {
        _handler.Expect(HttpMethod.Get, "folkeregisteret/offentlig-med-hjemmel/api/v1/personer/{personIdentifier}")
            .WithRouteValue("personIdentifier", personIdentifier)
            .WithQuery("part", "vergemaalEllerFremtidsfullmakt")
            .Respond(() => LoadJson(GuardianshipsFileProvider, $"{personIdentifier}.json"));
    }

    private static HttpResponseMessage LoadJson(IFileProvider provider, string filePath)
    {
        var file = provider.GetFileInfo(filePath);
        if (!file.Exists)
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var content = new StreamContent(file.CreateReadStream());
        content.Headers.ContentType = new("application/json", "utf-8");

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content,
        };
    }

    public static TheoryData<string, ExpectedGuardianship[]> Guardianships()
    {
        var data = new TheoryData<string, ExpectedGuardianship[]>();
        data.Add("07876296653", [
            new("13926596971", [
                RoleFor("nav", "arbeid"),
                RoleFor("kartverket", "arvPrivatSkifteOgUskifte"),
                RoleFor("oevrige", "avslutningAvHusleiekontrakter"),
            ]),
            new("06876393326", [
                RoleFor("nav", "arbeid"),
                RoleFor("oevrige", "avslutningAvHusleiekontrakter"),
            ]),
        ]);
        data.Add("24886199623", [
            new("19906199303", [
                RoleFor("nav", "arbeid"),
                RoleFor("kartverket", "arvPrivatSkifteOgUskifte"),
                RoleFor("oevrige", "avslutningAvHusleiekontrakter"),
            ]),
        ]);
        data.Add("25876399957", [
            new("27816499491", [
                RoleFor("skatteetaten", "endrePostadresse"),
            ]),
        ]);
        data.Add("30926299758", [
            new("31886599660", [
                RoleFor("nav", "arbeid"),
                RoleFor("kartverket", "arvPrivatSkifteOgUskifte"),
                RoleFor("oevrige", "avslutningAvHusleiekontrakter"),
                RoleFor("kartverket", "avtalerOgRettigheter"),
                RoleFor("tingretten", "begjaereSkifteAvUskiftebo"),
                RoleFor("tingretten", "begjaereUskifte"),
                RoleFor("husbanken", "bostoette"),
                RoleFor("kommune", "byggOgEiendom"),
                RoleFor("oevrige", "disponereInntekterTilAaDekkeUtgifter"),
                RoleFor("skatteetaten", "endrePostadresse"),
                RoleFor("kartverket", "endringAvEiendom"),
                RoleFor("nav", "familie"),
                RoleFor("helfo", "fastlege"),
                RoleFor("inkassoselskap", "forhandleOgInngaaInkassoavtaler"),
                RoleFor("forsikringsselskap", "forvalteForsikringsavtaler"),
                RoleFor("namsmannen", "gjeldsordning"),
                RoleFor("statensInnkrevingssentral", "gjeldsordningOgBetalingsavtaler"),
                RoleFor("kommune", "helseOgOmsorg"),
                RoleFor("nav", "hjelpemidler"),
                RoleFor("oevrige", "inngaaelseAvHusleiekontrakter"),
                RoleFor("skatteetaten", "innkreving"),
                RoleFor("kartverket", "kjoepAvEiendom"),
                RoleFor("oevrige", "kjoepLeieAvVarerOgTjenester"),
                RoleFor("kredittvurderingsselskap", "kredittsperre"),
                RoleFor("kartverket", "laaneopptak"),
                RoleFor("skatteetaten", "meldeFlytting"),
                RoleFor("nav", "pensjon"),
                RoleFor("tingretten", "privatSkifteAvDoedsbo"),
                RoleFor("pasientreiser", "refusjonAvPasientreiser"),
                RoleFor("helfo", "refusjonForPrivatpersoner"),
                RoleFor("bank", "representasjonDagligbank"),
                RoleFor("kartverket", "salgAvFastEiendomBorettslagsandel"),
                RoleFor("oevrige", "salgAvLoesoereAvStoerreVerdi"),
                RoleFor("skatteetaten", "skatt"),
                RoleFor("kommune", "skattOgAvgift"),
            ]),
        ]);

        return data;

        static string RoleFor(string vergeTjenestevirksomhet, string vergeTjenesteoppgave)
        {
            if (!GuardianshipRoleMapper.TryFindRoleByNprValues(vergeTjenestevirksomhet, vergeTjenesteoppgave, out var role))
            {
                throw new InvalidOperationException($"Role not found: vergeTjenestevirksomhet = '{vergeTjenestevirksomhet}', vergeTjenesteoppgave = '{vergeTjenesteoppgave}'");
            }

            return role.Identifier;
        }
    }

    ////public sealed record ExpectedGuardianships
    ////    : IXunitSerializable
    ////{

    ////}

    public sealed record ExpectedGuardianship
        : IXunitSerializable
    {
        private PersonIdentifier _guardian = null!;
        private string[] _roles = null!;

        public ExpectedGuardianship() 
        {
        }

        [SetsRequiredMembers]
        public ExpectedGuardianship(
            string guardian,
            string[] roles)
        {
            Guardian = PersonIdentifier.Parse(guardian);
            Roles = roles;
        }

        public required PersonIdentifier Guardian
        {
            get => _guardian;
            init => _guardian = value;
        }

        public required string[] Roles
        {
            get => _roles;
            init => _roles = value;
        }

        public override string ToString()
            => $"{_guardian}: [{string.Join(',', _roles)}]";

        void IXunitSerializable.Deserialize(IXunitSerializationInfo info)
        {
            var guardian = info.GetValue<string>(nameof(Guardian));
            var roles = info.GetValue<string[]>(nameof(Roles));

            _guardian = PersonIdentifier.Parse(guardian);
            _roles = roles;
        }

        void IXunitSerializable.Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(Guardian), _guardian.ToString(), typeof(string));
            info.AddValue(nameof(Roles), _roles, typeof(string[]));
        }
    }
}
