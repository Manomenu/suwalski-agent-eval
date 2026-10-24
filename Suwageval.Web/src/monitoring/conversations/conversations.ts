// The monitored conversations of an agent as the Monitoring page lists them, as plain functions.

/** The page size of the conversations table. */
export const CONVERSATIONS_PAGE_SIZE = 20;

/** A conversation's state, as the server says it. */
export type ConversationState = "Open" | "Closed";

/** What the table is narrowed to: one environment, one state, the failures of one monitoring check (its key); null: all. */
export interface ConversationFilter {
    environment: string | null;
    state: ConversationState | null;
    failedCheck: string | null;
}

/** The query parameter a link to the failing conversations of a check sets (`failedConversationsHref`). */
export const FAILED_CHECK_PARAM = "failedCheck";

/** The filter a page opens with: the failed check from the address, nothing else. */
export function initialFilter(search: string): ConversationFilter {
    const failedCheck = new URLSearchParams(search).get(FAILED_CHECK_PARAM);
    return { environment: null, state: null, failedCheck: failedCheck?.trim() ? failedCheck : null };
}

/** A conversation's checks as the Checks column reads them. */
interface Checked {
    checkedAt?: string | null;
    failedChecks?: number;
    notJudgedChecks?: number;
}

/** What the Checks column says of a conversation: not checked yet, passed, or how many checks failed and were not judged. */
export function checksText(conversation: Checked): string {
    if (conversation.checkedAt == null) return "Not checked";
    const failed = conversation.failedChecks ?? 0;
    const notJudged = conversation.notJudgedChecks ?? 0;
    const parts = [failed > 0 ? `${String(failed)} failed` : null, notJudged > 0 ? `${String(notJudged)} not judged` : null];
    return parts.filter((part) => part !== null).join(", ") || "Passed";
}

/** The colour of the Checks column: red for a failure, yellow for checks not judged, green when all passed, grey when not checked. */
export function checksColor(conversation: Checked): "red" | "yellow" | "green" | "gray" {
    if (conversation.checkedAt == null) return "gray";
    if ((conversation.failedChecks ?? 0) > 0) return "red";
    return (conversation.notJudgedChecks ?? 0) > 0 ? "yellow" : "green";
}

/** The path of one page of the agent's conversations, the newest last turn first, narrowed by the filter. */
export function conversationsPath(agent: string, filter: ConversationFilter, page: number, pageSize: number): string {
    const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (filter.environment !== null) query.set("environment", filter.environment);
    if (filter.state !== null) query.set("state", filter.state);
    if (filter.failedCheck !== null) query.set("failedCheck", filter.failedCheck);
    return `/monitoring/agents/${encodeURIComponent(agent)}/conversations?${query.toString()}`;
}

/** The value of the state filter's field, read back: anything else is no filter. */
export function stateOf(value: string | null): ConversationState | null {
    return value === "Open" || value === "Closed" ? value : null;
}

/** What the column of a conversation's version says: the label or the start of the id, and a mark when a deploy came during it. */
export function versionText(conversation: { agentVersionId: string; versionLabel?: string | null; versionChanged?: boolean }): string {
    const name = conversation.versionLabel?.trim() ? conversation.versionLabel : conversation.agentVersionId.slice(0, 8);
    return conversation.versionChanged === true ? `${name} (changed during it)` : name;
}

/** The path of the conversation's scenario draft: taken as it is (null), or replayed to a turn and simulated after it. */
export function scenarioDraftPath(id: string, untilTurn: number | null): string {
    return `/monitoring/conversations/${id}/scenario${untilTurn === null ? "" : `?untilTurn=${String(untilTurn)}`}`;
}

/** What the draft's "Replay" field offers: the conversation as it is, then each turn before the last to simulate after. */
export function replayOptions(turns: number): { value: string; label: string }[] {
    return [
        { value: "", label: "As it is: the agent answers the last message" },
        ...Array.from({ length: Math.max(turns - 1, 0) }, (_, index) => ({
            value: String(index + 1),
            label: `To turn ${String(index + 1)}, then a simulated user`,
        })),
    ];
}

/** The turn the "Replay" field's value names; null: as it is. */
export function untilTurnOf(value: string | null): number | null {
    const turn = Number(value);
    return value === null || value === "" || !Number.isInteger(turn) || turn < 1 ? null : turn;
}

export const SIMULATED_DRAFT_HELP =
    "Replayed to the chosen turn: the agent answers its message, then Suwageval's model plays the user for as many turns as the real conversation went on. The simulated user is described from the real one: review it before you save the file.";

export const SCENARIO_DRAFT_HELP =
    "Taken as it is: every turn before the last is the history, and the agent answers the last user message. The checks at the end are drafts, as comments, from what the agent did: keep what it must do, delete the rest. Add the fixtures the agent needs (the conversation ran on real data). Save the file in the agent's repository, then suwageval validate and push.";

export const CONVERSATIONS_HELP =
    "Conversations the deployed agent sent, the one with the newest turn first. A conversation is Open while turns come and Closed after the agent's closeAfter without one; a turn after that opens it again.";
export const VERSION_COLUMN_HELP =
    "The version that answered the conversation's last turn. When an earlier turn had another version (a deploy during the conversation), it says so.";
