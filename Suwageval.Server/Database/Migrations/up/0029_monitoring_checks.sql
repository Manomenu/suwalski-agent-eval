CREATE TABLE monitoring_checks (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_id uuid NOT NULL REFERENCES agents (id) ON DELETE CASCADE,
    key text NOT NULL,
    deleted_at timestamptz NULL,
    UNIQUE (agent_id, key)
);

CREATE TABLE monitoring_check_revisions (
    check_id uuid NOT NULL REFERENCES monitoring_checks (id) ON DELETE CASCADE,
    revision int NOT NULL,
    content jsonb NOT NULL,
    content_hash text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (check_id, revision)
);

CREATE TABLE monitoring_check_results (
    conversation_id uuid NOT NULL REFERENCES monitoring_conversations (id) ON DELETE CASCADE,
    check_id uuid NOT NULL REFERENCES monitoring_checks (id) ON DELETE CASCADE,
    revision int NOT NULL,
    passed boolean NOT NULL,
    not_judged boolean NOT NULL DEFAULT false,
    message text NOT NULL,
    evidence jsonb NOT NULL,
    judge_model text NULL,
    PRIMARY KEY (conversation_id, check_id),
    FOREIGN KEY (check_id, revision) REFERENCES monitoring_check_revisions (check_id, revision) ON DELETE CASCADE
);

ALTER TABLE monitoring_conversations ADD COLUMN checked_at timestamptz NULL;
CREATE INDEX monitoring_conversations_unchecked_idx ON monitoring_conversations (closed_at) WHERE state = 'Closed' AND checked_at IS NULL;
