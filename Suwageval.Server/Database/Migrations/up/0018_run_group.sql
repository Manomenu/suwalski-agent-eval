ALTER TABLE runs ADD COLUMN run_group text NULL;

CREATE INDEX runs_group_idx ON runs (agent_id, run_group, created_at) WHERE run_group IS NOT NULL;
