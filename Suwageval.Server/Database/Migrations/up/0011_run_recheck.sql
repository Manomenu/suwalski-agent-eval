-- A re-check is a run that evaluates the stored answers of an earlier run with the current checks, without calling the agent.
-- recheck_of is always a run that called the agent (a re-check of a re-check points at the original).
ALTER TABLE runs ADD COLUMN recheck_of uuid NULL REFERENCES runs (id);

-- The scenarios a re-check left out, as [{"key": ..., "reason": ...}]: the answers are of a conversation that changed or is gone.
ALTER TABLE runs ADD COLUMN skipped_scenarios jsonb NOT NULL DEFAULT '[]';

CREATE INDEX runs_recheck_of_idx ON runs (recheck_of) WHERE recheck_of IS NOT NULL;
