# Architecture and stack

How the parts fit together; the rules for changing them (layers, import contracts, layout) are in
[AGENTS.md](../AGENTS.md).

## Stack

- **Server:** .NET 10, ASP.NET Core minimal APIs, PostgreSQL through Npgsql and Dapper (SQL
  written by hand, numbered migrations), OpenAPI document the web app is typed from; Microsoft Agent Framework for the
  judge today, the user simulator after the MVP.
- **Web:** React 19, Mantine, Vite, TypeScript strict.
- **Around them:** PostgreSQL 17, a container per part, compose for the whole stack on a
  laptop, a Helm chart with a post-deploy smoke test for the cluster.


## Parts

- **`Suwageval.Server`** — the one HTTP API (the web app and the CLI both use it; there is no MCP endpoint).
- **`Suwageval.Contracts`** — the records the API sends and receives, shared by the server and the CLI, so the file format and the API cannot drift apart.
- **`Suwageval.Cli`** — the `suwageval` command; reads the agent's YAML files and drives the API.
- **`Suwageval.Web`** — the web app, typed from the server's OpenAPI document.
- **The agent under test** — reached over HTTP through its two routes (contract v1, [scenario-format.md](scenario-format.md)); it is never part of this repository's deployment.

Which folder holds what, the import contracts between the layers and the three ways to run the stack
(host, containers, cluster) are in [AGENTS.md](../AGENTS.md), sections 1, 2 and 4. The user-facing
behaviour is in [front-doors.md](front-doors.md).
