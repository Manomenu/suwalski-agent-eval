---
description: Update this plugin (suwageval) to the newest one on the marketplace's master
allowed-tools: Bash(claude plugin:*)
---

Update the suwageval plugin, so Claude knows the newest commands and flags of the `suwageval` CLI. The installed copy is cached
and changes only when it is updated. Run, in order:

1. Note the installed version: the `Version:` line under `suwageval@suwalski-agent-eval` in `claude plugin list`.
2. Refresh the marketplace from GitHub (its `master`): `claude plugin marketplace update suwalski-agent-eval`.
3. Update the plugin: `claude plugin update suwageval@suwalski-agent-eval`.
4. Run `claude plugin list` again and compare the version.

If a command fails, show its error and stop (no network, no access to GitHub, or the plugin installed under another name:
`claude plugin list` shows the right `name@marketplace`, use it).

Report in at most 3 lines: the version before and after, or that it was already the newest. If it changed, tell the user to run
`/reload-plugins` (or start a new session) to load it here. Recommend once that `/plugin` → **Marketplaces** →
`suwalski-agent-eval` → **Enable auto-update** keeps it current by itself. The `suwageval` CLI is updated apart from the plugin
(`just cli-install` in the Suwageval repository).
