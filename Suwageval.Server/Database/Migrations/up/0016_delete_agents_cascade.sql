-- An agent can be deleted (AgentStore.DeleteAgentAsync): everything that belongs to it goes with it. Next to the chain of 0015
-- (version -> runs -> samples -> verdicts) these are the agent's own rows: versions, scenarios and checks with their revisions;
-- the samples and verdicts also point at the revisions they were made with.
ALTER TABLE agent_versions DROP CONSTRAINT agent_versions_agent_id_fkey;
ALTER TABLE agent_versions ADD CONSTRAINT agent_versions_agent_id_fkey FOREIGN KEY (agent_id) REFERENCES agents (id) ON DELETE CASCADE;

ALTER TABLE scenarios DROP CONSTRAINT scenarios_agent_id_fkey;
ALTER TABLE scenarios ADD CONSTRAINT scenarios_agent_id_fkey FOREIGN KEY (agent_id) REFERENCES agents (id) ON DELETE CASCADE;

ALTER TABLE scenario_revisions DROP CONSTRAINT scenario_revisions_scenario_id_fkey;
ALTER TABLE scenario_revisions ADD CONSTRAINT scenario_revisions_scenario_id_fkey FOREIGN KEY (scenario_id) REFERENCES scenarios (id) ON DELETE CASCADE;

ALTER TABLE checks DROP CONSTRAINT checks_agent_id_fkey;
ALTER TABLE checks ADD CONSTRAINT checks_agent_id_fkey FOREIGN KEY (agent_id) REFERENCES agents (id) ON DELETE CASCADE;

ALTER TABLE checks DROP CONSTRAINT checks_scenario_id_fkey;
ALTER TABLE checks ADD CONSTRAINT checks_scenario_id_fkey FOREIGN KEY (scenario_id) REFERENCES scenarios (id) ON DELETE CASCADE;

ALTER TABLE check_revisions DROP CONSTRAINT check_revisions_check_id_fkey;
ALTER TABLE check_revisions ADD CONSTRAINT check_revisions_check_id_fkey FOREIGN KEY (check_id) REFERENCES checks (id) ON DELETE CASCADE;

ALTER TABLE runs DROP CONSTRAINT runs_agent_id_fkey;
ALTER TABLE runs ADD CONSTRAINT runs_agent_id_fkey FOREIGN KEY (agent_id) REFERENCES agents (id) ON DELETE CASCADE;

ALTER TABLE scenario_results DROP CONSTRAINT scenario_results_scenario_id_scenario_revision_fkey;
ALTER TABLE scenario_results ADD CONSTRAINT scenario_results_scenario_id_scenario_revision_fkey
    FOREIGN KEY (scenario_id, scenario_revision) REFERENCES scenario_revisions (scenario_id, revision) ON DELETE CASCADE;

ALTER TABLE check_results DROP CONSTRAINT check_results_check_id_check_revision_fkey;
ALTER TABLE check_results ADD CONSTRAINT check_results_check_id_check_revision_fkey
    FOREIGN KEY (check_id, check_revision) REFERENCES check_revisions (check_id, revision) ON DELETE CASCADE;
