import { Anchor, Badge, Button, formatDateTime, Label, Select, Table } from "@maniumek/suwfrontkit";
import { IconX } from "@tabler/icons-react";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useId, useState } from "react";

import { HeaderHelp } from "../../help/HeaderHelp";
import { ListPagination } from "../../pagination/ListPagination";
import { usePageParam } from "../../pagination/usePageParam";
import { conversationHref, versionInMonitoringHref } from "../../routes/links";
import { conversationsQuery, type ConversationList } from "./api";
import { VerdictBadge } from "../../verdict/VerdictBadge";
import {
    checksVerdict,
    checksText,
    CONVERSATIONS_HELP,
    FAILED_CHECK_PARAM,
    initialFilter,
    CONVERSATIONS_PAGE_SIZE,
    stateOf,
    VERSION_COLUMN_HELP,
    versionText,
    type ConversationFilter,
} from "./conversations";

export function ConversationsSection({ agent, environment }: { agent: string; environment: string | null }) {
    const stateId = useId();
    const [narrowed, setFilter] = useState<ConversationFilter>(() => initialFilter(window.location.search));
    const filter = { ...narrowed, environment };
    const [page, setPage] = usePageParam("conversationsPage");
    const loaded = useQuery({
        ...conversationsQuery(agent, filter, page, CONVERSATIONS_PAGE_SIZE),
        placeholderData: keepPreviousData,
    });
    const list = loaded.data ?? null;
    const narrow = (change: Partial<ConversationFilter>) => {
        setFilter({ ...narrowed, ...change });
        setPage(1);
    };
    const showAllChecks = () => {
        narrow({ failedCheck: null });
        const url = new URL(window.location.href);
        url.searchParams.delete(FAILED_CHECK_PARAM);
        window.history.replaceState(null, "", `${url.pathname}${url.search}${url.hash}`);
    };
    return (
        <div className="flex flex-col gap-3">
            <h3 id="conversations" className="text-[22px] font-bold">
                <HeaderHelp label="Conversations" help={CONVERSATIONS_HELP} />
            </h3>
            <div className="flex flex-wrap items-end gap-4">
                <div className="flex flex-col gap-1">
                    <Label htmlFor={stateId}>State</Label>
                    <Select
                        id={stateId}
                        value={filter.state ?? ""}
                        onChange={(event) => {
                            narrow({ state: stateOf(event.target.value) });
                        }}
                    >
                        <option value="">Open and closed</option>
                        <option value="Open">Open</option>
                        <option value="Closed">Closed</option>
                    </Select>
                </div>
                {filter.failedCheck !== null && (
                    <Badge variant="danger" className="gap-1 pr-0.5">
                        Failed {filter.failedCheck}
                        <Button variant="ghost" size="icon" className="size-5" aria-label="Show all checks" onClick={showAllChecks}>
                            <IconX size={12} aria-hidden />
                        </Button>
                    </Badge>
                )}
            </div>
            {loaded.isError && <p className="text-destructive">{loaded.error.message}</p>}
            {list !== null && list.totalCount === 0 && (
                <p className="text-muted-foreground">
                    No conversations yet: the deployed agent sends them as they happen (Suwageval:Url in its configuration).
                </p>
            )}
            {list !== null && list.totalCount > 0 && (
                <>
                    <Table className="min-w-[900px]">
                        <thead>
                            <tr>
                                <th>Conversation</th>
                                <th>Environment</th>
                                <th>State</th>
                                <th>Turns</th>
                                <th>
                                    <HeaderHelp label="Version" help={VERSION_COLUMN_HELP} />
                                </th>
                                <th>Last turn</th>
                                <th>Checks</th>
                            </tr>
                        </thead>
                        <tbody>
                            {list.conversations.map((conversation) => (
                                <tr key={conversation.id}>
                                    <td className="wrap-key">
                                        <Anchor href={conversationHref(conversation.id)}>{conversation.conversationId}</Anchor>
                                    </td>
                                    <td>{conversation.environment}</td>
                                    <td>
                                        <Badge variant={conversation.state === "Open" ? "info" : "neutral"}>{conversation.state}</Badge>
                                    </td>
                                    <td>{conversation.turns}</td>
                                    <td>
                                        <Anchor href={versionInMonitoringHref(agent, conversation.agentVersionId)}>
                                            {versionText(conversation)}
                                        </Anchor>
                                    </td>
                                    <td>{formatDateTime(conversation.lastTurnAt)}</td>
                                    <td>
                                        <ChecksCell conversation={conversation} />
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </Table>
                    <ListPagination totalCount={list.totalCount} page={list.page} pageSize={CONVERSATIONS_PAGE_SIZE} onChange={setPage} />
                </>
            )}
        </div>
    );
}

function ChecksCell({ conversation }: { conversation: ConversationList["conversations"][number] }) {
    const verdict = checksVerdict(conversation);
    return verdict === null ? (
        <Badge variant="neutral">{checksText(conversation)}</Badge>
    ) : (
        <VerdictBadge verdict={verdict} label={checksText(conversation)} />
    );
}
