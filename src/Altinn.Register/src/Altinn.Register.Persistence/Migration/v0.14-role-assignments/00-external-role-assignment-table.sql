-- CREATE TABLE register.external_role (
-- 	"source" register.party_source NOT NULL,
-- 	identifier register.identifier NOT NULL,
-- 	from_party uuid NOT NULL,
-- 	to_party uuid NOT NULL,
-- 	CONSTRAINT external_role_pkey PRIMARY KEY (source, identifier, from_party, to_party),
-- 	CONSTRAINT external_role_from_party_fkey FOREIGN KEY (from_party) REFERENCES register.party(uuid) ON DELETE CASCADE ON UPDATE CASCADE,
-- 	CONSTRAINT external_role_source_identifier_fkey FOREIGN KEY ("source",identifier) REFERENCES register.external_role_definition("source",identifier) ON DELETE CASCADE ON UPDATE CASCADE,
-- 	CONSTRAINT external_role_to_party_fkey FOREIGN KEY (to_party) REFERENCES register.party(uuid) ON DELETE CASCADE ON UPDATE CASCADE
-- );

ALTER TABLE register.external_role
RENAME TO external_role_assignment;
