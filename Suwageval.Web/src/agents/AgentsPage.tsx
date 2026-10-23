import { Anchor, Paper, Stack, Table, Text, Title } from "@mantine/core";

import { formatTime } from "../runs/results";
import { ListPagination } from "../pagination/ListPagination";
import { pageOfItems } from "../pagination/pages";
import { usePageParam } from "../pagination/usePageParam";
import { fetchAgents } from "./api";
import { Page } from "./Page";
import { useRequest } from "../api/useRequest";
import { agentHref } from "./view";

/** `/`: every agent, each linking to its page. */
export function AgentsPage() {
    const state = useRequest("agents", fetchAgents);
    const [requestedPage, setRequestedPage] = usePageParam("agentsPage");

    return (
        <Page state={state} notFound="">
            {(allAgents) => {
                const { items: agents, page } = pageOfItems(allAgents, requestedPage);
                return (
                    <Stack gap="md">
                        <Title order={2}>Agents</Title>
                        {allAgents.length === 0 ? (
                            <Text c="dimmed">There are no agents yet. Add the first one with suwageval push.</Text>
                        ) : (
                            <Paper withBorder p="md">
                                <Table.ScrollContainer minWidth={500}>
                                    <Table highlightOnHover>
                                        <Table.Thead>
                                            <Table.Tr>
                                                <Table.Th>Name</Table.Th>
                                                <Table.Th>Address</Table.Th>
                                                <Table.Th>Created</Table.Th>
                                            </Table.Tr>
                                        </Table.Thead>
                                        <Table.Tbody>
                                            {agents.map((agent) => (
                                                <Table.Tr key={agent.id}>
                                                    <Table.Td>
                                                        <Anchor href={agentHref(agent.name)}>{agent.name}</Anchor>
                                                    </Table.Td>
                                                    <Table.Td>{agent.endpointUrl}</Table.Td>
                                                    <Table.Td>{formatTime(agent.createdAt)}</Table.Td>
                                                </Table.Tr>
                                            ))}
                                        </Table.Tbody>
                                    </Table>
                                </Table.ScrollContainer>
                                <ListPagination totalCount={allAgents.length} page={page} onChange={setRequestedPage} />
                            </Paper>
                        )}
                    </Stack>
                );
            }}
        </Page>
    );
}
