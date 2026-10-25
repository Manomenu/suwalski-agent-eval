import { Anchor, Badge, CloseButton, Group, Select, Stack, Table, Text, Title } from "@mantine/core";
import { useState } from "react";

import { useRequest } from "../../api/useRequest";
import { formatTime } from "../../format/time";
import { HeaderHelp } from "../../help/HeaderHelp";
import { ListPagination } from "../../pagination/ListPagination";
import { usePageParam } from "../../pagination/usePageParam";
import { conversationHref, versionInMonitoringHref } from "../../routes/links";
import { fetchConversations, type ConversationList } from "./api";
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

/** The agent's monitored conversations: a table, newest turn first, narrowed by environment and state, a page at a time. */
export function ConversationsSection({ agent }: { agent: string }) {
    const [filter, setFilter] = useState<ConversationFilter>(() => initialFilter(window.location.search));
    const [page, setPage] = usePageParam("conversationsPage");
    const loaded = useRequest(JSON.stringify([agent, filter, page]), (signal) =>
        fetchConversations(agent, filter, page, CONVERSATIONS_PAGE_SIZE, signal),
    );
    const list = loaded.kind === "loaded" ? loaded.value : null;
    const narrow = (change: Partial<ConversationFilter>) => {
        setFilter({ ...filter, ...change });
        setPage(1);
    };
    const showAllChecks = () => {
        narrow({ failedCheck: null });
        const url = new URL(window.location.href);
        url.searchParams.delete(FAILED_CHECK_PARAM);
        window.history.replaceState(null, "", `${url.pathname}${url.search}${url.hash}`);
    };
    return (
        <Stack gap="sm">
            <Title order={3} id="conversations">
                <HeaderHelp label="Conversations" help={CONVERSATIONS_HELP} />
            </Title>
            <Group align="flex-end">
                <Select
                    label="Environment"
                    placeholder="All environments"
                    data={list?.environments ?? []}
                    value={filter.environment}
                    onChange={(value) => {
                        narrow({ environment: value });
                    }}
                    clearable
                />
                <Select
                    label="State"
                    placeholder="Open and closed"
                    data={["Open", "Closed"]}
                    value={filter.state}
                    onChange={(value) => {
                        narrow({ state: stateOf(value) });
                    }}
                    clearable
                />
                {filter.failedCheck !== null && (
                    <Badge
                        size="lg"
                        variant="light"
                        color="red"
                        rightSection={<CloseButton size="xs" aria-label="Show all checks" onClick={showAllChecks} />}
                    >
                        Failed {filter.failedCheck}
                    </Badge>
                )}
            </Group>
            {loaded.kind === "error" && <Text c="red">{loaded.error.message}</Text>}
            {list !== null && list.totalCount === 0 && (
                <Text c="dimmed">
                    No conversations yet: the deployed agent sends them as they happen (Suwageval:Url in its configuration).
                </Text>
            )}
            {list !== null && list.totalCount > 0 && (
                <>
                    <Table.ScrollContainer minWidth={900}>
                        <Table striped>
                            <Table.Thead>
                                <Table.Tr>
                                    <Table.Th>Conversation</Table.Th>
                                    <Table.Th>Environment</Table.Th>
                                    <Table.Th>State</Table.Th>
                                    <Table.Th>Turns</Table.Th>
                                    <Table.Th>
                                        <HeaderHelp label="Version" help={VERSION_COLUMN_HELP} />
                                    </Table.Th>
                                    <Table.Th>Last turn</Table.Th>
                                    <Table.Th>Checks</Table.Th>
                                </Table.Tr>
                            </Table.Thead>
                            <Table.Tbody>
                                {list.conversations.map((conversation) => (
                                    <Table.Tr key={conversation.id}>
                                        <Table.Td className="wrap-key">
                                            <Anchor href={conversationHref(conversation.id)}>{conversation.conversationId}</Anchor>
                                        </Table.Td>
                                        <Table.Td>{conversation.environment}</Table.Td>
                                        <Table.Td>
                                            <Badge variant="light" color={conversation.state === "Open" ? "blue" : "gray"}>
                                                {conversation.state}
                                            </Badge>
                                        </Table.Td>
                                        <Table.Td>{conversation.turns}</Table.Td>
                                        <Table.Td>
                                            <Anchor href={versionInMonitoringHref(agent, conversation.agentVersionId)}>
                                                {versionText(conversation)}
                                            </Anchor>
                                        </Table.Td>
                                        <Table.Td>{formatTime(conversation.lastTurnAt)}</Table.Td>
                                        <Table.Td>
                                            <ChecksCell conversation={conversation} />
                                        </Table.Td>
                                    </Table.Tr>
                                ))}
                            </Table.Tbody>
                        </Table>
                    </Table.ScrollContainer>
                    <ListPagination totalCount={list.totalCount} page={list.page} pageSize={CONVERSATIONS_PAGE_SIZE} onChange={setPage} />
                </>
            )}
        </Stack>
    );
}

/** The Checks column: the conversation's verdict in the shared colours, grey until its checks ran. */
function ChecksCell({ conversation }: { conversation: ConversationList["conversations"][number] }) {
    const verdict = checksVerdict(conversation);
    return verdict === null ? (
        <Badge variant="light" color="gray">
            {checksText(conversation)}
        </Badge>
    ) : (
        <VerdictBadge verdict={verdict} label={checksText(conversation)} />
    );
}
