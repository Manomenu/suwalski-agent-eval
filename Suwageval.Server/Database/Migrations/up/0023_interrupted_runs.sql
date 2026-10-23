-- A run the server stopped in the middle of (a restart, a deploy) is marked Interrupted at the next start.
ALTER TABLE runs DROP CONSTRAINT runs_status_check;
ALTER TABLE runs ADD CONSTRAINT runs_status_check CHECK (status IN ('Queued', 'Running', 'Done', 'Failed', 'Cancelled', 'Interrupted'));
