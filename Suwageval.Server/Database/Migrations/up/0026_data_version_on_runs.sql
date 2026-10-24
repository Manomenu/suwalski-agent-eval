-- A version is the agent's behaviour (prompts, models, tools, configuration), no longer its retrieval data: dataVersion leaves the
-- version's content and hash and becomes an attribute of the run, with its description. Versions that differed only in their data
-- become one (the oldest), their runs move to it and keep the data they ran on.
ALTER TABLE runs ADD COLUMN data_version text NULL;
ALTER TABLE runs ADD COLUMN data_description text NULL;
UPDATE runs SET data_version = agent_versions.content #>> '{context,retrieval,dataVersion}', data_description = agent_versions.data_description
FROM agent_versions WHERE agent_versions.id = runs.agent_version_id;

-- The content as the server now stores it: the data version null, as the description already is.
CREATE TEMPORARY TABLE version_content ON COMMIT DROP AS
SELECT id, agent_id, created_at, archived_at,
    content #>> '{context,retrieval,dataVersion}' IS NOT NULL AS had_data_version,
    CASE WHEN jsonb_typeof(content #> '{context,retrieval}') = 'object'
        THEN jsonb_set(content, '{context,retrieval,dataVersion}', 'null')
        ELSE content END AS content
FROM agent_versions;

CREATE TEMPORARY TABLE version_merge ON COMMIT DROP AS
SELECT id, first_value(id) OVER (PARTITION BY agent_id, content ORDER BY created_at, id) AS kept FROM version_content;

UPDATE runs SET agent_version_id = version_merge.kept
FROM version_merge WHERE runs.agent_version_id = version_merge.id AND version_merge.id <> version_merge.kept;

-- The merged version is archived only when every version it was made of was.
UPDATE agent_versions SET archived_at = NULL
WHERE id IN (
    SELECT version_merge.kept FROM version_merge JOIN version_content USING (id) WHERE version_content.archived_at IS NULL
);

DELETE FROM agent_versions USING version_merge WHERE agent_versions.id = version_merge.id AND version_merge.id <> version_merge.kept;

-- The hash is the SDK's (AgentContractHash), computed in C# over the contract as the agent serves it, which SQL cannot repeat: a
-- changed version gets a placeholder, and the next registration of the same content (AgentStore.RegisterVersionAsync) finds it by
-- its content and writes the real hash.
UPDATE agent_versions SET content = version_content.content, content_hash = 'pending:' || agent_versions.id::text
FROM version_content WHERE version_content.id = agent_versions.id AND version_content.had_data_version;

ALTER TABLE agent_versions DROP COLUMN data_description;
