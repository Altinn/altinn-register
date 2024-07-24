-- Composite: register._address (private - implementation detail)
DO $$
BEGIN
  CREATE TYPE register._address AS(
    municipal_number text,
    municipal_name text,
    street_name text,
    house_number text,
    house_letter text,
    apartment_number text,
    postal_code text,
    city text
);
EXCEPTION
  WHEN duplicate_object THEN
    NULL;
END
$$;

-- Domain: register.address
DO $$
BEGIN
  -- No checks at this time, but create a domain so they can be added later
  CREATE DOMAIN register.address AS register._address;
EXCEPTION
  WHEN duplicate_object THEN
    NULL;
END
$$;

