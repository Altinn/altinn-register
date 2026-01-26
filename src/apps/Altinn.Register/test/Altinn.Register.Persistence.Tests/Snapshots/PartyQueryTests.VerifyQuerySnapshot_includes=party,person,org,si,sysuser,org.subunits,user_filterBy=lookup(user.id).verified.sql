-- include: party,person,org,si,sysuser,org.subunits,user
-- filter: lookup(user.id)

WITH uuids_by_user_id AS (
    SELECT "user"."uuid", party.version_id
    FROM register."user" AS "user"
    INNER JOIN register.party AS party USING (uuid)
    WHERE "user".user_id = ANY (@userIds)
),
top_level_uuids AS (
    SELECT "uuid", version_id FROM uuids_by_user_id
),
filtered_users AS (
    SELECT "user".*
    FROM register."user" AS "user"
    WHERE "user".is_active
       OR "user".user_id = ANY (@userIds)
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
    party.ext_urn p_ext_urn,
    party.display_name p_display_name,
    party.person_identifier p_person_identifier,
    party.organization_identifier p_organization_identifier,
    party.created p_created,
    party.updated p_updated,
    party.is_deleted p_is_deleted,
    party.deleted_at p_deleted_at,
    party.version_id p_version_id,
    party."owner" p_owner_uuid,
    person.first_name p_first_name,
    person.middle_name p_middle_name,
    person.last_name p_last_name,
    person.short_name p_short_name,
    person.date_of_birth p_date_of_birth,
    person.date_of_death p_date_of_death,
    person.address p_address,
    person.mailing_address p_person_mailing_address,
    org.unit_status p_unit_status,
    org.unit_type p_unit_type,
    org.telephone_number p_telephone_number,
    org.mobile_number p_mobile_number,
    org.fax_number p_fax_number,
    org.email_address p_email_address,
    org.internet_address p_internet_address,
    org.mailing_address p_org_mailing_address,
    org.business_address p_business_address,
    si_u."type" p_self_identified_user_type,
    si_u.email p_self_identified_user_email,
    sys_u."type" p_system_user_type,
    "user".is_active u_is_active,
    "user".user_id u_user_id,
    "user".username u_username
FROM uuids AS uuids
INNER JOIN register.party AS party USING (uuid)
LEFT JOIN register.person AS person USING (uuid)
LEFT JOIN register.organization AS org USING (uuid)
LEFT JOIN register.self_identified_user AS si_u USING (uuid)
LEFT JOIN register.system_user AS sys_u USING (uuid)
LEFT JOIN filtered_users AS "user" USING (uuid)
ORDER BY
    uuids.sort_first,
    uuids.sort_second NULLS FIRST,
    "user".is_active DESC,
    "user".user_id DESC
