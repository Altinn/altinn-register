CREATE TABLE register.saga_state (
	id uuid NOT NULL PRIMARY KEY,
	status register.saga_status NOT NULL,
	state_type text NOT NULL,
	state_value jsonb NOT NULL
);
