CREATE TABLE archived_run_groups (
    agent_id uuid NOT NULL REFERENCES agents (id) ON DELETE CASCADE,
    name text NOT NULL,
    archived_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (agent_id, name)
);
