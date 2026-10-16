-- The agent's default number of samples (samples: in agent.yaml; null means the default, 3).
ALTER TABLE agents ADD COLUMN samples int NULL;
