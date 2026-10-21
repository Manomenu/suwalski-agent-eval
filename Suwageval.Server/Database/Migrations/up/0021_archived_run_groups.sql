-- A run group is archived before it can be deleted with its runs, like a version. A row exists only while the group is archived:
-- a run started in an archived group puts it back in use.
CREATE TABLE archived_run_groups (
    agent_id uuid NOT NULL REFERENCES agents (id) ON DELETE CASCADE,
    name text NOT NULL,
    archived_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (agent_id, name)
);
