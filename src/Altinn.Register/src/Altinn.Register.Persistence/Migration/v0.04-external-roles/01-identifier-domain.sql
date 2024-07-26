-- Domain: register.identifier
CREATE DOMAIN register.identifier AS text CONSTRAINT identifier_valid CHECK (value ~ '^[a-z][a-z0-9_]{4,29}$');

