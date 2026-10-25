-- A version is the agent's behaviour (prompts, models, tools, configuration), no longer its retrieval data: dataVersion leaves the
-- version's content and hash and becomes an attribute of the run, with its description. A version stored with its data version in
-- the content is deleted with its runs: its hash no longer matches what the agent sends, so the agent registers it again.
ALTER TABLE runs ADD COLUMN data_version text NULL;
ALTER TABLE runs ADD COLUMN data_description text NULL;
DELETE FROM agent_versions WHERE content #>> '{context,retrieval,dataVersion}' IS NOT NULL;
ALTER TABLE agent_versions DROP COLUMN data_description;
