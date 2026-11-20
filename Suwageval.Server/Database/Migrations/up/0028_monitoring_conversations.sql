CREATE TABLE monitoring_conversations (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_id uuid NOT NULL REFERENCES agents (id) ON DELETE CASCADE,
    environment text NOT NULL,
    conversation_key text NOT NULL,
    agent_version_id uuid NOT NULL REFERENCES agent_versions (id) ON DELETE CASCADE,
    version_changed boolean NOT NULL DEFAULT false,
    data_version text NULL,
    state text NOT NULL DEFAULT 'Open' CHECK (state IN ('Open', 'Closed')),
    turns int NOT NULL DEFAULT 0,
    started_at timestamptz NOT NULL,
    last_turn_at timestamptz NOT NULL,
    received_at timestamptz NOT NULL DEFAULT now(),
    closed_at timestamptz NULL,
    UNIQUE (agent_id, environment, conversation_key)
);

CREATE INDEX monitoring_conversations_list_idx ON monitoring_conversations (agent_id, last_turn_at DESC, id);
CREATE INDEX monitoring_conversations_open_idx ON monitoring_conversations (received_at) WHERE state = 'Open';

CREATE TABLE monitoring_turns (
    id uuid PRIMARY KEY,
    conversation_id uuid NOT NULL REFERENCES monitoring_conversations (id) ON DELETE CASCADE,
    agent_version_id uuid NOT NULL REFERENCES agent_versions (id) ON DELETE CASCADE,
    data_version text NULL,
    at timestamptz NOT NULL,
    received_at timestamptz NOT NULL DEFAULT now(),
    items jsonb NOT NULL
);

CREATE INDEX monitoring_turns_conversation_idx ON monitoring_turns (conversation_id, at, received_at);
