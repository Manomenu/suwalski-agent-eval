import { request } from "../../api/client";
import type { components } from "../../api/openapi";
import { conversationsPath, scenarioDraftPath, type ConversationFilter, type DraftCut } from "./conversations";

export type ConversationList = components["schemas"]["ConversationList"];
export type ConversationDetail = components["schemas"]["ConversationDetail"];
export type ScenarioDraft = components["schemas"]["ScenarioDraft"];

export function fetchConversations(
    agent: string,
    filter: ConversationFilter,
    page: number,
    pageSize: number,
    signal: AbortSignal,
): Promise<ConversationList> {
    return request<ConversationList>(conversationsPath(agent, filter, page, pageSize), { signal });
}

export function fetchConversation(id: string, signal: AbortSignal): Promise<ConversationDetail> {
    return request<ConversationDetail>(`/monitoring/conversations/${id}`, { signal });
}

export function fetchScenarioDraft(
    id: string,
    cut: DraftCut,
    ordinal: number,
    turns: number | null,
    signal: AbortSignal,
): Promise<ScenarioDraft> {
    return request<ScenarioDraft>(scenarioDraftPath(id, cut, ordinal, turns), { signal });
}
