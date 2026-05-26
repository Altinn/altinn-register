ALTER TYPE register.organization_source RENAME VALUE 'sdf' TO 'srs';
ALTER TYPE register.external_role_source ADD VALUE 'srs'; -- RegisteredWithSkatteetaten (skatteetatens register av indre selskap)
ALTER TYPE register.party_source ADD VALUE 'srs';
