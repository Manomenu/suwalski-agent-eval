---
name: suwageval
description: Set up and drive Suwageval, the evaluation server for LLM agents, through its suwageval CLI. Use when the user wants to evaluate or test an LLM agent with Suwageval, write or fix scenarios and checks, validate them, run them, or find out why a scenario failed, or asks to install or troubleshoot the suwageval CLI.
---

# Suwageval

Suwageval replays a recorded conversation against an LLM agent, lets the agent answer the last
message, and checks that answer. You drive it through the `suwageval` CLI, run through Bash.
Source and docs: https://github.com/Manomenu/suwalski-agent-eval

## First: is it installed?

```bash
command -v suwageval && suwageval --help
```

`command not found`: the CLI is not installed. Go to **Setup**.

## Setup

Needs the .NET 10 SDK, git and `just`. Give the user the lines to run, prefixed with `!` so they
run in this session's terminal:

```
! git clone https://github.com/Manomenu/suwalski-agent-eval ~/repos/suwalski-agent-eval
! cd ~/repos/suwalski-agent-eval && just cli-install
```

The CLI talks to the Suwageval server through the address of its **web app**; the API is under
its `/api`. The address is `SUWAGEVAL_URL`, or `--server` on a command; the default is
`http://localhost:3230` (a laptop running `just web` in the clone). `validate` needs no server.
`push` and `run` need the server, and `run` needs the agent itself running too.

## The workflow

- **Scenario files are code in the agent's repository**, in `suwageval/`: `agent.yaml` and
  `scenarios/*.yaml`. Git versions them together with the agent. Never edit them on the server:
  change the file, then `push`.
- **Read before writing.** Read the existing files in `suwageval/` and copy their style. The
  format (agent, scenarios, checks, shorthands, one complete example) is specified at
  https://raw.githubusercontent.com/Manomenu/suwalski-agent-eval/master/docs/scenario-format.md
  — fetch it when you write or change a scenario; do not rely on memory.
- **Never invent tool or event names.** Take them from the agent's contract: `GET
  {endpointUrl}/suwageval/contract` (the `endpointUrl` is in `agent.yaml`) lists its tools with
  argument schemas and its events, or read them from `suwageval validate` output.
- **After every change to the agent**, run `suwageval validate`: it lists the scenarios that no
  longer fit the agent's contract (a renamed tool, a removed event). Fix those files.
- A version is never written in a file: the running agent serves it, and `run` records what
  really ran.

## Commands

| want | command |
| --- | --- |
| check the files and the names against the agent's contract | `suwageval validate [dir]` (`--json`, `--offline` to skip the agent, `--endpoint URL`) |
| send the agent, scenarios and checks to the server | `suwageval push [dir]` |
| run the scenarios and wait | `suwageval run --wait --json` (`--tag <tag>` for some scenarios only) |
| open a run on the web | `suwageval results <runId>` |
| a run's results as JSON | `suwageval results <runId> --json` |
| list the agent's versions | `suwageval versions` (`--archived` to include archived ones, `--json`) |
| archive or restore versions | `suwageval versions archive <versionId>...`, `suwageval versions unarchive <versionId>...` |

A run's page is `{SUWAGEVAL_URL}/runs/{runId}`.

### Exit codes

| code | `validate` | `run --wait`, `results --json` |
| --- | --- | --- |
| 0 | the files are valid | every check passed its threshold |
| 1 | something is wrong | a check failed |
| 2 | — | the run could not be started or finished |

`validate` exits 0 with a notice when the agent was unreachable: the names were then not
checked (`namesChecked: false` in `--json`). Say so; do not call the scenarios verified.

## Versions

Every change of the agent's contract makes a new version, so the list grows. Versions are never
deleted (every run points at one): they are **archived**, hidden by default and restorable. A new
run on a contract that matches an archived version unarchives it. Archive only what the user
asked for or confirmed: show the ids first. `/suwageval:versions` does this.

## Reading results

`run --wait --json` prints `{ run, scenarios }`. `run` has the agent, `agentVersionId`, `status`,
`error` and the totals (`checksPassed`, `checksFailed`). Each scenario has `key` and its
`checks`; a check has `key`, `passed`, `passRate`, `passThreshold` and per-sample `message` and
`evidence` — the message says why it failed. A scenario sample with an `error` means the agent
itself failed on that turn, which is not a check failing: say which.

## Rules

- **Push and run only when asked** (or when the command you were given says so): a push writes
  to the server.
- **Do not delete or weaken a check to make it pass.** If its intent no longer fits the agent,
  say so and let the user decide.
- **Do not change the agent to make a scenario pass** unless the user asked for that.
