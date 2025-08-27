-- include: display-name,identifiers
-- filter: stream-page

WITH maxval AS (
    SELECT register.tx_max_safeval('register.party_version_id_seq') maxval
),
top_level_uuids AS (
    SELECT party."uuid", party.version_id
    FROM register.party AS party
    CROSS JOIN maxval mv
    WHERE party.version_id > @streamFromExlusive
      AND party.version_id <= mv.maxval
    ORDER BY party.version_id ASC
    LIMIT @streamLimit
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