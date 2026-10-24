# Status, open questions and roadmap

**Status:** evaluation of an agent is built end to end — from the agent's repository (`validate`, `push`, `run`, `sweep`), through
the server (deterministic and model-judged checks, multi-turn simulation, regressions), to the web views and the Claude Code plugin.
What the features do is in [concepts.md](concepts.md) and [front-doors.md](front-doors.md); what is decided but not built yet is
in [TODO.md](../TODO.md).

## Open questions

- **Live traffic** (the next big step, below): how production conversations reach the server through Cloudflare Access, their
  retention and personal data. Decided: the retrieval data (`dataVersion`) is no part of a version's hash; each run keeps its own.
- **Cost limits of a run:** tokens and an estimated cost per run are not recorded yet (TODO.md, "Cost and tokens per run").

Decided along the way: the scenario format is our own YAML, contract v1 ([scenario-format.md](scenario-format.md), why it looks
the way it does: [universal-approach.md](universal-approach.md)); an import of real conversations comes with live traffic. Models
are configured by name (`Models:<name>`, AGENTS.md section 1), the judge and the user simulator share their pools.

## Roadmap

**First goal (MVP), reached:** from `suwalski-example-agent`, `suwageval validate`, `push` the agent and a scenario,
`run --tag regression --wait` against the running agent, and read the result of deterministic checks in the CLI.

1. **Contracts and `validate`** — done: `Suwageval.Contracts` (format v1) and the CLI's `validate`.
2. **Agents, Scenarios (with tags), Checks** — done: the slices that store them and `suwageval push`.
3. **Invocation and Runs** — done: the agent under test over HTTP (contract v1: `/suwageval/turn` and `/suwageval/contract`), the
   deterministic checks and `suwageval run` (exit codes: 0, 1 for a regression or a new failing check unless `--strict`, 2 when
   the run could not finish). The MVP.
4. **The judge and simulation** — done: a `judge` check through Microsoft Agent Framework with its live test and the daily live
   check from the cluster; multi-turn simulation with a simulated user played by a model.
5. **Regressions and the web** — done: automatic regression states over reference runs, re-checks, the run page, the comparison
   (groups, sweeps, baseline, "What differs", the version diff), the pass-rate trend, unstable checks, agent errors, versions and
   groups management, print of the run page and the comparison, notes on runs and groups; the Claude Code plugin over the CLI.
6. **Live traffic — next:** an agent sends its production conversations; agent-scoped checks run on them, with charts per check
   and version, and a conversation becomes a scenario in two clicks. Plan, stages and open questions:
   [Suwageval: rozmowy z produkcji i checks na żywym ruchu — plan](https://claude.ai/artifact/Y7FDEQtqn36YaaCUwA4p2e).

Microsoft Agent Framework came in with the judge, the first code that uses it ([AGENTS.md](../AGENTS.md), section 0), together
with its live test (`scripts/.internal/live-check.sh`) and the recorded answers the gate uses instead of a model.
