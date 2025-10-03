#nullable enable

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Altinn.Authorization.ModelUtils;
using Altinn.Register.Contracts;
using Altinn.Register.Core.Parties.Records;
using CommunityToolkit.Diagnostics;

namespace Altinn.Register.Mapping;

/// <summary>
/// Mappers for converting from <see cref="PartyRecord"/>
/// to <see cref="Party"/>.
/// </summary>
internal static partial class PartyMapper
{
    [return: NotNullIfNotNull(nameof(source))]
    private static Person? MapPerson(PersonRecord? source)
    {
        if (source is null)
        {
            return null;
        }

        if (!source.PartyUuid.HasValue)
        {
            ThrowHelper.ThrowInvalidOperationException("Platform.Models.Register.Party requires a PartyUuid to be set.");
        }

        if (!source.VersionId.HasValue)
        {
            ThrowHelper.ThrowInvalidOperationException("Platform.Models.Register.Party requires a VersionId to be set.");
        }

        if (!source.PersonIdentifier.HasValue)
        {
            ThrowHelper.ThrowInvalidOperationException("Platform.Models.Register.Person requires a PersonIdentifier to be set.");
        }

        Debug.Assert(source.PartyType.IsSet && source.PartyType.Value == PartyRecordType.Person);

        return new Person
        {
            Uuid = source.PartyUuid.Value,
            VersionId = source.VersionId.Value,
            PartyId = source.PartyId,
            DisplayName = source.DisplayName,
            CreatedAt = source.CreatedAt,
            ModifiedAt = source.ModifiedAt,
            IsDeleted = source.IsDeleted,
            User = source.User.Select(static u => u.ToPlatformModel()),

            PersonIdentifier = source.PersonIdentifier.Value,
            FirstName = source.FirstName,
            MiddleName = source.MiddleName,
            LastName = source.LastName,
            ShortName = source.ShortName,
            Address = source.Address.Select(static a => a.ToPlatformModel()),
            MailingAddress = source.MailingAddress.Select(static a => a.ToPlatformModel()),
            DateOfBirth = source.DateOfBirth,
            DateOfDeath = source.DateOfDeath,
        };
    }

    [return: NotNullIfNotNull(nameof(source))]
    private static Organization? MapOrganization(OrganizationRecord? source)
    {
        if (source is null)
        {
            return null;
        }

        if (!source.PartyUuid.HasValue)
        {
            ThrowHelper.ThrowInvalidOperationException("Platform.Models.Register.Party requires a PartyUuid to be set.");
        }

        if (!source.VersionId.HasValue)
        {
            ThrowHelper.ThrowInvalidOperationException("Platform.Models.Register.Party requires a VersionId to be set.");
        }

        if (!source.OrganizationIdentifier.HasValue)
        {
            ThrowHelper.ThrowInvalidOperationException("Platform.Models.Register.Organization requires an OrganizationIdentifier to be set.");
        }

        Debug.Assert(source.PartyType.IsSet && source.PartyType.Value == PartyRecordType.Organization);

        return new Organization
        {
            Uuid = source.PartyUuid.Value,
            VersionId = source.VersionId.Value,
            PartyId = source.PartyId,
            DisplayName = source.DisplayName,
            CreatedAt = source.CreatedAt,
            ModifiedAt = source.ModifiedAt,
            IsDeleted = source.IsDeleted,
            User = source.User.Select(static u => u.ToPlatformModel()),

            OrganizationIdentifier = source.OrganizationIdentifier.Value,
            UnitStatus = source.UnitStatus,
            UnitType = source.UnitType,
            TelephoneNumber = source.TelephoneNumber,
            MobileNumber = source.MobileNumber,
            FaxNumber = source.FaxNumber,
            EmailAddress = source.EmailAddress,
            InternetAddress = source.InternetAddress,
            MailingAddress = source.MailingAddress.Select(static a => a.ToPlatformModel()),
            BusinessAddress = source.BusinessAddress.Select(static a => a.ToPlatformModel()),
        };
    }

    [return: NotNullIfNotNull(nameof(source))]
    private static SelfIdentifiedUser? MapSelfIdentifiedUser(this SelfIdentifiedUserRecord? source)
    {
        if (source is null)
        {
            return null;
        }

        if (!source.PartyUuid.HasValue)
        {
            ThrowHelper.ThrowInvalidOperationException("Platform.Models.Register.Party requires a PartyUuid to be set.");
        }

        if (!source.VersionId.HasValue)
        {
            ThrowHelper.ThrowInvalidOperationException("Platform.Models.Register.Party requires a VersionId to be set.");
        }

        Debug.Assert(source.PartyType.IsSet && source.PartyType.Value == PartyRecordType.SelfIdentifiedUser);

        return new SelfIdentifiedUser
        {
            Uuid = source.PartyUuid.Value,
            VersionId = source.VersionId.Value,
            PartyId = source.PartyId,
            DisplayName = source.DisplayName,
            CreatedAt = source.CreatedAt,
            ModifiedAt = source.ModifiedAt,
            IsDeleted = source.IsDeleted,
            User = source.User.Select(static u => u.ToPlatformModel()),
        };
    }

    [return: NotNullIfNotNull(nameof(source))]
    private static EnterpriseUser? MapEnterpriseUser(this EnterpriseUserRecord? source)
    {
        if (source is null)
        {
            return null;
        }

        if (!source.PartyUuid.HasValue)
        {
            ThrowHelper.ThrowInvalidOperationException("Platform.Models.Register.Party requires a PartyUuid to be set.");
        }

        if (!source.VersionId.HasValue)
        {
            ThrowHelper.ThrowInvalidOperationException("Platform.Models.Register.Party requires a VersionId to be set.");
        }

        Debug.Assert(source.PartyType.IsSet && source.PartyType.Value == PartyRecordType.EnterpriseUser);

        return new EnterpriseUser
        {
            Uuid = source.PartyUuid.Value,
            VersionId = source.VersionId.Value,
            PartyId = source.PartyId,
            DisplayName = source.DisplayName,
            CreatedAt = source.CreatedAt,
            ModifiedAt = source.ModifiedAt,
            IsDeleted = source.IsDeleted,
            User = source.User.Select(static u => u.ToPlatformModel()),
            Owner = source.OwnerUuid.Select(static uuid => new PartyRef { Uuid = uuid }),
        };
    }

    [return: NotNullIfNotNull(nameof(source))]
    private static SystemUser? MapSystemUser(this SystemUserRecord? source)
    {
        if (source is null)
        {
            return null;
        }

        if (!source.PartyUuid.HasValue)
        {
            ThrowHelper.ThrowInvalidOperationException("Platform.Models.Register.Party requires a PartyUuid to be set.");
        }

        if (!source.VersionId.HasValue)
        {
            ThrowHelper.ThrowInvalidOperationException("Platform.Models.Register.Party requires a VersionId to be set.");
        }

        if (source.SystemUserType.IsNull)
        {
            ThrowHelper.ThrowInvalidOperationException($"Platform.Models.Register.SystemUser requires {nameof(SystemUser.SystemUserType)} to not be null.");
        }

        Debug.Assert(source.PartyType.IsSet && source.PartyType.Value == PartyRecordType.SystemUser);

        return new SystemUser
        {
            Uuid = source.PartyUuid.Value,
            VersionId = source.VersionId.Value,
            PartyId = source.PartyId,
            DisplayName = source.DisplayName,
            CreatedAt = source.CreatedAt,
            ModifiedAt = source.ModifiedAt,
            IsDeleted = source.IsDeleted,
            User = source.User.Select(static u => u.ToPlatformModel()),
            Owner = source.OwnerUuid.Select(static uuid => new PartyRef { Uuid = uuid }),
            SystemUserType = source.SystemUserType.Select(static t => NonExhaustiveEnum.Create(t.MapSystemUserType())),
        };
    }

    private static SystemUserType MapSystemUserType(this SystemUserRecordType source)
        => source switch
        {
            SystemUserRecordType.Standard => SystemUserType.FirstPartySystemUser,
            SystemUserRecordType.Agent => SystemUserType.ClientPartySystemUser,
            _ => ThrowHelper.ThrowArgumentOutOfRangeException<SystemUserType>(nameof(source), source, "Invalid system user type."),
        };
}
