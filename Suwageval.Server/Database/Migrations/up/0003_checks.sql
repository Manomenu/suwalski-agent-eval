CREATE TABLE checks (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_id uuid NOT NULL REFERENCES agents (id),
    key text NOT NULL,
    scenario_id uuid NOT NULL REFERENCES scenarios (id),
    deleted_at timestamptz NULL,
    UNIQUE (agent_id, key)
);

CREATE INDEX checks_scenario_id_idx ON checks (scenario_id);

CREATE TABLE check_revisions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    check_id uuid NOT NULL REFERENCES checks (id),
    revision int NOT NULL,
    content jsonb NOT NULL,
    content_hash text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (check_id, revision)
);
