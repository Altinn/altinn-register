UPDATE register.party
SET ext_urn = CASE
  WHEN party_type = 'person' THEN CONCAT('urn:altinn:person:identifier-no:', person_identifier::text)
  WHEN party_type = 'organization' THEN CONCAT('urn:altinn:organization:identifier-no:', organization_identifier::text)
  WHEN party_type = 'system-user' THEN CONCAT('urn:altinn:systemuser:uuid:', uuid::text)
END
WHERE party_type IN ('person', 'organization', 'system-user')
  AND ext_urn IS NULL;

ALTER TABLE register.party
  DROP CONSTRAINT chk_party_ext_urn,
  ADD CONSTRAINT chk_party_ext_urn CHECK (
    CASE
      WHEN party_type = 'person' THEN ext_urn IS NOT DISTINCT FROM CONCAT('urn:altinn:person:identifier-no:', person_identifier::text)
      WHEN party_type = 'organization' THEN ext_urn IS NOT DISTINCT FROM CONCAT('urn:altinn:organization:identifier-no:', organization_identifier::text)
      WHEN party_type = 'system-user' THEN ext_urn IS NOT DISTINCT FROM CONCAT('urn:altinn:systemuser:uuid:', uuid::text)
      WHEN party_type = 'enterprise-user' THEN ext_urn IS NULL
      WHEN party_type = 'self-identified-user' THEN TRUE -- Multiple formats, some NULL for self-identified users
    END
  );
