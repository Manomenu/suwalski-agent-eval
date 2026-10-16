-- The run a re-check copies the verdicts of unchanged checks from: the run the person looked at (a re-check of a re-check
-- copies from the earlier re-check, whose verdicts are already for the same answers). recheck_of stays the run that called the agent.
ALTER TABLE runs ADD COLUMN verdicts_from uuid NULL REFERENCES runs (id);
