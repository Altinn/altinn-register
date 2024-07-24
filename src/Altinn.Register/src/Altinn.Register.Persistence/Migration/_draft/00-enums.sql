-- Enum: register.party_type
DO $$
BEGIN
  CREATE TYPE register.party_type AS ENUM(
    'person',
    'organization'
);
EXCEPTION
  WHEN duplicate_object THEN
    NULL;
END
$$;

-- Enum: register.party_source
DO $$
BEGIN
  CREATE TYPE register.party_source AS ENUM(
    'folkeregistered',
    'enhetsregisteret'
);
EXCEPTION
  WHEN duplicate_object THEN
    NULL;
END
$$;

