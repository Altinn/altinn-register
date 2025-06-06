﻿-- include: display-name,identifiers
-- filter: party-id,party-uuid,person-identifier,organization-identifier,user-id,multiple

WITH uuids_by_party_uuid AS (
    SELECT party."uuid", party.version_id
    FROM register.party AS party
    WHERE party."uuid" = ANY (@partyUuids)
),
uuids_by_party_id AS (
    SELECT party."uuid", party.version_id
    FROM register.party AS party
    WHERE party."id" = ANY (@partyIds)
),
uuids_by_person_identifier AS (
    SELECT party."uuid", party.version_id
    FROM register.party AS party
    WHERE party."person_identifier" = ANY (@personIdentifiers)
),
uuids_by_organization_identifier AS (
    SELECT party."uuid", party.version_id
    FROM register.party AS party
    WHERE party."organization_identifier" = ANY (@organizationIdentifiers)
),
uuids_by_user_id AS (
    SELECT "user"."uuid", party.version_id
    FROM register."user" AS "user"
    INNER JOIN register.party AS party USING (uuid)
    WHERE "user".user_id = ANY (@userIds)
),
top_level_uuids AS (
    SELECT "uuid", version_id FROM uuids_by_party_uuid
    UNION
    SELECT "uuid", version_id FROM uuids_by_party_id
    UNION
    SELECT "uuid", version_id FROM uuids_by_person_identifier
    UNION
    SELECT "uuid", version_id FROM uuids_by_organization_identifier
    UNION
    SELECT "uuid", version_id FROM uuids_by_user_id
),
uuids AS (
    SELECT
        "uuid" AS "uuid",
        NULL::uuid AS parent_uuid,
        version_id AS sort_first,
        NULL::uuid AS sort_second
    FROM top_level_uuids
)
SELECT
    party.uuid p_uuid,
    party.id p_id,
    party.party_type p_party_type,
    party.display_name p_display_name,
    party.person_identifier p_person_identifier,
    party.organization_identifier p_organization_identifier
FROM uuids AS uuids
INNER JOIN register.party AS party USING (uuid)
ORDER BY
    uuids.sort_first,
    uuids.sort_second NULLS FIRST