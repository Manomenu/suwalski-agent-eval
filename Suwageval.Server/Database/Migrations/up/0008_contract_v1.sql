-- Contract v1 replaces v0 with no compatibility (docs/scenario-format.md, "The agent contract"): a stored version holds v0
-- JSON that v1 can no longer read, and runs and their results point at those versions. Nothing in them is worth migrating yet,
-- so they go, in the order of their foreign keys. Agents, scenarios and checks stay.
DELETE FROM check_results;
DELETE FROM scenario_results;
DELETE FROM runs;
DELETE FROM agent_versions;
