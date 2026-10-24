# Why the contract and the checks look the way they do (contract v1)

This note explains what branch `more-universal-approach` added to Suwageval, why each element
was added, and why it has the shape it has. The first real system to adopt Suwageval drove the
change: a conversational backend made of several LLM agents and a state machine. Its own E2E
suite had to answer questions Suwageval could not answer yet. Every element below replaced
something that suite did with a workaround: reading the backend's database, a hidden
request header, parsing stdout. The rule throughout: **a check may only rest on what the agent
under test reports in its answer, and the scenario file shows everything the agent was given.**

The format itself is in [scenario-format.md](scenario-format.md). This file is the reasoning.

## The contract: what the system is

### `agents[]` replaces `systemPrompt` / `model` / `tools` (contract v1)

A system under test is often several LLM agents, not one prompt: a router, a query rewriter,
the main chat agent and so on. v0 forced all of that into one `systemPrompt`. That leaves two bad
options. Gluing several prompts into one string describes a system that does not exist.
Describing only the main agent hides the others, and then a change to one of them makes no new version. Each agent now has its own
`name`, `model`, `config` and `tools`. That way:

- A version records what really ran, and a change to any agent's prompt or model is a new
  version (the content hash covers all of it).
- Tools belong to the agent that may call them. This is what makes `by:` (below) checkable.

`config` is **free text**, not a structure. Real systems define an agent with very different
things (instructions, a scope policy, phases, YAML of their own). Structure would have to guess
which parts checks or comparisons need. Free text is enough for the hash and for a human
comparing versions. Structure comes once two systems show what is common.

**No compatibility with v0.** There was one agent using v0 (the example agent) and no stored
data worth keeping. Supporting both shapes would double every reader of the contract for nothing.

### `otherConfig`

Some behaviour belongs to the system, not to one agent: the state machine's step limit, which
tools are switched on right now, a retry policy. If that is left out of the contract, two runs with
the same prompts but a different setting look like the same version. That is a false comparison. It
is free text for the same reason as `config`. The data the answers are grounded on is not free text:
it has its own structured field, `context.retrieval.dataVersion` (below).

### `context.retrieval.dataVersion`

A changed knowledge base leaves the prompts, the models and the tools as they were, so without a
field of its own two runs of "the same version" would be compared over different data. Suwageval
does not store the data, only a fingerprint the agent computes. With `usesRetrieval: true` the
contract therefore requires `context.retrieval`.

- **Why structured, not in `otherConfig`.** A version's data has to be filtered ("only runs over
  this data") and compared like a model. That needs one agreed field, not a sentence to read.
- **`dataVersion` is outside the hash, kept on each run.** A version is the agent's behaviour
  (prompts, models, tools, configuration); new data is not a new version. Earlier contract versions
  hashed it, which works for evaluation (a frozen snapshot) but not for live traffic, where the
  knowledge base changes all the time: every index update would split the charts into versions of a
  few conversations each. Two definitions of a version (with data in evaluation, without it in
  monitoring) were rejected: "v7" would mean another thing in each area. So each run keeps the data
  version it started with: runs are filtered by it, the comparison shows it among what differs.
  It is an opaque string: how the agent computes it is its business, but it must change whenever
  what retrieval can return changes: the content AND how it was indexed. Recommended is the id of the index build, or a hash
  over (document id, content hash) of every document plus the pipeline settings (chunking,
  embedding model, index parameters).
- **`description` is outside the hash**, like `label`, and kept on the run beside the data version.
  It is for people (how many documents, what kind).
- **"Last updated" is not a valid fingerprint.** A deleted document or a re-index with other
  chunking or embeddings does not move it, yet changes what retrieval returns.
- **A frozen snapshot for evaluation.** For repeatable runs the agent serves a snapshot of its data
  in test mode (the "same test data" rule of the contract), so `dataVersion` changes only when the
  snapshot is swapped on purpose.

### `context.fixturesSchema`

The schema says that the agent accepts scenario fixtures (below), and what shape it expects. It
is declared in the contract, like event types, so that `validate` and the 409 at `POST /eval/runs`
can refuse a scenario with fixtures for an agent that would silently ignore them. It is **not
validated** against yet, just like `dataSchema` of events. JSON Schema validation needs a package
and stays in TODO.md "Later".

### `label` stays out of the hash

Unchanged from v0, repeated because the multi-agent contract makes it matter more. A label (an
image tag, a git SHA) names a version but is not what the version is. Rebuilding the same code
under a new tag must not split the results into two versions.

## The conversation log: what the agent did

### `ToolCall.agent`

Once tools belong to agents, the log must say which agent made a call. Otherwise "the right tool
called by the wrong agent" cannot be told from "the right tool called by the right agent". The
field is **optional**:

- a hand-written scenario log stays short;
- a single-agent system need not repeat its only agent on every call;
- a call without an agent never satisfies a `by:` check, so leaving it out cannot make a check
  pass.

### `AssistantMessage.sources`

Many agents ground an answer on documents and show them to the user (the articles each reply is
based on). Before this field, the only way to check "the answer used
article X" was an event invented for it. Worse, event data is matched by position inside arrays,
so "contains X" could not be said at all. Sources are a property of the answer itself, so they
sit on the message.

### `RetrievedChunk.score` is optional

Not every retriever reports a score: a pinned retrieval or a title lookup has none. A required
score would push agents to send a made-up number. A missing value is honest, and a made-up one
would look like data.

## The scenario: what the agent was given

### `fixtures`

A scenario sometimes has to fix something the agent normally decides for itself. The main case
is retrieval: pinning which documents the retriever "finds", so that a scenario measures the
model's choice and not the vector store's ranking. Doing this with a request header the
scenario file does not show hides part of the input. Fixtures are part of the scenario (and of its content hash), go
to the agent in the turn request, and are visible to anyone reading the file or the run. Their
content is the agent's business (`fixturesSchema`). Suwageval only carries them.

### No scripted user messages

A conversation of several real steps is **not** written as one scenario with fixed user messages
after each agent answer. A user message written before the agent's real answer is known may not
fit that answer, and then the scenario measures noise. Instead such a conversation is split into
several scenarios. Each one predefines the earlier agent answers, recorded from a real run, and
asks for exactly one new turn. More than one agent turn is reserved for a user played by an LLM,
which is a different kind of evaluation (TODO.md).

## The checks: what is asked of the answer

### `by:` on `tool-called` / `tool-not-called`

This follows from `agents[]`. A multi-agent system can be wrong in a way a single agent cannot:
the right action taken by an agent that should not take it. `validate` and the 409 check that
the named agent exists and has the tool, so a typo fails before any call is made.

### `event-not-emitted`

The negative of `event`, as `tool-not-called` is the negative of `tool-called`. Without it,
"no procedure was started" could only be checked
through a tool that happens to start procedures. That couples the check to one way of starting
them.

### `cited`

Checks `sources` (above). It needs no JSON matching rules: a list of ids, each of which must
appear. With no `where`, it looks at the whole answer.

### `result` on `tool-called`

Some arguments are only right in the light of what the tool made of them: e.g. that the address
the model passed was normalised by the service to the expected one. Without `result` this needs
a special case for one tool inside a "generic" verifier. `result` matches the tool
result the agent itself reported, so any agent can use it and no tool is special. It searches
the whole log, not only the check's scope, because a result belongs to its call wherever it
appears.

### `$pattern` inside `args`, `data`, `result`

Exact equality is too strict for values a model writes ("12 Oak Street" vs "12 Oak St",
case, spaces). A whole check type per comparison would multiply types. One matcher, usable at any
depth of the expected JSON, covers case-insensitive, trimmed and any-of matching with one regex.
The `$` prefix and the "only property" rule keep it from colliding with a real argument named
`pattern`. An invalid regex is a validation error, never a silent "no match".

### `$pattern` and `$judge` side by side

`$judge` (a model decides a free-text value) arrived from `master` with the same "only property" rule, so the two
matchers never disagree: an object holding both, or either one next to another property, is neither, and validation
rejects it when `$judge` is among the keys. `$judge` works wherever `$pattern` does (`args`, `data`, and `result`, where the
value is the parsed content of the call's result), except in `event-not-emitted`: "no event whose data a model would
accept" needs a model call per event and turns the verdict upside down, so it is a validation error. With `by`, a call by
another agent is never a candidate for the model. Use `$pattern` for a value that is loosely written but checkable by its
form, `$judge` for free text whose meaning matters.

## The run: how it is observed

### Correlation: the `baggage` header, and a `conversationId` always

The agent's traces (Langfuse, OpenTelemetry) are where a failing sample is understood. Without a
shared id, finding the trace of one sample means matching timestamps. Where a turn comes from (run, scenario, its type and tags, the
run's tags and group, the sample) goes in **one standard header, W3C `baggage`**, instead of a custom header per value: OpenTelemetry
tooling already reads it, a new value is a new key and not a new header, and no agent has to learn a private format. Keys are
namespaced `suwageval.*` so they never clash with the agent's own baggage; values are percent-encoded, lists comma-separated inside
one value. It is **correlation only**: an agent must not change behaviour on it. That is why everything that does change behaviour
(fixtures) is in the body. The price of the standard is that a propagator may forward `baggage` to third parties; the contract warns
about it and says how to stop it.

`conversationId` is always sent, a new one per sample, because a trace system's session is the conversation: the agent can use the
same session id it uses in production, and one sample is one session. For a plain scenario that costs nothing: its single turn is the
first turn of a fresh id, which is the fresh state the contract already demanded.

### `durationMs` per sample

How long a turn takes is part of whether a model setup is usable. Model setups are compared on
median and p95 turn time. Suwageval measures it itself, around the call, instead of asking the
agent to report it: the number then means the same for every agent, and an agent cannot
under-report it. The web shows median and p95 per run.

## The migrations

- `0008_contract_v1.sql` deletes runs, their results and the stored agent versions. Those hold v0
  JSON that v1 cannot read, and none of it was worth migrating. Agents, scenarios and checks stay.
- `0009_sample_duration.sql` adds the nullable duration column. Null means no call was made, not
  zero.
- `0010_agent_version_data_description.sql` adds the nullable column that keeps `context.retrieval.description`.
  The description is outside the content hash, so (like the label) it is stored beside the content, not in it.

## Left out on purpose

| Not added | Why |
| --- | --- |
| Multi-turn with a simulated user | A different kind of evaluation, not needed by regression scenarios. Built since: "Simulation" in scenario-format.md; scripted user messages are still left out, for the reason above. |
| `where: { state: … }` | Part of the v1 design, but no scenario needs it yet (AGENTS.md §0). Kept in TODO.md. |
| Informational checks that never fail the run | One use (measured-only forbidden tools), and a pass rate per check already measures them. |
| Token usage in the turn answer | No check or view reads it yet. |

The judge (`judge` checks) and the comparison of runs were built on `master` while this change was
open, and arrived with the merge: the comparison shows each version's models (`modelNames`, one
per agent), its data version and the turn time beside the pass rates.
