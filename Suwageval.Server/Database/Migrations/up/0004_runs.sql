-- A run is a row in a queue: the worker takes the oldest 'Queued' one (FOR UPDATE SKIP LOCKED).
CREATE TABLE runs (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_id uuid NOT NULL REFERENCES agents (id),
    agent_version_id uuid NOT NULL REFERENCES agent_versions (id),
    tag text NULL,
    endpoint_url text NOT NULL,
    status text NOT NULL DEFAULT 'Queued' CHECK (status IN ('Queued', 'Running', 'Done', 'Failed', 'Cancelled')),
    cancel_requested boolean NOT NULL DEFAULT false,
    error text NULL,
    samples_total int NOT NULL DEFAULT 0,
    created_at timestamptz NOT NULL DEFAULT now(),
    started_at timestamptz NULL,
    finished_at timestamptz NULL
);

CREATE INDEX runs_queue_idx ON runs (created_at, id) WHERE status = 'Queued';
CREATE INDEX runs_agent_id_idx ON runs (agent_id, created_at DESC);

-- One row per sample of a scenario: what the agent produced for it, or why it produced nothing.
CREATE TABLE scenario_results (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    run_id uuid NOT NULL REFERENCES runs (id),
    scenario_id uuid NOT NULL,
    scenario_revision int NOT NULL,
    sample int NOT NULL,
    status text NOT NULL CHECK (status IN ('Done', 'Failed')),
    output jsonb NULL,
    error text NULL,
    FOREIGN KEY (scenario_id, scenario_revision) REFERENCES scenario_revisions (scenario_id, revision),
    UNIQUE (run_id, scenario_id, sample)
);

-- One row per check and sample: the verdict. The pass rate over samples is computed when the results are read.
CREATE TABLE check_results (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    scenario_result_id uuid NOT NULL REFERENCES scenario_results (id),
    check_id uuid NOT NULL,
    check_revision int NOT NULL,
    passed boolean NOT NULL,
    evidence jsonb NOT NULL,
    message text NOT NULL,
    FOREIGN KEY (check_id, check_revision) REFERENCES check_revisions (check_id, revision),
    UNIQUE (scenario_result_id, check_id)
);
