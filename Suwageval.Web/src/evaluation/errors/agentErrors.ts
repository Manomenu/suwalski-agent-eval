import { type AgentError } from "./api";

export const AGENT_ERRORS_PAGE_SIZE = 8;

export const AGENT_ERROR_MAX_LENGTH = 60;

export const AGENT_ERRORS_EXPLANATION =
    "Samples where the agent gave no answer (an HTTP error, a timeout, an unreadable answer). They do not count against checks; they say the agent itself is unreliable in that scenario. A scenario is listed while the newest version (among the ones the filters show) that ran it has agent errors in it: a row disappears once a newer version runs the scenario without an agent error. Archive a version to hide its rows.";

export function agentErrorsPath(agent: string, versionIds: readonly string[], dataVersions: readonly string[] = []): string {
    const query = new URLSearchParams();
    for (const id of versionIds) query.append("version", id);
    for (const data of dataVersions) query.append("dataVersion", data);
    const text = query.toString();
    return `/eval/agents/${encodeURIComponent(agent)}/agent-errors${text === "" ? "" : `?${text}`}`;
}

export function errorsOfSamples(item: AgentError): string {
    return `${item.errors}/${item.samples}`;
}

export function shortError(text: string): string {
    const oneLine = text.split(/\s+/).filter(Boolean).join(" ");
    return oneLine.length <= AGENT_ERROR_MAX_LENGTH ? oneLine : `${oneLine.slice(0, AGENT_ERROR_MAX_LENGTH - 1)}…`;
}
