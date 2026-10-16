# The scenario files (format v1)

What an agent's repository keeps for Suwageval, and what `suwageval validate` checks. The
files are YAML in a folder `suwageval/` at the root of the agent's repository (`--dir` or the
`path` argument points elsewhere):

```
suwageval/
  agent.yaml            the agent: its name and where it runs
  scenarios/
    refund-happy-path.yaml   one scenario per file, its checks inside
    ...
```

The fields are the records of `Suwageval.Contracts` in camelCase. Unknown fields, missing
required fields and wrong values are errors, reported with file, line and path. `validate`
needs no server; it asks the running agent for its contract (below) to check names, and says so
when it could not:

```sh
suwageval validate                      # ./suwageval
suwageval validate path/to/suwageval    # another folder
suwageval validate scenarios/x.yaml     # one scenario, checked against ./suwageval/agent.yaml (--dir for another)
suwageval validate - < scenario.yaml    # one scenario from stdin, same
suwageval validate --endpoint URL       # read the agent's contract from URL instead of agent.yaml's endpointUrl
suwageval validate --offline            # do not ask the agent: the structure only
suwageval validate --json               # { "valid": bool, "namesChecked": bool, "notice": string|null, "problems": [{ "file", "path", "message" }] }
```

Exit code 0 when valid, 1 when anything is wrong. A file that does not match the contract is
reported for its structure first; what the files say about each other (keys, tool results) is
checked once its structure is right. **Tool and event names** are checked against the contract
the running agent serves: `validate` GETs `{endpointUrl}/suwageval/contract` (5 s timeout). When
the agent cannot be reached, or does not serve a contract, the names are not checked, a notice
says why (`names not checked: agent at http://localhost:7010 unreachable (connection refused)`),
`namesChecked` is `false` and the exit code is still 0 when the files are right. `--offline`
skips the fetch.

## `agent.yaml`

Only the agent's name and where it runs. **What the agent is** — its LLM agents with their prompts, models and tools, and its events —
is not written in any file: the running agent serves it (next section), so a run always records
what really ran. A `version:` key is an error.

| Field | | |
| --- | --- | --- |
| `name` | required | the agent's name |
| `endpointUrl` | required | where the agent is reached: `POST {endpointUrl}/suwageval/turn`, `GET {endpointUrl}/suwageval/contract` |
| `regression` | optional | how regressions are found (next block, section "Regressions") |

```yaml
regression:
  branches: [master, release] # optional; none means the repository's default branch
  history: 5 # optional; 1 to 20, default 5
```

- `branches` — the branches whose runs make the history of a check (the main one first). When given, the list is not empty,
  each name is non-empty without whitespace, and none is named twice. Left out, it is the repository's default branch
  (`origin/HEAD`, read by the CLI).
- `history` — how many of the newest reference runs make a check's history: an integer from 1 to 20 (default 5).

`suwageval validate` and `push` check both (the server answers 422 for the same problems).

## The agent contract (contract v1)

An agent under test is a system of one or more LLM agents. It serves two routes. `POST /suwageval/turn` runs a
turn (the conversation in, the items it produced out). `GET /suwageval/contract` says what the system is:

```json
{
  "label": "3f2a9c1",
  "agents": [
    {
      "name": "support-agent",
      "model": { "name": "gpt-4o-mini", "parameters": { "temperature": 0 } },
      "config": "You are a polite customer support agent ...",
      "tools": [
        {
          "name": "get_order",
          "description": "Look up an order: its status, items and total.",
          "parameters": { "type": "object", "properties": { "order_id": { "type": "string" } }, "required": ["order_id"] }
        }
      ]
    }
  ],
  "otherConfig": "retries on every LLM agent: 2",
  "context": {
    "usesRetrieval": true,
    "retrieval": { "dataVersion": "index-build-2026-10-14.3", "description": "1832 articles, help center + returns policy" },
    "events": [{ "type": "state.changed", "dataSchema": { "type": "object" } }],
    "fixturesSchema": { "type": "object" }
  }
}
```

| Field | | |
| --- | --- | --- |
| `label` | | a name for this version, e.g. the git SHA; no part of the content: renaming alone is not a new version |
| `agents` | required | the LLM agents of the system, at least one |
| `agents[].name` | required | non-empty, unique among the agents; what a tool call's `agent` and a check's `by` refer to |
| `agents[].model` | required | `name` (required, non-empty) and `parameters` (free mapping, e.g. `temperature`) |
| `agents[].config` | required | free text that defines this agent: its system prompt, a scope policy, whatever it is; may be empty |
| `agents[].tools` | | the tools THIS agent may call: `name` (unique within the agent; two agents may share a name), `description`, `parameters` (a JSON Schema) |
| `otherConfig` | | free text for what belongs to the system, not to one agent (e.g. "retries on every LLM agent: 2") |
| `context.usesRetrieval` | | `true` if the agent retrieves chunks (RAG); default `false` |
| `context.retrieval` | with `usesRetrieval: true` | the data retrieval reads. Required exactly when `usesRetrieval` is `true`, and an error when it is `false` |
| `context.retrieval.dataVersion` | required | a non-empty, opaque string: a fingerprint of what retrieval can return, the content AND how it was indexed. Part of the content hash: new data is a new version, which can be filtered and compared like a model |
| `context.retrieval.description` | | for people (e.g. `1832 articles, help center + returns policy`), shown on the web and in the comparison header; no part of the content, like `label` |
| `context.events` | | the events the agent emits: `type` and `dataSchema` (a JSON Schema for `data`) |
| `context.fixturesSchema` | | a JSON Schema: present means the agent accepts scenario fixtures (below). Declared only: the fixtures are not validated against it yet |

Everything but `label` and `context.retrieval.description` is part of the content hash, so any change of a `config`, of `otherConfig` or of
`context.retrieval.dataVersion` is a new version.

How the agent computes `dataVersion` is its business, but it must change whenever what retrieval can return changes. Recommended: the id
of the index build (every rebuild gets a new one), or a hash over (document id, content hash) of every document plus the pipeline
settings (chunking, embedding model, index parameters). Not "last updated": a deletion, or a re-index with other chunking or embeddings,
does not move it. For repeatable runs the agent serves a frozen snapshot of its data in test mode, so `dataVersion` changes only when the
snapshot is swapped on purpose.

Unknown fields are an error, like everywhere in the contracts.

**The turn.** `POST /suwageval/turn` gets `{ "conversation": [...], "fixtures": {...} }` and answers `{ "items": [...] }`, everything
the agent produced in that turn, in order (never a `UserMessage`). `fixtures` is the scenario's `fixtures`, verbatim, and is left out
when the scenario has none. Every call also carries three headers, for correlation only (the agent's traces and logs): they never
change what the agent does.

| Header | Value |
| --- | --- |
| `Suwageval-Run-Id` | the run's id (a GUID) |
| `Suwageval-Scenario` | the scenario's key |
| `Suwageval-Sample` | the sample's number, from 1 |

**Every conversation starts from the same test data.** Suwageval runs scenarios in parallel and
one run after another; an agent that carried state from one `POST /suwageval/turn` to the next
(an order refunded in an earlier run) would make a result depend on what ran before. The agent
runs each conversation against a fresh copy of its test data — the example agent gives every
turn its own copy of the fake shop.

**How Suwageval uses it.** `POST /runs` GETs the contract from the agent (`endpointUrl` of the
request, else the agent's), registers it as a version and runs against that version. The content
hash (without `label`) makes the same contract the existing version and a changed one a new
version; the answer to `POST /runs` and `suwageval run` say which version and whether it is `new`.

- An agent that cannot be reached, or whose contract cannot be read or is not valid, is a 502
  that names the URL and the cause; nothing is queued.
- The scenarios and checks must fit that version (every tool and event they name is in it, a
  tool call's `agent` and a check's `by` name an agent that has the tool, a
  retrieval needs `usesRetrieval`): otherwise 409 with the mismatches, nothing is queued.
- A scenario with `fixtures` needs a version that declares `context.fixturesSchema`; otherwise it is a mismatch too.
- A **re-check** (`POST /runs/{id}/recheck`, `suwageval recheck`) is a run that does not call the agent: it copies the stored
  samples of a finished run (output, error, undeclared, `durationMs` — the turn times are the source's, copied, not measured
  again) and judges them with the *current* checks. A check whose revision already judged the source keeps that verdict (a
  judge is not asked again); a changed or new check is evaluated on the stored output exactly as in a normal run; a removed
  check is absent. A scenario whose latest revision is not the one the source ran, or that was deleted, is skipped and listed
  in `skippedScenarios` with its reason. `recheckOf` names the source, always a run that called the agent (a re-check of a
  re-check points at the original). Unchanged verdicts are copied from the run the re-check was asked for, so a re-check of a re-check does not ask the judge again for what the earlier one already judged. `GET /runs/{id}/recheck` previews it (`possible`, `changedChecks`, `skippedScenarios`,
  `mismatches`); a re-check that would change nothing (no check changed, was added or removed — a changed pass threshold
  counts), whose scenarios are all skipped, whose checks do not fit the source's version or whose source is not `Done` is
  refused with 409. The agent's current contract plays no role. In the pass-rate trend a re-check is never a point of its own:
  the point of its source run shows the results of the latest finished re-check (`recheckedBy`).
- At the end of a run the contract is read again. If it differs from the run's version (the agent
  was redeployed mid-run) the run says `contractChangedDuringRun: true`.
- A `ToolCall` or `Event` the agent produces during a run that its version does not declare is
  recorded on the sample as `undeclared` (`tool 'x'` when no agent has it, `agent 'y'` for an agent the contract does not have, `tool 'x' of agent 'y'` for an agent without that tool, `event 'z'`) and shown in the results. It
  is a warning, not a failure by itself.

## A scenario file

| Field | | |
| --- | --- | --- |
| `key` | required | lowercase kebab-case (`refund-happy-path`), unique within the agent |
| `tags` | | lowercase kebab-case; a run picks scenarios by tag |
| `conversation` | required | the log up to the agent's turn, at least one item |
| `continuation.turns` | | how many agent turns are produced, default `1` |
| `samples` | | how many times the conversation is repeated, default `1` |
| `fixtures` | | free mapping, sent verbatim with every turn of the scenario (e.g. which documents are pinned for the conversation); shaped by the agent's `context.fixturesSchema`, which the agent must declare. Part of the scenario's content: changing it is a new revision |
| `checks` | | a list of checks, below |

### The conversation log

One item per list entry. Shorthands cover the common ones; any item can also be written in
full with `kind:`.

| Shorthand | Full form (`kind:`) |
| --- | --- |
| `- user: "text"` | `UserMessage` with `text` |
| `- assistant: "text"` | `AssistantMessage` with `text` and optionally `interrupted` (full form also `sources`: the ids of the documents the answer is grounded on, a list of strings) |
| `- tool_call: { id, name, arguments, agent }` | `ToolCall`; `arguments` is free mapping; `agent` (optional) is the LLM agent, from the contract's `agents`, that made the call |
| `- tool_result: { call_id, content, isError }` | `ToolResult` with `callId`; `isError` defaults to `false` |
| `- event: { type, data }` | `Event`; `data` is free mapping |
| | `Retrieval` with `query` and `chunks: [{ text, source, score }]` (full form only; `score` is optional, not every retriever reports one) |

Rules: a `tool_result` answers an earlier `tool_call` with the same id; an `event` has a
type declared in the agent's `context.events`; a `Retrieval` needs `usesRetrieval: true` in its
contract.

### Checks

A check tests what the agent produced in its turn(s). Common fields: `key` (kebab-case,
unique within the agent), `type`, `where` and `passThreshold` (the share of samples that must
pass, above 0 up to 1, default `1.0`). The scenario is the file the check sits in.

| `type` | Fields | Passes when |
| --- | --- | --- |
| `tool-called` | `tool`, `by` (optional), `args` (optional), `result` (optional) | the agent called the tool; `by`, when given, names the LLM agent that must have made the call (a call that names no agent never satisfies it); `args`, when given, is a subset of the call's arguments; `result`, when given, is a subset of the call's result (a value may be a `$pattern` or `$judge` matcher, below) |
| `tool-not-called` | `tool`, `by` (optional) | the agent did not call the tool; with `by`, only calls made by that LLM agent count |
| `event` | `event` (the type), `data` (optional) | the agent emitted the event; `data`, when given, is a subset of its data (a value may be a `$pattern` or `$judge` matcher, below) |
| `event-not-emitted` | `event`, `data` (optional) | no event of that type was emitted (with `data`: none whose data contains it; a `$pattern` may be used there, a `$judge` may not) |
| `cited` | `sources` (a list of ids, at least one, none empty) | every listed source appears in the `sources` of some assistant message |
| `text` | `contains`, `notContains`, `pattern` (at least one) | the answer contains the text, does not contain the text, matches the regular expression |
| `judge` | `criteria` (required, plain words) | a model judges that what the agent produced satisfies the criteria (below) |

A `tool` must be a tool of some agent (of the agent named by `by`, when given; `by` must be an agent of the contract) and an `event` one of its declared event types (checked
by `validate` against the agent's contract, and by the server when a run starts).
An unknown `type` is an error.

`where` narrows a check to a part of what was produced:

| Field | Meaning |
| --- | --- |
| `turn` | a number from 1, `last` or `any` (default `any`) |
| `after` | a tool name or event type: only what came after it counts |
| `before` | a tool name or event type: only what came before it counts |

How a check is evaluated (the server's `Checks/`; no model involved, except for `judge`, below):

- **A turn** is the items one `POST {endpointUrl}/suwageval/turn` returned, in the order the agent produced them; turns
  are numbered from 1. For now a scenario has exactly one turn.
- **`turn`** picks the turns whose items are looked at: a number, the last one, or all (`any`). A turn that was not produced
  leaves nothing to look at.
- **`after: X`** keeps the items that come after the *first* tool call named `X` or event of type `X` among the selected
  turns; **`before: X`** keeps the items before the *last* one — "after / before at least one occurrence". The anchor itself
  is never kept, and an anchor that does not occur leaves nothing. With both, what lies between. (So `tool-called refund_order
  where: {after: get_order}` needs a `get_order` call first, and `tool-not-called` after an anchor that never happened
  passes: nothing followed it.)
- **`args` / `data` / `result`** match as a *subset*: an object has every given property (extras are fine, nested objects the same
  way); an array has the same length and each element matches at its position; strings, booleans and `null` are equal;
  numbers are equal by value (`1` is `1.0`).
- **`$pattern`**: anywhere inside `args`, `data` or `result`, an object whose *only* property is `$pattern` (a .NET regular
  expression, as a string) matches an actual JSON string the expression matches (a match may be anywhere in the string; anchor
  it with `^` and `$`; `(?i)` makes it case-insensitive). It takes at most one second; a pattern that takes longer matches
  nothing. A pattern that is not a valid regular expression is an error in `validate` and when checks are pushed, reported with
  its path (`checks[0].args.zipCode.$pattern`). Written in YAML: `args: { zipCode: { $pattern: "(?i)^\\s*12345\\s*$" } }`.
  Use it for a value that is written loosely but can be checked by its form; for free text whose meaning matters use
  `$judge` (below).
- **`$judge`**: an object whose only property is `$judge` is not a literal but a matcher a model decides (below), in `args`,
  `result` or `data`. Both matchers are recognised only as the *only* property of their object; an object holding `$judge`
  next to `$pattern` (or any other property) is neither, and validation rejects it.
- **`result`** is read from the `ToolResult` whose `callId` is the matching call's `id`, searched in everything the agent
  produced (not only inside `where`). Its `content` is parsed as JSON and must contain `result` as a subset; content that is not
  JSON counts as the JSON string of itself (so `{ $pattern: "not found" }` can match a plain-text result). A call with no
  `ToolResult` never matches, and the evidence of a passing check includes the result.
- **`tool-called` / `event`** pass when any call or event in scope matches; **`event-not-emitted`** passes when none does.
  **`cited`** reads the `sources` of the assistant messages in scope: it passes when every listed source is among them, its
  evidence is the messages that cite any of the listed sources, and its message names the missing ones. **`text`** reads the assistant messages in scope,
  joined by a newline; `contains` and `notContains` are case-sensitive (use `(?i)` in `pattern` otherwise); every condition
  given must hold. An invalid `pattern` fails the check with a message.
- **The pass rate** of a check is the share of its scenario's samples that passed; the check passes the run when the rate
  reaches `passThreshold`. A sample the agent did not answer (down, timeout, unreadable answer) counts as failed for every check.

### The judge check

```yaml
- key: polite-refusal
  type: judge
  criteria: "The assistant refuses to refund an order older than 30 days and explains why, politely."
  where: { turn: last }   # optional, like for any check
```

A `judge` check is the one check that calls a model. Prefer a deterministic check whenever one can say it (a tool called,
a text contained): it is exact, free and repeatable. Use a judge for what only reading can tell — tone, whether an answer
is grounded or complete, whether a refusal was explained.

- **What the model sees:** the criteria, the conversation the agent was given, and the items in the check's scope (after
  `where`), as JSON, one item per line. It answers `{ "passed": true|false, "reason": "…" }` at temperature 0; the `reason`
  is the check's message in the results, and the items in scope are its evidence. Every sample is judged on its own, and
  `passThreshold` works as for any check.
- **Write criteria** as one or a few concrete, checkable statements about the output ("states the refund period of 14
  days", "does not promise a delivery date"), never as a vague quality ("is good"). One thing per check: two things are two
  checks. The judge judges only by what it is shown; it does not know the agent's tools or data beyond the log.
- **A judge can fail to judge.** When the model is unreachable or too slow, or its answer is not the JSON asked for, that
  sample counts as failed with a message starting `Not judged:`, and the results mark it `notJudged: true` (the web shows
  "not judged"; the CLI's summary line counts them). The run goes on and other checks are unaffected. "Not judged" is
  not "judged failed" — run again before concluding anything about the agent.
- **The model** is the server's: `Judge:Model` names one of `Models:<name>` (`BaseUrl` of an OpenAI-compatible server,
  `Model`, `MaxConcurrency` — default 8 calls at once to that model, shared by everything that calls it, and
  `TimeoutSeconds`). A run whose scenarios have a `judge` check is refused with 409 when no judge model is configured.
  `validate` checks only that `criteria` is not empty.

### The `$judge` matcher

Inside `args` and `result` (of `tool-called`) and `data` (of `event`), at any depth, an object whose **only** property is
`$judge` says "roughly like this" instead of an exact value:

```yaml
- key: refund-reason-is-damage
  type: tool-called
  tool: refund_order
  args:
    order_id: A-17                       # exact, as always
    reason:
      $judge:
        description: "Says the item arrived damaged or broken"
        examples: ["kettle arrived broken", "damaged on delivery"]
```

- **Shape.** `$judge` has `description` (a non-empty string) and/or `examples` (a non-empty list of JSON values — strings
  usually, but any JSON). At least one is required. With both, the value must satisfy the description **and** be similar in
  kind and meaning to the examples (it need not equal one). An unknown key inside `$judge` is an error, and so is `$judge`
  next to other properties in the same object (`{ $judge: …, other: 1 }`, `{ $judge: …, $pattern: … }`): a matcher is the
  only property of its object, and `$`-names are reserved for matchers. The matcher takes the place of any value, including
  a whole object or list. It is not allowed in `event-not-emitted` (a model call per event, and a verdict turned upside
  down); `tool-not-called`, `text` and `judge` checks have no `args` / `data`. `validate` and a push report a wrong shape
  or place with its path (`checks[0].args.reason.$judge`).
- **How it is evaluated.** First, without a model: every literal and `$pattern` must match as above, and a `$judge` node only
  needs a value to exist at its path (any JSON). A call or event that fails this is never sent to the model; with `by`, a call
  made by another agent is never a candidate. In `result` the value is the parsed content of the call's `ToolResult`
  (paths `result.…`); a call with no result is not a candidate. Then, for each remaining
  candidate in the order the agent produced them, ONE model request judges all the `$judge` values of that candidate together
  (input: the tool name or event type and, per value, its path, the actual JSON, the description and the examples; answer:
  `{ "results": [ { "path", "correct", "reason" } ] }` at temperature 0, exactly one entry per path given — anything else is an
  unreadable answer). The candidate matches when every value is correct; the first match stops the search and passes the
  check. The message says which value failed and the model's reason (or, on a pass, the reasons); the evidence is the matching
  call or event, and the calls or events with that name when none matched.
- **The cost.** One model call per candidate, only for candidates that passed the deterministic part. It uses the judge's
  model (below), so a run with a `$judge` matcher is refused with 409 when no judge model is configured. Prefer an exact value
  whenever it is known, `$pattern` when its form can be checked; use `$judge` for free text — a reason, a search query, a
  message to a customer.
- **It can fail to judge.** As for the `judge` check: when the model is unreachable or too slow, or its answer is not the
  JSON asked for, the sample counts as failed with a message starting `Not judged:` and `notJudged: true`. Run again before
  concluding anything about the agent.
- **Write it well.** A precise description of what the value must say, and two or three varied examples (different words,
  same meaning); one value per `$judge`.

Not checked yet: that `arguments`, `data` and `fixtures` match the JSON Schemas the agent declares.

## Regressions

A **regression** is a check that passed steadily and fails now. Nobody marks a baseline: the baseline is the check's **history**
over the newest *reference runs*.

- **Reference run.** A run is a reference run when the CLI found, when it started it, a clean working tree, a commit that is
  pushed, and a branch that is one of `regression.branches` (or the repository's default branch when none are listed). The CLI
  reads this from git in the agent's folder, or from the CI's environment (GitHub Actions, GitLab CI), and sends it with the run.
  Without git a run has no origin: it is never a reference run and is compared with every run of the agent.
- **What a run is compared with** (decided once, when it starts): the branch given with `suwageval run --ref <branch>`; else the
  run's own branch when it is one of the listed branches; else the branch a pull or merge request targets (in CI) when it is
  listed; else the first listed branch. A rerun and a re-check inherit the origin, the reference flag and the comparison of the
  run they repeat.
- **History of a check.** Of the finished runs of that branch's reference runs before this one (a re-check's verdicts replace
  those of its original), only the runs that judged *the same revision of the check on the same revision of the scenario* count.
  Their newest ones, up to twice `history`, give one verdict each: the check passed in that run or it did not.
- **The states** of a check in a run (`N` = `history`):

  | State | Means |
  | --- | --- |
  | Passed | passed (steadily, or with too little history to judge) |
  | Regression | failed now after its last `N` runs all passed |
  | New | failed now with fewer than `N` runs of history |
  | Already broken | failed now, and failing for fewer than `N` runs in a row after `N` passing ones: not caused by this run |
  | Known failure | failed now and in at least the last `N` runs |
  | Fixed | passes now after a failing streak |
  | Unstable | its history mixes passes and failures in no steady pattern |

  A check also carries the `history` its state was computed from (run, time, passed). The web shows it as a strip of marks
  (a green dot passed, a red ✗ failed; oldest first, this run last); the CLI prints it after each listed check as
  `✓✓✓✗ → ✗`: the history oldest first, an arrow, this run.

  A check also says when the agent gave no answer in its scenario (`agentFailed`): then the agent failed, not the check.
- **Exit codes** of `run --wait` (and `rerun`, `recheck`) when the results carry the comparison: 1 only when a check regressed
  or is new and failing; known failures, unstable and already broken checks leave the exit code 0. `--strict` brings back the old
  rule (1 when any check misses its threshold). 2 and 130 are unchanged.
- **An intended change of behaviour is a change of the check.** Update the check (it becomes a new revision, whose history starts
  empty, so it is *new* until `N` runs show it steady); never edit a check to hide a regression. A new revision of a scenario
  resets the history of its checks the same way.

## Results: how long a turn took

Suwageval measures the wall-clock time of every `POST /suwageval/turn` call itself, whether the agent answered or not, and stores
it on the sample: `durationMs` in `GET /runs/{id}/results` (`SampleResult`). It is absent when no call was made (a scenario with
`continuation.turns` above 1 still fails without one). The run page shows it per sample, and the median and 95th percentile
(nearest rank) of the samples that made a call.

## One complete example

`suwageval/agent.yaml`:

```yaml
name: example-agent
endpointUrl: http://localhost:7010
```

The agent at that address serves a contract like the one above, with the tools `get_order`
and `refund_order` on its `support-agent` and the event `state.changed` that this scenario uses.

`suwageval/scenarios/refund-follow-up.yaml`:

```yaml
key: refund-follow-up
tags: [regression, refunds]
conversation:
  - user: "Hi, my kettle from order A-17 arrived broken."
  - tool_call: { id: call-1, name: get_order, arguments: { order_id: A-17 }, agent: support-agent }
  - tool_result: { call_id: call-1, content: '{"status": "delivered"}' }
  - assistant: "I can refund it. Shall I go ahead?"
  - user: "Yes, please."
checks:
  - key: follow-up-refunds
    type: tool-called
    tool: refund_order
    by: support-agent
    args: { order_id: A-17 }
    where: { turn: 1, after: get_order }
  - key: follow-up-state-event
    type: event
    event: state.changed
    data: { node: refunded }
  - key: follow-up-says-refund
    type: text
    pattern: "(?i)refund"
    passThreshold: 0.8
```

A scenario with fixtures, a call checked by its result and a pattern, and the two newer checks (the agent must declare
`context.fixturesSchema` and the event `procedure.started`):

```yaml
key: delivery-to-exact-address
samples: 3
fixtures:
  pinnedResources: [shipping-policy]
conversation:
  - user: "Can you deliver order A-17 to 12 Oak St, 12345?"
checks:
  - key: address-checked-exactly
    type: tool-called
    tool: check_delivery
    by: support-agent
    args: { zipCode: { $pattern: "^\\s*12345\\s*$" } }
    result: { delivery: { checkedAddress: { $pattern: "(?i)12 Oak Street" } } }
  - key: no-procedure
    type: event-not-emitted
    event: procedure.started
  - key: plans-cited
    type: cited
    sources: [shipping-policy]
```
