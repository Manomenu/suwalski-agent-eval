-- Simulated scenarios: a sample may stop because the simulated user could not be played (not the agent's doing), and keeps how long
-- the agent took for each turn (their sum is duration_ms; null for a sample of one turn) and whether the simulated user ended it early.
ALTER TABLE scenario_results DROP CONSTRAINT scenario_results_status_check;
ALTER TABLE scenario_results ADD CONSTRAINT scenario_results_status_check CHECK (status IN ('Done', 'Failed', 'NotSimulated'));
ALTER TABLE scenario_results ADD COLUMN turn_durations_ms int[] NULL;
ALTER TABLE scenario_results ADD COLUMN ended_by_user boolean NOT NULL DEFAULT false;
