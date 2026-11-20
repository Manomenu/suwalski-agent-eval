ALTER TABLE agents ADD COLUMN evaluation boolean NOT NULL DEFAULT true;
ALTER TABLE agents ADD COLUMN monitoring jsonb NULL;
