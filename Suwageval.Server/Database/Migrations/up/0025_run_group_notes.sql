-- A free-text note on a run group: what the experiment is about. Shown only in the group's comparison; clearing it deletes the row.
CREATE TABLE run_group_notes (
    agent_id uuid NOT NULL REFERENCES agents (id) ON DELETE CASCADE,
    name text NOT NULL,
    note text NOT NULL,
    PRIMARY KEY (agent_id, name)
);
