CREATE TABLE scenarios (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_id uuid NOT NULL REFERENCES agents (id),
    key text NOT NULL,
    tags text[] NOT NULL DEFAULT '{}',
    deleted_at timestamptz NULL,
    UNIQUE (agent_id, key)
);

CREATE INDEX scenarios_tags_idx ON scenarios USING GIN (tags);

CREATE TABLE scenario_revisions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    scenario_id uuid NOT NULL REFERENCES scenarios (id),
    revision int NOT NULL,
    content jsonb NOT NULL,
    content_hash text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (scenario_id, revision)
);
