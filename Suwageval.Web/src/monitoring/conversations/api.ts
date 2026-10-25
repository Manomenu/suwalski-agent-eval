// This feature's HTTP calls, typed from the generated OpenAPI types.
import { request } from "../../api/client";
import type { components } from "../../api/openapi";
import { conversationsPath, scenarioDraftPath, type ConversationFilter, type DraftCut } from "./conversations";

export type ConversationList = components["schemas"]["ConversationList"];
export type ConversationDetail = components["schemas"]["ConversationDetail"];
export type ScenarioDraft = components["schemas"]["ScenarioDraft"];

/** One page of the agent's monitored conversations; see `conversationsPath`. */
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

/** The conversation drafted as a scenario file (its key, the file's path, the YAML and a notice); see `scenarioDraftPath`. */
export function fetchScenarioDraft(
    id: string,
    cut: DraftCut,
    ordinal: number,
    turns: number | null,
    signal: AbortSignal,
): Promise<ScenarioDraft> {
    return request<ScenarioDraft>(scenarioDraftPath(id, cut, ordinal, turns), { signal });
}
