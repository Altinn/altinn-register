#nullable enable

using System.Collections.Immutable;
using Altinn.Common.AccessToken;
using Altinn.Common.PEP.Authorization;
using CommunityToolkit.Diagnostics;

namespace Altinn.Register.Authorization;

/// <summary>
/// This requirement was created to allow access if either Scope or AccessToken verification is successful.
/// It inherits from both <see cref="IAccessTokenRequirement"/> and <see cref="IScopeAccessRequirement"/> which
/// will trigger both <see cref="AccessTokenHandler"/> and <see cref="ScopeAccessHandler"/>. If any of them
/// indicate success, authorization will succeed.
/// </summary>
public class InternalScopeOrAccessTokenRequirement 
    : IAccessTokenRequirement
    , IScopeAccessRequirement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InternalScopeOrAccessTokenRequirement"/> class with the given scope.
    /// </summary>
    public InternalScopeOrAccessTokenRequirement(string scope)
    {
        ApprovedIssuers = [];
        Scope = [scope];
    }

    /// <inheritdoc/>
    public ImmutableArray<string> ApprovedIssuers { get; }

    /// <summary>
    /// Gets the set of scopes that fulfills this requirement.
    /// </summary>
    public string[] Scope { get; }

    /// <inheritdoc/>
    string[] IScopeAccessRequirement.Scope
    {
        get => Scope;
        set => ThrowHelper.ThrowInvalidOperationException("This property is read-only.");
    }
}
