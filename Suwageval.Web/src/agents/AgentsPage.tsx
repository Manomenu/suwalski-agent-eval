import { Anchor, Stack, Table, Text, Title } from "@mantine/core";

import { formatTime } from "../runs/results";
import { fetchAgents } from "./api";
import { Page } from "./Page";
import { useLoaded } from "./useLoaded";
import { agentHref } from "./view";

/** `/`: every agent, each linking to its page. */
export function AgentsPage() {
    const state = useLoaded("agents", fetchAgents);

    return (
        <Page state={state} notFound="">
            {(agents) => (
                <Stack gap="md">
                    <Title order={2}>Agenci</Title>
                    {agents.length === 0 ? (
                        <Text c="dimmed">Nie ma jeszcze agentów. Dodaj pierwszego przez suwageval push.</Text>
                    ) : (
                        <Table>
                            <Table.Thead>
                                <Table.Tr>
                                    <Table.Th>Nazwa</Table.Th>
                                    <Table.Th>Adres</Table.Th>
                                    <Table.Th>Utworzony</Table.Th>
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
                    )}
                </Stack>
            )}
        </Page>
    );
}
