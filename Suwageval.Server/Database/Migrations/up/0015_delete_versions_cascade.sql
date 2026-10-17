-- A version can be deleted (only an archived one, see AgentStore.DeleteVersionAsync): everything that belongs to it goes with it.
-- The chain is version -> runs -> samples (scenario_results) -> verdicts (check_results); a re-check or a run that copies verdicts
-- points at its source run, so it is deleted with that run.
ALTER TABLE runs DROP CONSTRAINT runs_agent_version_id_fkey;
ALTER TABLE runs ADD CONSTRAINT runs_agent_version_id_fkey FOREIGN KEY (agent_version_id) REFERENCES agent_versions (id) ON DELETE CASCADE;

ALTER TABLE runs DROP CONSTRAINT runs_recheck_of_fkey;
ALTER TABLE runs ADD CONSTRAINT runs_recheck_of_fkey FOREIGN KEY (recheck_of) REFERENCES runs (id) ON DELETE CASCADE;

ALTER TABLE runs DROP CONSTRAINT runs_verdicts_from_fkey;
ALTER TABLE runs ADD CONSTRAINT runs_verdicts_from_fkey FOREIGN KEY (verdicts_from) REFERENCES runs (id) ON DELETE CASCADE;

ALTER TABLE scenario_results DROP CONSTRAINT scenario_results_run_id_fkey;
ALTER TABLE scenario_results ADD CONSTRAINT scenario_results_run_id_fkey FOREIGN KEY (run_id) REFERENCES runs (id) ON DELETE CASCADE;

ALTER TABLE check_results DROP CONSTRAINT check_results_scenario_result_id_fkey;
ALTER TABLE check_results ADD CONSTRAINT check_results_scenario_result_id_fkey FOREIGN KEY (scenario_result_id) REFERENCES scenario_results (id) ON DELETE CASCADE;
