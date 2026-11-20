ALTER TABLE agents ADD COLUMN regression_branches text[] NULL;
ALTER TABLE agents ADD COLUMN regression_history int NULL;

ALTER TABLE runs ADD COLUMN origin jsonb NULL;
ALTER TABLE runs ADD COLUMN reference boolean NOT NULL DEFAULT false;
ALTER TABLE runs ADD COLUMN compared_with text NULL;

CREATE INDEX runs_reference_history_idx ON runs (agent_id, (origin ->> 'branch'), created_at DESC)
    WHERE reference AND recheck_of IS NULL AND status = 'Done';
