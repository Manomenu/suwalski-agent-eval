ALTER TABLE runs ADD COLUMN data_version text NULL;
ALTER TABLE runs ADD COLUMN data_description text NULL;
DELETE FROM agent_versions WHERE content #>> '{context,retrieval,dataVersion}' IS NOT NULL;
ALTER TABLE agent_versions DROP COLUMN data_description;
