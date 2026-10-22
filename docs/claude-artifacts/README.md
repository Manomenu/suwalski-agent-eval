# Claude artifacts

Design documents that live on claude.ai, not in the repo — written with the owner while
planning, editable and commentable there. One entry per artifact: the link and what it holds.
When a document's decision lands in the code, the code and AGENTS.md win; the artifact is the
record of how we got there.

| Artifact | What it holds |
| --- | --- |
| [suwageval — organizacja projektu i API](https://claude.ai/artifact/UfjpCwDbQJVW9dd9zqwUGY) | The plan of suwageval: repo layout, server layers and feature slices, the inside of a slice, the data model with the context contract and events, the API manifest, checks as conditions on a run's turns, and diagrams of preparing an evaluation (API calls) and of a run. |
| [Suwageval — jak przetestować z example-agent](https://claude.ai/artifact/5TcYJo3qwtnxqXcQn3B3ow) | The manual end-to-end test with `suwalski-example-agent` on the local model: one-time setup, the three processes, `validate` / `push` / `run` with the output to expect, what else to try, and what to do when a step fails. |
| [Suwageval kontrakt v1](https://claude.ai/artifact/WUCTvZHqQXuBGSpZGiV68v) | What the merge of `more-universal-approach` brought (contract v1: `agents[]`, `otherConfig`, `context.retrieval.dataVersion`, fixtures; checks `by`, `result`, `event-not-emitted`, `cited`, `$pattern`; correlation headers, turn time), when to use each, what it broke and what was left out. |
| [Regresje w Suwageval](https://claude.ai/artifact/2mFra9GHziCuWGwbzQA2xc) | The design of automatic regression detection: a check's states from its history over reference runs (regression, broken on master, known failure, fixed, unstable, new), which runs are reference runs (`regression.branches` and `history` in `agent.yaml`, git facts sent by the CLI), what a run is compared with, the CLI and run page output, the exit codes, what to build, and the open decisions. |
| [Suwageval — symulacja wieloturowa](https://claude.ai/artifact/8Xw9RyvSmFUX2gDNFCu4se) | The plan of multi-turn simulation: a scenario's `continuation.simulatedUser` (free-text customer) and `turns`, what a turn is, the user simulator (what it sees, early end, model), `conversationId` in the turn request, checks and the judge over the whole simulation (only the agent is judged), how samples, turn times and re-checks are stored, the four MRs, and the decisions as built. |
| [Suwageval — symulacja wieloturowa: wdrożenie i kroki](https://claude.ai/artifact/97QQGPLndiU2FDjd4vry5z) | How multi-turn simulation was built on `feature/multi-turn-scenarios` (area → what → where in the code), the matching branch of `suwalski-example-agent` (a shop per `conversationId` and a simulated scenario), and the owner's checklist: MR and CI, the manual end-to-end test, merge order, the daily live check, releasing the SDK, the optional temperature on the cluster. |
