// The monitored conversations of an agent as the Monitoring page lists them, as plain functions.
import type { Verdict } from "../../verdict/verdict";

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

/** The verdict the Checks column shows: failed on a failure, not judged when some check was not, passed when all were; null: not checked. */
export function checksVerdict(conversation: Checked): Verdict | null {
    if (conversation.checkedAt == null) return null;
    if ((conversation.failedChecks ?? 0) > 0) return "failed";
    return (conversation.notJudgedChecks ?? 0) > 0 ? "not judged" : "passed";
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

/** Where the copy of a conversation ends: at its end, or at a chosen user or agent message (included). */
export type DraftCut = "end" | "user" | "agent";

export const DRAFT_CUTS: { value: DraftCut; label: string }[] = [
    { value: "end", label: "Whole conversation" },
    { value: "user", label: "Up to a user message" },
    { value: "agent", label: "Up to an agent message" },
];

/** The cut the field's value names; anything else is the whole conversation. */
export function draftCutOf(value: string | null): DraftCut {
    return value === "user" || value === "agent" ? value : "end";
}

/** The path of the conversation's scenario draft: to its end, or up to its `ordinal`-th (from 1) user or agent message. */
export function scenarioDraftPath(id: string, cut: DraftCut, ordinal: number): string {
    const base = `/monitoring/conversations/${id}/scenario`;
    if (cut === "end") return base;
    return `${base}?${cut === "user" ? "upToUserMessage" : "upToAgentMessage"}=${String(ordinal)}`;
}

/** How much of a message the picker shows. */
export const MESSAGE_PREVIEW_LENGTH = 70;

/** The user's or the agent's messages of the conversation to pick from, numbered from 1, each with the start of its text. */
export function messageOptions(
    items: readonly { kind?: string; text?: string }[],
    cut: Exclude<DraftCut, "end">,
): { value: string; label: string }[] {
    const kind = cut === "user" ? "UserMessage" : "AssistantMessage";
    return items
        .filter((item) => item.kind === kind)
        .map((item, index) => {
            const text = (item.text ?? "").replace(/\s+/g, " ").trim();
            const preview = text.length > MESSAGE_PREVIEW_LENGTH ? `${text.slice(0, MESSAGE_PREVIEW_LENGTH)}…` : text;
            return { value: String(index + 1), label: `${String(index + 1)}. ${preview}` };
        });
}

export const SCENARIO_DRAFT_HELP =
    "The conversation copied as a scenario: whole, or up to the message you pick. The monitoring checks it failed are at the end, as comments with their definitions. Build the scenario's checks from it (Claude can), add the fixtures the agent needs (the conversation ran on real data), save the file in the agent's repository, then suwageval validate and push.";

export const CONVERSATIONS_HELP =
    "Conversations the deployed agent sent, the one with the newest turn first. A conversation is Open while turns come and Closed after the agent's closeAfter without one; a turn after that opens it again.";
export const VERSION_COLUMN_HELP =
    "The version that answered the conversation's last turn. When an earlier turn had another version (a deploy during the conversation), it says so.";
