-- What a run observed about the agent's contract: it changed while the run was going, and per sample the tool calls and events
-- the version does not declare (a JSON array of sentences).
ALTER TABLE runs ADD COLUMN contract_changed_during_run boolean NOT NULL DEFAULT false;
ALTER TABLE scenario_results ADD COLUMN undeclared jsonb NOT NULL DEFAULT '[]'::jsonb;
