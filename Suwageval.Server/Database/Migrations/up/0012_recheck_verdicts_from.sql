ALTER TABLE runs ADD COLUMN verdicts_from uuid NULL REFERENCES runs (id);
