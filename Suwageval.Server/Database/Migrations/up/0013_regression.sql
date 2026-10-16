-- Regression detection: the agent's settings (regression: in agent.yaml; both null means the defaults).
ALTER TABLE agents ADD COLUMN regression_branches text[] NULL;
ALTER TABLE agents ADD COLUMN regression_history int NULL;

-- Where the repository stood when a run started (the contract's RunOrigin as JSON; null: no git), whether the run is a reference run
-- and the branch whose history it is compared with (null: every run of the agent). Decided once, when the run is created.
ALTER TABLE runs ADD COLUMN origin jsonb NULL;
ALTER TABLE runs ADD COLUMN reference boolean NOT NULL DEFAULT false;
ALTER TABLE runs ADD COLUMN compared_with text NULL;

-- Serves the history of a check: the finished reference runs of an agent on one branch, newest first.
CREATE INDEX runs_reference_history_idx ON runs (agent_id, (origin ->> 'branch'), created_at DESC)
    WHERE reference AND recheck_of IS NULL AND status = 'Done';
