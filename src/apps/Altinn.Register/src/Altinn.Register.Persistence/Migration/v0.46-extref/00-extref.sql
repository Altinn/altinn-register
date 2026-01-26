-- CREATE TABLE register.party (
-- 	"uuid" uuid NOT NULL,
-- 	id int8 NULL,
-- 	"party_type" register."party_type" NOT NULL,
-- 	display_name text NOT NULL,
-- 	"person_identifier" register."person_identifier" NULL,
-- 	"organization_identifier" register."organization_identifier" NULL,
-- 	created timestamptz NOT NULL,
-- 	updated timestamptz NOT NULL,
-- 	is_deleted bool DEFAULT false NOT NULL,
-- 	version_id int8 DEFAULT register.tx_nextval('register.party_version_id_seq'::regclass) NOT NULL,
-- 	"owner" uuid NULL,
-- 	deleted_at timestamptz NULL
-- );

ALTER TABLE register.party
  ADD COLUMN ext_urn text;

UPDATE register.party
   SET ext_urn = CONCAT('urn:altinn:person:identifier-no:', "person_identifier"::text)
 WHERE "party_type" = 'person';

UPDATE register.party
   SET ext_urn = CONCAT('urn:altinn:organization:identifier-no:', "organization_identifier"::text)
 WHERE "party_type" = 'organization';

UPDATE register.party
   SET ext_urn = CONCAT('urn:altinn:systemuser:uuid:', "uuid"::text)
 WHERE "party_type" = 'system-user';

ALTER TABLE register.party
  ADD CONSTRAINT chk_party_ext_urn CHECK (
    CASE
      WHEN party_type = 'person' THEN ext_urn = CONCAT('urn:altinn:person:identifier-no:', "person_identifier"::text)
      WHEN party_type = 'organization' THEN ext_urn = CONCAT('urn:altinn:organization:identifier-no:', "organization_identifier"::text)
      WHEN party_type = 'system-user' THEN ext_urn = CONCAT('urn:altinn:systemuser:uuid:', "uuid"::text)
      WHEN party_type = 'enterprise-user' THEN ext_urn IS NULL
      WHEN party_type = 'self-identified-user' THEN TRUE -- Multiple formats, some NULL for self-identified users
    END
  );

CREATE UNIQUE INDEX uq_party_ext_urn ON register.party (ext_urn);
