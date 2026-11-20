ALTER TABLE agents ADD COLUMN ac_guardrail_retry_limit integer NULL;
ALTER TABLE runs ADD COLUMN gate_retry_limit integer NULL;
