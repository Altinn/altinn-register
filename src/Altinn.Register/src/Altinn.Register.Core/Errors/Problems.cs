using System.Net;
using Altinn.Authorization.ProblemDetails;

namespace Altinn.Register.Core.Errors;

/// <summary>
/// Problem descriptors for Register.
/// </summary>
public static class Problems
{
    private static readonly ProblemDescriptorFactory _factory
        = ProblemDescriptorFactory.New("REG");

    /// <summary>Gets a <see cref="ProblemDescriptor"/>.</summary>
    public static ProblemDescriptor PartyConflict { get; }
        = _factory.Create(0, HttpStatusCode.Conflict, "Party could not be inserted due to one or more uniqueness constraint violation");

    /// <summary>Gets a <see cref="ProblemDescriptor"/>.</summary>
    public static ProblemDescriptor InvalidPartyUpdate { get; }
        = _factory.Create(1, HttpStatusCode.BadRequest, "Invalid party update");

    /// <summary>Gets a <see cref="ProblemDescriptor"/>.</summary>
    public static ProblemDescriptor PartyGone { get; }
        = _factory.Create(2, HttpStatusCode.Gone, "Party is gone");

    /// <summary>Gets a <see cref="ProblemDescriptor"/>.</summary>
    public static ProblemDescriptor PartyNotFound { get; }
        = _factory.Create(3, HttpStatusCode.NotFound, "Party not found");

    /// <summary>Gets a <see cref="ProblemDescriptor"/>.</summary>
    public static ProblemDescriptor PartyFetchFailed { get; }
        = _factory.Create(4, HttpStatusCode.InternalServerError, "Failed to fetch party");
}
