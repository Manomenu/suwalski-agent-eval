-- What the agent says its retrieval data is (context.retrieval.description). For people, outside the content hash like the label.
ALTER TABLE agent_versions ADD COLUMN data_description text NULL;
