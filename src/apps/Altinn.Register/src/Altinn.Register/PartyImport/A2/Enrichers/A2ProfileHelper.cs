using System.Diagnostics;
using Altinn.Authorization.ModelUtils;
using Altinn.Register.Contracts;
using Altinn.Register.Core.Parties.Records;
using Altinn.Register.Core.PartyImport.A2;
using Altinn.Urn;

namespace Altinn.Register.PartyImport.A2.Enrichers;

/// <summary>
/// Helpers for <see cref="A2ProfileRecord"/>s during party import.
/// </summary>
internal static class A2ProfileHelper
{
    /// <summary>
    /// Applies a profile to a party.
    /// </summary>
    /// <param name="party">The party.</param>
    /// <param name="profile">The profile.</param>
    /// <param name="now">The current time (used as <see cref="PartyRecord.DeletedAt"/> in some cases)</param>
    /// <returns>The merged party.</returns>
    public static PartyRecord ApplyProfile(PartyRecord party, A2ProfileRecord profile, DateTimeOffset now)
    {
        party = party with
        {
            User = new PartyUserRecord(profile.UserId, profile.UserName),
        };

        if (profile.ProfileType is A2UserProfileType.SelfIdentifiedUser)
        {
            Debug.Assert(party is SelfIdentifiedUserRecord);
            var si = (SelfIdentifiedUserRecord)party;

            if (!si.IsDeleted.Value && !profile.IsActive)
            {
                si = si with
                {
                    IsDeleted = true,
                    DeletedAt = profile.LastChangedAt ?? now,
                };
            }

            Debug.Assert(profile.ExternalAuthenticationReference != string.Empty);
            si = profile.ExternalAuthenticationReference switch
            {
                null => ApplyLegacyProfile(si, profile),
                string s when PartyExternalRefUrn.TryParse(s, out var urn) && urn.IsIDPortenEmail(out var email) => ApplyEpostProfile(si, email.Value),
                _ => ApplyEducationalProfile(si),
            };

            party = si;
        }

        return party;

        static SelfIdentifiedUserRecord ApplyLegacyProfile(SelfIdentifiedUserRecord si, A2ProfileRecord profile)
        {
            Debug.Assert(!string.IsNullOrEmpty(profile.UserName));
            return si with
            {
                SelfIdentifiedUserType = SelfIdentifiedUserType.Legacy,
                Email = FieldValue.Null,
                ExternalUrn = PartyExternalRefUrn.LegacySelfIdentifiedUsername.Create(UrnEncoded.Create(profile.UserName.ToLowerInvariant())),
            };
        }

        static SelfIdentifiedUserRecord ApplyEducationalProfile(SelfIdentifiedUserRecord si)
        {
            return si with
            {
                SelfIdentifiedUserType = SelfIdentifiedUserType.Educational,
                Email = FieldValue.Null,
            };
        }

        static SelfIdentifiedUserRecord ApplyEpostProfile(SelfIdentifiedUserRecord si, string email)
        {
            email = email.ToLowerInvariant();

            return si with
            {
                SelfIdentifiedUserType = SelfIdentifiedUserType.IdPortenEmail,
                Email = email,
                ExternalUrn = PartyExternalRefUrn.IDPortenEmail.Create(UrnEncoded.Create(email)),
                DisplayName = email,
            };
        }
    }
}
