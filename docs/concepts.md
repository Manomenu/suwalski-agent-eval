# Concepts: scenarios, runs, checks, results, regressions

What Suwageval works with, in the order a run goes through it. The file format is in
[scenario-format.md](scenario-format.md); why it looks the way it does in
[universal-approach.md](universal-approach.md).

## A scenario

A scenario is a frozen moment of a conversation with the agent, plus what we expect next:

- **The context, structured** — everything the agent had at that point:
  - the system's configuration: its LLM agents (each with its model, config such as the system prompt, and the tools it may call, their names and schemas);
  - the messages so far: user, assistant, system;
  - the tool calls already made: which tool, with which arguments, what it returned;
  - the data retrieved by RAG: the query and the chunks that came back (text, source, score);
  - the last message — the user's turn the agent answers next.
- **The tests** run on the agent's answer to that last message.

A **simulated scenario** describes a user in plain words instead of what they say next: Suwageval plays that user with a model,
turn after turn, for up to the scenario's number of turns, and its checks look at the whole conversation (what happened and what never
did, and a judge over the agent's behaviour). The simulated user's messages are never judged.

## A run

The agent under test receives the context and produces its next turn: a tool call, an answer,
or both. Tools are not executed for real — what they would return comes from the scenario, so
a run is repeatable and touches nothing outside.

## The tests

Run after the last message, on what the agent produced:

- **Deterministic** — no model involved, so cheap and exact:
  - the right tool was called: its name, its arguments (exact, a subset, or a matcher), the
    order and number of calls;
  - a tool that must not be called was not;
  - the answer contains / does not contain a text, matches a pattern, fits a format (e.g. a
    JSON schema).
- **LLM judge** — a model grades the answer against a rubric:
  - the answer contains the essential information (e.g. "states the refund period of 14 days");
  - the answer is grounded in the retrieved RAG chunks — nothing made up beyond them;
  - language, tone, what it must not promise.
  - a free-text argument or event field is "roughly like this" (a `$judge` matcher with a description
    and examples, only asked about calls whose exact parts already match).

  Each verdict carries pass/fail, a short justification and the evidence it relied on.

## The results

Per test: pass/fail and why. Per scenario: all its tests. Per run of a suite: the agent's
configuration and model, the judge's model and prompt version, the totals — and a comparison
with an earlier run, so a regression after a prompt or model change is visible at once.

## Regressions

A check that passed in the last 5 reference runs (clean tree, pushed commit, the repository's default branch or the branches
`agent.yaml` lists under `regression`) and fails now is a **regression**; a failing check with too little history is **new**.
A check that fails only because the agent gave no answer is an **agent error**: it is left out of the check's history. `suwageval run --wait` prints these first and exits 1 only for them; known failures, unstable and already broken checks leave
the exit code 0, `--strict` fails on any failed check, `--ref <branch>` picks the branch to compare with. A run started in a group (`run --group`) is an experiment: it is never a reference, its checks get no state and `run --wait` exits as with `--strict`. The rules are in
[scenario-format.md](scenario-format.md), "Regressions".

