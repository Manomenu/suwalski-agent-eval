CREATE TABLE run_group_notes (
    agent_id uuid NOT NULL REFERENCES agents (id) ON DELETE CASCADE,
    name text NOT NULL,
    note text NOT NULL,
    PRIMARY KEY (agent_id, name)
);
