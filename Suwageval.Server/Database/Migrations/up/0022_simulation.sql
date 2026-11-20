ALTER TABLE scenario_results DROP CONSTRAINT scenario_results_status_check;
ALTER TABLE scenario_results ADD CONSTRAINT scenario_results_status_check CHECK (status IN ('Done', 'Failed', 'NotSimulated'));
ALTER TABLE scenario_results ADD COLUMN turn_durations_ms int[] NULL;
ALTER TABLE scenario_results ADD COLUMN ended_by_user boolean NOT NULL DEFAULT false;
