import type { Verdict } from "../../verdict/verdict";

export const CONVERSATIONS_PAGE_SIZE = 40;

export type ConversationState = "Open" | "Closed";

export interface ConversationFilter {
    environment: string | null;
    state: ConversationState | null;
    failedCheck: string | null;
}

export const FAILED_CHECK_PARAM = "failedCheck";

export function initialFilter(search: string): ConversationFilter {
    const failedCheck = new URLSearchParams(search).get(FAILED_CHECK_PARAM);
    return { environment: null, state: null, failedCheck: failedCheck?.trim() ? failedCheck : null };
}

interface Checked {
    checkedAt?: string | null;
    failedChecks?: number;
    notJudgedChecks?: number;
}

export function checksText(conversation: Checked): string {
    if (conversation.checkedAt == null) return "Not checked";
    const failed = conversation.failedChecks ?? 0;
    const notJudged = conversation.notJudgedChecks ?? 0;
    const parts = [failed > 0 ? `${String(failed)} failed` : null, notJudged > 0 ? `${String(notJudged)} not judged` : null];
    return parts.filter((part) => part !== null).join(", ") || "Passed";
}

export function checksVerdict(conversation: Checked): Verdict | null {
    if (conversation.checkedAt == null) return null;
    if ((conversation.failedChecks ?? 0) > 0) return "failed";
    return (conversation.notJudgedChecks ?? 0) > 0 ? "not judged" : "passed";
}

export function conversationsPath(agent: string, filter: ConversationFilter, page: number, pageSize: number): string {
    const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (filter.environment !== null) query.set("environment", filter.environment);
    if (filter.state !== null) query.set("state", filter.state);
    if (filter.failedCheck !== null) query.set("failedCheck", filter.failedCheck);
    return `/monitoring/agents/${encodeURIComponent(agent)}/conversations?${query.toString()}`;
}

export function stateOf(value: string | null): ConversationState | null {
    return value === "Open" || value === "Closed" ? value : null;
}

export function versionText(conversation: { agentVersionId: string; versionLabel?: string | null; versionChanged?: boolean }): string {
    const name = conversation.versionLabel?.trim() ? conversation.versionLabel : conversation.agentVersionId.slice(0, 8);
    return conversation.versionChanged === true ? `${name} (changed during it)` : name;
}

export type DraftCut = "end" | "user" | "agent";

export const DRAFT_CUTS: { value: DraftCut; label: string }[] = [
    { value: "end", label: "Whole conversation" },
    { value: "user", label: "Up to a user message" },
    { value: "agent", label: "Up to an agent message" },
];

export function draftCutOf(value: string | null): DraftCut {
    return value === "user" || value === "agent" ? value : "end";
}

export const MAX_DRAFT_TURNS = 20;

export function scenarioDraftPath(id: string, cut: DraftCut, ordinal: number, turns: number | null = null): string {
    const base = `/monitoring/conversations/${id}/scenario`;
    if (cut === "end") return base;
    const upTo = `${cut === "user" ? "upToUserMessage" : "upToAgentMessage"}=${String(ordinal)}`;
    return turns === null ? `${base}?${upTo}` : `${base}?${upTo}&simulate=true&turns=${String(turns)}`;
}

export function draftTurns(items: readonly { kind?: string }[], cut: Exclude<DraftCut, "end">, ordinal: number): number {
    const kind = cut === "user" ? "UserMessage" : "AssistantMessage";
    const end = items.map((item, index) => (item.kind === kind ? index : -1)).filter((index) => index >= 0)[ordinal - 1];
    if (end === undefined) return 0;
    const left = items.slice(end + 1).filter((item) => item.kind === "UserMessage").length + (cut === "user" ? 1 : 0);
    return Math.min(left, MAX_DRAFT_TURNS);
}

export const MESSAGE_PREVIEW_LENGTH = 70;

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

export const SIMULATE_REST_HELP =
    "Instead of copying only the start, simulate the rest: a model (Models:describer, else Models:fallback) reads the whole conversation and describes the customer, so the simulated customer goes for the same goal the real one did. Turns: how long the simulation runs, by default as long as the real conversation went on.";
export const NOTHING_TO_SIMULATE = "Nothing came after this message: there is no rest to simulate.";

export const SCENARIO_DRAFT_HELP =
    "The conversation copied as a scenario: whole, or up to the message you pick. The monitoring checks it failed are at the end, as comments with their definitions. Build the scenario's checks from it (Claude can), add the fixtures the agent needs (the conversation ran on real data), save the file in the agent's repository, then suwageval validate and push.";

export const CONVERSATIONS_HELP =
    "Conversations the deployed agent sent, the one with the newest turn first. A conversation is Open while turns come and Closed after the agent's closeAfter without one; a turn after that opens it again.";
export const VERSION_COLUMN_HELP =
    "The version that answered the conversation's last turn. When an earlier turn had another version (a deploy during the conversation), it says so.";
