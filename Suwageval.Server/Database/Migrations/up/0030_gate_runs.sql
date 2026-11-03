-- Gate runs (suwageval gate). The agent's default number of attempts of a scenario on the gate (acGuardrailRetryLimit: in agent.yaml,
-- none means 5), as the last push said. A run with gate_retry_limit set is a gate run, and the value is that default as it was when
-- the run started, so a later push does not change what the run's results say.
ALTER TABLE agents ADD COLUMN ac_guardrail_retry_limit integer NULL;
ALTER TABLE runs ADD COLUMN gate_retry_limit integer NULL;
