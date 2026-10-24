-- Which areas an agent uses (features: in agent.yaml), as the last push said: evaluation on unless switched off, monitoring off unless
-- its settings are there (closeAfter, environments; '{}' for the defaults).
ALTER TABLE agents ADD COLUMN evaluation boolean NOT NULL DEFAULT true;
ALTER TABLE agents ADD COLUMN monitoring jsonb NULL;
