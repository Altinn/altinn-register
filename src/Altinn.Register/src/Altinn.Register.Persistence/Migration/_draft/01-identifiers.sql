-- Domain: register.person_identifier
DO $$
BEGIN
  CREATE DOMAIN register.person_identifier AS text CONSTRAINT fmtchk CHECK(value ~ '^[0-9]{11}$');
EXCEPTION
  WHEN duplicate_object THEN
    NULL;
END
$$;

-- Domain: register.organization_identifier
DO $$
BEGIN
  CREATE DOMAIN register.organization_identifier AS text CONSTRAINT fmtchk CHECK(value ~ '^[0-9]{9}$');
EXCEPTION
  WHEN duplicate_object THEN
    NULL;
END
$$;

