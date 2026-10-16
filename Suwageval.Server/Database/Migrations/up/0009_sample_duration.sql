-- How long the call to the agent took, measured by Suwageval; null when no call was made.
ALTER TABLE scenario_results ADD COLUMN duration_ms int NULL;
