using Altinn.Authorization.ProblemDetails;
using Altinn.Authorization.ProblemDetails.Validation;
using Altinn.Register.Core.Location;
using Altinn.Register.Core.Sire;
using Altinn.Register.Integrations.Sire.Organization;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Altinn.Register.Tests.UnitTests;

/// <summary>
/// Focused unit tests for <see cref="OrganizationDocumentValidator"/> covering the
/// <c>opphoerstidspunkt</c> (validTo) handling semantics: SIRE treats any non-null
/// validTo as "terminated, drop this entry", and a validTo sitting more than
/// <c>ValidToFutureGrace</c> in the future is flagged as bad data rather than silently
/// carried forward.
/// </summary>
/// <remarks>
/// The golden-file <see cref="SireOrganizationParsingTests"/> exercises the validator
/// end-to-end on real SIRE responses; these tests use synthetic in-memory documents to
/// pin down the exact validity branches around a deterministic <c>now</c>.
/// </remarks>
public class OrganizationDocumentValidatorTests
{
    private static readonly DateTimeOffset Now
        = new(2026, 5, 27, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan WithinGrace = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan BeyondGrace = TimeSpan.FromHours(1);

    private const string TestOrgNo = "090090003";
    private const string TestFnr = "25871999336";

    private static OrganizationDocumentValidator CreateValidator()
    {
        var timeProvider = new FakeTimeProvider();
        timeProvider.SetUtcNow(Now);
        return new OrganizationDocumentValidator(Mock.Of<ILocationLookup>(), timeProvider);
    }

    private static (SireOrganization? Validated, bool HasError) Run(OrganizationDocument document)
    {
        var validator = CreateValidator();
        ValidationProblemBuilder builder = default;
        builder.TryValidate(path: "/", document, validator, out SireOrganization? validated);
        var hasError = builder.TryBuild(out _);
        return (validated, hasError);
    }

    private static OrganizationDocument DocWithPostalAddress(DateTimeOffset? validTo)
        => new()
        {
            Identifier = TestOrgNo,
            CompanyName = "Test AS",
            OrganizationForm = "indreSelskap",
            PostalAddress = new PostalAddress
            {
                ValidTo = validTo,
                NorwegianAddress = new NorwegianAddress
                {
                    AddressLines = ["Testgata 1"],
                    PostalCode = "0001",
                    City = "OSLO",
                },
            },
            BusinessRelationships = [],
        };

    private static OrganizationDocument DocWithBusinessRelationship(DateTimeOffset? validTo)
        => new()
        {
            Identifier = TestOrgNo,
            CompanyName = "Test AS",
            OrganizationForm = "indreSelskap",
            PostalAddress = null,
            BusinessRelationships =
            [
                new BusinessRelationship
                {
                    RelationshipType = "styremedlem",
                    ValidTo = validTo,
                    RelatedIdentifier = new RelatedIdentifier
                    {
                        IdentifierType = "taxIdentificationNumber",
                        Value = TestFnr,
                    },
                },
            ],
        };

    /// <summary>
    /// Null <c>opphoerstidspunkt</c> means the address is still in force, so it should
    /// be carried through to <c>MailingAddress</c>.
    /// </summary>
    [Fact]
    public void PostalAddress_NullValidTo_IsKept()
    {
        var (validated, hasError) = Run(DocWithPostalAddress(validTo: null));

        Assert.False(hasError);
        Assert.NotNull(validated);
        Assert.NotNull(validated.MailingAddress);
        Assert.Equal("Testgata 1", validated.MailingAddress.Address);
    }

    /// <summary>
    /// A past <c>opphoerstidspunkt</c> is the canonical "terminated" case — the address
    /// is silently dropped and no error is added.
    /// </summary>
    [Fact]
    public void PostalAddress_PastValidTo_IsDropped()
    {
        var (validated, hasError) = Run(DocWithPostalAddress(validTo: Now - TimeSpan.FromDays(1)));

        Assert.False(hasError);
        Assert.NotNull(validated);
        Assert.Null(validated.MailingAddress);
    }

    /// <summary>
    /// A <c>validTo</c> a few minutes in the future stays within the clock-skew grace
    /// window — the address is dropped (treat as terminated) but no error is added.
    /// </summary>
    [Fact]
    public void PostalAddress_WithinGrace_IsDropped()
    {
        var (validated, hasError) = Run(DocWithPostalAddress(validTo: Now + WithinGrace));

        Assert.False(hasError);
        Assert.NotNull(validated);
        Assert.Null(validated.MailingAddress);
    }

    /// <summary>
    /// A <c>validTo</c> meaningfully in the future is treated as bad data: SIRE
    /// shouldn't be sending future-dated terminations and we don't know how to safely
    /// process them. The validator surfaces a validation error rather than silently
    /// choosing an interpretation.
    /// </summary>
    [Fact]
    public void PostalAddress_FarFuture_AddsValidationError()
    {
        var (validated, hasError) = Run(DocWithPostalAddress(validTo: Now + BeyondGrace));

        Assert.True(hasError);
        Assert.Null(validated);
    }

    /// <summary>
    /// Null <c>opphoerstidspunkt</c> on a relationship means it's still in force, so it
    /// should appear in <c>BusinessRelationships</c>.
    /// </summary>
    [Fact]
    public void BusinessRelationship_NullValidTo_IsKept()
    {
        var (validated, hasError) = Run(DocWithBusinessRelationship(validTo: null));

        Assert.False(hasError);
        Assert.NotNull(validated);
        Assert.Single(validated.BusinessRelationships);
    }

    /// <summary>
    /// A past <c>opphoerstidspunkt</c> on a relationship — silently dropped, no error.
    /// </summary>
    [Fact]
    public void BusinessRelationship_PastValidTo_IsDropped()
    {
        var (validated, hasError) = Run(DocWithBusinessRelationship(validTo: Now - TimeSpan.FromDays(1)));

        Assert.False(hasError);
        Assert.NotNull(validated);
        Assert.Empty(validated.BusinessRelationships);
    }

    /// <summary>
    /// Within the clock-skew grace window — dropped, no error.
    /// </summary>
    [Fact]
    public void BusinessRelationship_WithinGrace_IsDropped()
    {
        var (validated, hasError) = Run(DocWithBusinessRelationship(validTo: Now + WithinGrace));

        Assert.False(hasError);
        Assert.NotNull(validated);
        Assert.Empty(validated.BusinessRelationships);
    }

    /// <summary>
    /// Far-future <c>opphoerstidspunkt</c> on a relationship — validation error.
    /// </summary>
    [Fact]
    public void BusinessRelationship_FarFuture_AddsValidationError()
    {
        var (validated, hasError) = Run(DocWithBusinessRelationship(validTo: Now + BeyondGrace));

        Assert.True(hasError);
        Assert.Null(validated);
    }
}
