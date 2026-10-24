// This feature's HTTP calls, typed from the generated OpenAPI types.
import { request } from "../../api/client";
import type { components } from "../../api/openapi";
import { conversationsPath, type ConversationFilter } from "./conversations";

export type ConversationList = components["schemas"]["ConversationList"];
export type ConversationDetail = components["schemas"]["ConversationDetail"];

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
