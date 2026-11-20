ALTER TABLE runs ADD COLUMN contract_changed_during_run boolean NOT NULL DEFAULT false;
ALTER TABLE scenario_results ADD COLUMN undeclared jsonb NOT NULL DEFAULT '[]'::jsonb;
