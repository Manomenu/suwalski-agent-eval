-- The label the agent's contract carried when the run started. The label is outside the version's hash, so a version keeps the label it
-- was first seen with; the run keeps its own. Existing runs take their version's label.
ALTER TABLE runs ADD COLUMN label text NULL;
UPDATE runs SET label = agent_versions.label FROM agent_versions WHERE agent_versions.id = runs.agent_version_id;
