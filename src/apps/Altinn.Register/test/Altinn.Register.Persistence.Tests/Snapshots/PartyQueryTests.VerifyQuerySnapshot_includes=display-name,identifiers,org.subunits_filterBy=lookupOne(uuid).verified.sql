-- include: display-name,identifiers,org.subunits
-- filter: lookupOne(uuid)

WITH top_level_uuids AS (
    SELECT party."uuid", party.version_id
    FROM register.party AS party
    WHERE party."uuid" = @partyUuid
),
sub_units AS (
    SELECT
        parent."uuid" AS parent_uuid,
        parent.version_id AS parent_version_id,
        ra."from_party" AS child_uuid
    FROM top_level_uuids AS parent
    JOIN register.external_role_assignment ra
         ON ra.to_party = parent."uuid"
        AND ra.source = 'ccr'
        AND (ra.identifier = 'ikke-naeringsdrivende-hovedenhet' OR ra.identifier = 'hovedenhet')
),
uuids AS (
    SELECT
        "uuid" AS "uuid",
        NULL::uuid AS parent_uuid,
        version_id AS sort_first,
        NULL::uuid AS sort_second
    FROM top_level_uuids
    UNION
    SELECT 
        child_uuid AS "uuid",
        parent_uuid,
        parent_version_id AS sort_first,
        child_uuid AS sort_second
    FROM sub_units
)
SELECT
    uuids.parent_uuid p_parent_uuid,
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
