import { dataOf } from "@maniumek/suwfrontkit";
import { queryOptions } from "@tanstack/react-query";

import { api } from "../../api/client";
import type { components } from "../../api/openapi";
import { conversationsParams, scenarioDraftParams, type ConversationFilter, type DraftCut } from "./conversations";

export type ConversationList = components["schemas"]["ConversationList"];
export type ConversationDetail = components["schemas"]["ConversationDetail"];
export type ScenarioDraft = components["schemas"]["ScenarioDraft"];

function fetchConversations(
    agent: string,
    filter: ConversationFilter,
    page: number,
    pageSize: number,
    signal: AbortSignal,
): Promise<ConversationList> {
    return dataOf(
        api.GET("/monitoring/agents/{agent}/conversations", {
            params: { path: { agent }, query: conversationsParams(filter, page, pageSize) },
            signal,
        }),
    );
}

function fetchConversation(id: string, signal: AbortSignal): Promise<ConversationDetail> {
    return dataOf(api.GET("/monitoring/conversations/{id}", { params: { path: { id } }, signal }));
}

function fetchScenarioDraft(id: string, cut: DraftCut, ordinal: number, turns: number | null, signal: AbortSignal): Promise<ScenarioDraft> {
    return dataOf(
        api.GET("/monitoring/conversations/{id}/scenario", {
            params: { path: { id }, query: scenarioDraftParams(cut, ordinal, turns) },
            signal,
        }),
    );
}

export const conversationsQuery = (agent: string, filter: ConversationFilter, page: number, pageSize: number) =>
    queryOptions({
        queryKey: ["monitoring", agent, "conversations", filter, page, pageSize] as const,
        queryFn: ({ signal }) => fetchConversations(agent, filter, page, pageSize, signal),
    });

export const conversationQuery = (id: string) =>
    queryOptions({
        queryKey: ["conversations", id] as const,
        queryFn: ({ signal }) => fetchConversation(id, signal),
    });

export const scenarioDraftQuery = (id: string, cut: DraftCut, ordinal: number, turns: number | null, run: number) =>
    queryOptions({
        queryKey: ["conversations", id, "draft", cut, ordinal, turns, run] as const,
        queryFn: ({ signal }) => fetchScenarioDraft(id, cut, ordinal, turns, signal),
    });
