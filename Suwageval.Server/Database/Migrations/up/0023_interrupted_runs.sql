ALTER TABLE runs DROP CONSTRAINT runs_status_check;
ALTER TABLE runs ADD CONSTRAINT runs_status_check CHECK (status IN ('Queued', 'Running', 'Done', 'Failed', 'Cancelled', 'Interrupted'));
