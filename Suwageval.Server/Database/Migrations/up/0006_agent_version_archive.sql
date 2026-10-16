-- A version is never deleted (every run points at it); it is archived: hidden by default, restorable.
ALTER TABLE agent_versions ADD COLUMN archived_at timestamptz NULL;
