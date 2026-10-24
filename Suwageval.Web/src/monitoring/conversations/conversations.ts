// The monitored conversations of an agent as the Monitoring page lists them, as plain functions.

/** The page size of the conversations table. */
export const CONVERSATIONS_PAGE_SIZE = 20;

/** A conversation's state, as the server says it. */
export type ConversationState = "Open" | "Closed";

/** What the table is narrowed to: one environment, one state; null: all. */
export interface ConversationFilter {
    environment: string | null;
    state: ConversationState | null;
}

/** The path of one page of the agent's conversations, the newest last turn first, narrowed by the filter. */
export function conversationsPath(agent: string, filter: ConversationFilter, page: number, pageSize: number): string {
    const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (filter.environment !== null) query.set("environment", filter.environment);
    if (filter.state !== null) query.set("state", filter.state);
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

export const CONVERSATIONS_HELP =
    "Conversations the deployed agent sent, the one with the newest turn first. A conversation is Open while turns come and Closed after the agent's closeAfter without one; a turn after that opens it again.";
export const VERSION_COLUMN_HELP =
    "The version that answered the conversation's last turn. When an earlier turn had another version (a deploy during the conversation), it says so.";
