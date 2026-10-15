CREATE TABLE agents (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name text NOT NULL UNIQUE,
    endpoint_url text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE agent_versions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_id uuid NOT NULL REFERENCES agents (id),
    content jsonb NOT NULL,
    content_hash text NOT NULL,
    label text NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (agent_id, content_hash)
);
