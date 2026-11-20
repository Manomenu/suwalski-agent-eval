ALTER TABLE runs ADD COLUMN label text NULL;
UPDATE runs SET label = agent_versions.label FROM agent_versions WHERE agent_versions.id = runs.agent_version_id;
