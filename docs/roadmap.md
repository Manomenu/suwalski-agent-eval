# Status, open questions and roadmap

**Status:** the repository was initialised (API, web app, database, gate, CI, containers, chart) with no evaluation
feature yet. Since then the steps of the roadmap below have been done; what the features do is in
[concepts.md](concepts.md) and [front-doors.md](front-doors.md).

## Open questions

Decided so far (the agent under test over HTTP, tags, non-determinism, who calls the API):
[TODO.md](../TODO.md), "Decided for the slices to come". Still open:

- **The scenario format:** a JSON schema of our own, an import from existing traces
  (OpenTelemetry GenAI conventions, Agent Framework's serialized threads), or both?
- **The model providers** for the agent and the judge, and the cost limits of a run.

## Roadmap

**First goal (MVP):** from `suwalski-example-agent`, `suwageval validate`, `push` the agent
and a scenario, `run --tag regression --wait` against the running agent, and read the
result of deterministic checks (a tool called, an event, text) in the CLI. No judge, no
multi-turn simulation, no web views yet (the judge came right after: step 4).

1. **Contracts and `validate`** — done: `Suwageval.Contracts` (format v1) and the CLI's
   `validate`; the YAML files in the example agent.
2. **Agents, Scenarios (with tags), Checks** — done: the slices that store them (HTTP endpoints
   in `*Endpoints.cs`), and `suwageval push`.
3. **Invocation and Runs** — done: the agent under test over HTTP (contract v1: `/suwageval/turn` and `/suwageval/contract`, [scenario-format.md](scenario-format.md)), the
   deterministic checks, and `suwageval run` (`--wait` exits 0 when every check passed its
   threshold, 1 when one did not, 2 when the run could not finish; since regressions, 1 means a regression or a new failing check unless `--strict`). The MVP is reached here.
4. **After the MVP** — the judge is done: a `judge` check, verdict by a model through Microsoft Agent Framework
   ([scenario-format.md](scenario-format.md), "The judge check"), with a daily live check of the model from the cluster. Still to come:
   multi-turn simulation, results and comparisons on the web.
   The Claude Code plugin over the CLI is done (see "Use it from Claude Code").

Microsoft Agent Framework came in with the judge, the first code that uses it ([AGENTS.md](../AGENTS.md), section 0), together
with its live test (`scripts/.internal/live-check.sh`) and the recorded answers the gate uses instead of a model.
The user simulator will use the same per-model client and pool of calls.

