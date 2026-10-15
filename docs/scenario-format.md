# The scenario files (format v0)

What an agent's repository keeps for Suwageval, and what `suwageval validate` checks. The
files are YAML in a folder `suwageval/` at the root of the agent's repository (`--dir` or the
`path` argument points elsewhere):

```
suwageval/
  agent.yaml            the agent and the version of it under test
  scenarios/
    refund-happy-path.yaml   one scenario per file, its checks inside
    ...
```

The fields are the records of `Suwageval.Contracts` in camelCase. Unknown fields, missing
required fields and wrong values are errors, reported with file, line and path. `validate`
needs no server:

```sh
suwageval validate                      # ./suwageval
suwageval validate path/to/suwageval    # another folder
suwageval validate scenarios/x.yaml     # one scenario, checked against ./suwageval/agent.yaml (--dir for another)
suwageval validate - < scenario.yaml    # one scenario from stdin, same
suwageval validate --json               # { "valid": bool, "problems": [{ "file", "path", "message" }] }
```

Exit code 0 when valid, 1 when anything is wrong. A file that does not match the contract is
reported for its structure first; what the files say about each other (keys, tools, events) is
checked once its structure is right.

## `agent.yaml`

The agent (`name`, `endpointUrl`) and, under `version:`, the version the scenarios run against.

| Field | | |
| --- | --- | --- |
| `name` | required | the agent's name |
| `endpointUrl` | required | where the agent is reached: `POST {endpointUrl}/suwageval/turn` |
| `version.label` | | a name for this version, e.g. `v3-shorter-prompt` |
| `version.systemPrompt` | required | the agent's system prompt |
| `version.model` | required | `name` (required) and `parameters` (free mapping, e.g. `temperature`) |
| `version.tools` | | the tools the agent may call: `name`, `description`, `parameters` (a JSON Schema) |
| `version.context.usesRetrieval` | | `true` if the agent retrieves chunks (RAG); default `false` |
| `version.context.events` | | the events the agent emits: `type` and `dataSchema` (a JSON Schema for `data`) |

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
type declared in `context.events`; a `Retrieval` needs `usesRetrieval: true`.

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

A `tool` must be one of the version's tools and an `event` one of its declared event types.
Other types (a judge) come after the MVP; an unknown `type` is an error.

`where` narrows a check to a part of what was produced:

| Field | Meaning |
| --- | --- |
| `turn` | a number from 1, `last` or `any` (default `any`) |
| `after` | a tool name or event type: only what came after it counts |
| `before` | a tool name or event type: only what came before it counts |

Not checked yet: that `arguments` and `data` match the JSON Schemas the agent declares.

## One complete example

`suwageval/agent.yaml`:

```yaml
name: example-agent
endpointUrl: http://localhost:7010

version:
  model:
    name: gpt-4o-mini
  systemPrompt: |
    You are a polite customer support agent for Kettle & Co.
    Look an order up with get_order before you refund it.
  tools:
    - name: get_order
      description: "Look up an order: its status, items and total."
      parameters:
        type: object
        properties:
          order_id: { type: string }
        required: [order_id]
    - name: refund_order
      description: Refund an order. Only a delivered order can be refunded.
      parameters:
        type: object
        properties:
          order_id: { type: string }
          reason: { type: string }
        required: [order_id, reason]
  context:
    usesRetrieval: false
    events:
      - type: state.changed
        dataSchema:
          type: object
          properties:
            node: { type: string }
            orderId: { type: string }
          required: [node, orderId]
```

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
