ALTER TABLE runs ADD COLUMN recheck_of uuid NULL REFERENCES runs (id);

ALTER TABLE runs ADD COLUMN skipped_scenarios jsonb NOT NULL DEFAULT '[]';

CREATE INDEX runs_recheck_of_idx ON runs (recheck_of) WHERE recheck_of IS NOT NULL;
