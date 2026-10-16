# The scenario files (format v0)

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

Only the agent's name and where it runs. **What the agent is** — prompt, model, tools, events —
is not written in any file: the running agent serves it (next section), so a run always records
what really ran. A `version:` key is an error.

| Field | | |
| --- | --- | --- |
| `name` | required | the agent's name |
| `endpointUrl` | required | where the agent is reached: `POST {endpointUrl}/suwageval/turn`, `GET {endpointUrl}/suwageval/contract` |

## The agent contract (contract v0)

An agent under test serves two routes. `POST /suwageval/turn` runs a turn (the conversation in,
the items it produced out). `GET /suwageval/contract` says what the agent is:

```json
{
  "label": "3f2a9c1",
  "systemPrompt": "You are a polite customer support agent ...",
  "model": { "name": "gpt-4o-mini", "parameters": { "temperature": 0 } },
  "tools": [
    {
      "name": "get_order",
      "description": "Look up an order: its status, items and total.",
      "parameters": { "type": "object", "properties": { "order_id": { "type": "string" } }, "required": ["order_id"] }
    }
  ],
  "context": {
    "usesRetrieval": false,
    "events": [{ "type": "state.changed", "dataSchema": { "type": "object" } }]
  }
}
```

| Field | | |
| --- | --- | --- |
| `label` | | a name for this version, e.g. the git SHA; no part of the content: renaming alone is not a new version |
| `systemPrompt` | required | the agent's system prompt |
| `model` | required | `name` (required) and `parameters` (free mapping, e.g. `temperature`) |
| `tools` | | the tools the agent may call: `name`, `description`, `parameters` (a JSON Schema) |
| `context.usesRetrieval` | | `true` if the agent retrieves chunks (RAG); default `false` |
| `context.events` | | the events the agent emits: `type` and `dataSchema` (a JSON Schema for `data`) |

Unknown fields are an error, like everywhere in the contracts.

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
  retrieval needs `usesRetrieval`): otherwise 409 with the mismatches, nothing is queued.
- At the end of a run the contract is read again. If it differs from the run's version (the agent
  was redeployed mid-run) the run says `contractChangedDuringRun: true`.
- A `ToolCall` or `Event` the agent produces during a run that its version does not declare is
  recorded on the sample as `undeclared` (`tool 'x'`, `event 'y'`) and shown in the results. It
  is a warning, not a failure by itself.

## A scenario file

| Field | | |
| --- | --- | --- |
| `key` | required | lowercase kebab-case (`refund-happy-path`), unique within the agent |
| `tags` | | lowercase kebab-case; a run picks scenarios by tag |
| `conversation` | required | the log up to the agent's turn, at least one item |
| `continuation.turns` | | how many agent turns are produced, default `1` |
| `samples` | | how many times the conversation is repeated, default `1` |
| `checks` | | a list of checks, below |

### The conversation log

One item per list entry. Shorthands cover the common ones; any item can also be written in
full with `kind:`.

| Shorthand | Full form (`kind:`) |
| --- | --- |
| `- user: "text"` | `UserMessage` with `text` |
| `- assistant: "text"` | `AssistantMessage` with `text` and optionally `interrupted` |
| `- tool_call: { id, name, arguments }` | `ToolCall`; `arguments` is free mapping |
| `- tool_result: { call_id, content, isError }` | `ToolResult` with `callId`; `isError` defaults to `false` |
| `- event: { type, data }` | `Event`; `data` is free mapping |
| | `Retrieval` with `query` and `chunks: [{ text, source, score }]` (full form only) |

Rules: a `tool_result` answers an earlier `tool_call` with the same id; an `event` has a
type declared in the agent's `context.events`; a `Retrieval` needs `usesRetrieval: true` in its
contract.

### Checks

A check tests what the agent produced in its turn(s). Common fields: `key` (kebab-case,
unique within the agent), `type`, `where` and `passThreshold` (the share of samples that must
pass, above 0 up to 1, default `1.0`). The scenario is the file the check sits in.

| `type` | Fields | Passes when |
| --- | --- | --- |
| `tool-called` | `tool`, `args` (optional) | the agent called the tool; `args`, when given, is a subset of the call's arguments |
| `tool-not-called` | `tool` | the agent did not call the tool |
| `event` | `event` (the type), `data` (optional) | the agent emitted the event; `data`, when given, is a subset of its data |
| `text` | `contains`, `notContains`, `pattern` (at least one) | the answer contains the text, does not contain the text, matches the regular expression |
| `judge` | `criteria` (required, plain words) | a model judges that what the agent produced satisfies the criteria (below) |

A `tool` must be one of the agent's tools and an `event` one of its declared event types (checked
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
- **`args` / `data`** match as a *subset*: an object has every given property (extras are fine, nested objects the same
  way); an array has the same length and each element matches at its position; strings, booleans and `null` are equal;
  numbers are equal by value (`1` is `1.0`).
- **`tool-called` / `event`** pass when any call or event in scope matches. **`text`** reads the assistant messages in scope,
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

Not checked yet: that `arguments` and `data` match the JSON Schemas the agent declares.

## One complete example

`suwageval/agent.yaml`:

```yaml
name: example-agent
endpointUrl: http://localhost:7010
```

The agent at that address serves the contract of the section above, with the tools `get_order`
and `refund_order` and the event `state.changed` that this scenario uses.

`suwageval/scenarios/refund-follow-up.yaml`:

```yaml
key: refund-follow-up
tags: [regression, refunds]
conversation:
  - user: "Hi, my kettle from order A-17 arrived broken."
  - tool_call: { id: call-1, name: get_order, arguments: { order_id: A-17 } }
  - tool_result: { call_id: call-1, content: '{"status": "delivered"}' }
  - assistant: "I can refund it. Shall I go ahead?"
  - user: "Yes, please."
checks:
  - key: follow-up-refunds
    type: tool-called
    tool: refund_order
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
