import { type AgentError } from "./api";
// Where the agent itself gave no answer (an HTTP error, a timeout, an unreadable answer). Such a sample counts against no check, so
// the agent page lists them apart. The server finds them (`GET /agents/<name>/agent-errors`); this is what the card does with the list.

/** How many scenarios with errors a page of the agent page's card lists. */
export const AGENT_ERRORS_PAGE_SIZE = 8;

/** The longest error text a row shows; the full text is in its tooltip. */
export const AGENT_ERROR_MAX_LENGTH = 60;

export const AGENT_ERRORS_EXPLANATION =
    "Samples where the agent gave no answer (an HTTP error, a timeout, an unreadable answer). They do not count against checks; they say the agent itself is unreliable in that scenario. A scenario is listed while the newest version (among the ones the filters show) that ran it has agent errors in it: a row disappears once a newer version runs the scenario without an agent error. Archive a version to hide its rows.";

/** The path of the agent errors of an agent, decided over the given versions (none: every version not archived) and the runs on the given data versions (none: any). */
export function agentErrorsPath(agent: string, versionIds: readonly string[], dataVersions: readonly string[] = []): string {
    const query = new URLSearchParams();
    for (const id of versionIds) query.append("version", id);
    for (const data of dataVersions) query.append("dataVersion", data);
    const text = query.toString();
    return `/eval/agents/${encodeURIComponent(agent)}/agent-errors${text === "" ? "" : `?${text}`}`;
}

/** "3/12": the samples without an answer over the samples asked. */
export function errorsOfSamples(item: AgentError): string {
    return `${item.errors}/${item.samples}`;
}

/** The error text on one line, cut with an ellipsis when longer than `AGENT_ERROR_MAX_LENGTH`. */
export function shortError(text: string): string {
    const oneLine = text.split(/\s+/).filter(Boolean).join(" ");
    return oneLine.length <= AGENT_ERROR_MAX_LENGTH ? oneLine : `${oneLine.slice(0, AGENT_ERROR_MAX_LENGTH - 1)}…`;
}
