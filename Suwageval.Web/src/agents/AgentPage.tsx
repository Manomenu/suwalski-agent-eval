import { Anchor, Badge, Group, Stack, Table, Text, Title } from "@mantine/core";

import { fetchRuns } from "../runs/api";
import { formatTime, num, progress, statusColor, statusLabel } from "../runs/results";
import { fetchAgent, fetchVersions } from "./api";
import { Page } from "./Page";
import { useLoaded } from "./useLoaded";
import { runHref, shortId, versionHref, versionLabel } from "./view";

/** `/agents/<name>`: the agent, its runs (newest first) and its versions. */
export function AgentPage({ name }: { name: string }) {
    const state = useLoaded(name, async (signal) => {
        const [agent, runs, versions] = await Promise.all([fetchAgent(name, signal), fetchRuns(name, signal), fetchVersions(name, signal)]);
        return { agent, runs, versions };
    });

    return (
        <Page state={state} notFound={`Nie ma agenta ${name}.`}>
            {({ agent, runs, versions }) => (
                <Stack gap="lg">
                    <Anchor href="/">← Agenci</Anchor>
                    <Stack gap={4}>
                        <Title order={2}>{agent.name}</Title>
                        <Text c="dimmed">
                            {agent.endpointUrl} · utworzony {formatTime(agent.createdAt)}
                        </Text>
                        <Text c="dimmed">
                            wersje {num(agent.versions)} · scenariusze {num(agent.scenarios)} · checki {num(agent.checks)}
                        </Text>
                    </Stack>

                    <Title order={3}>Runy</Title>
                    {runs.length === 0 ? (
                        <Text c="dimmed">Ten agent nie miał jeszcze runu.</Text>
                    ) : (
                        <Table>
                            <Table.Thead>
                                <Table.Tr>
                                    <Table.Th>Run</Table.Th>
                                    <Table.Th>Status</Table.Th>
                                    <Table.Th>Tag</Table.Th>
                                    <Table.Th>Wersja</Table.Th>
                                    <Table.Th>Checki</Table.Th>
                                    <Table.Th>Próbki</Table.Th>
                                    <Table.Th>Utworzony</Table.Th>
                                    <Table.Th>Zakończony</Table.Th>
                                </Table.Tr>
                            </Table.Thead>
                            <Table.Tbody>
                                {runs.map((run) => (
                                    <Table.Tr key={run.id}>
                                        <Table.Td>
                                            <Anchor href={runHref(run.id)}>{shortId(run.id)}</Anchor>
                                        </Table.Td>
                                        <Table.Td>
                                            <Badge color={statusColor(run.status)}>{statusLabel(run.status)}</Badge>
                                        </Table.Td>
                                        <Table.Td>{run.tag ?? "—"}</Table.Td>
                                        <Table.Td>
                                            <Anchor href={versionHref(agent.name, run.agentVersionId)}>
                                                {shortId(run.agentVersionId)}
                                            </Anchor>
                                        </Table.Td>
                                        <Table.Td>
                                            <Group gap={4}>
                                                <Text span c="green">
                                                    {num(run.checksPassed)} zal.
                                                </Text>
                                                <Text span c={num(run.checksFailed) > 0 ? "red" : "dimmed"}>
                                                    {num(run.checksFailed)} niezal.
                                                </Text>
                                            </Group>
                                        </Table.Td>
                                        <Table.Td>{progress(run)}</Table.Td>
                                        <Table.Td>{formatTime(run.createdAt)}</Table.Td>
                                        <Table.Td>{formatTime(run.finishedAt)}</Table.Td>
                                    </Table.Tr>
                                ))}
                            </Table.Tbody>
                        </Table>
                    )}

                    <Title order={3}>Wersje</Title>
                    {versions.length === 0 ? (
                        <Text c="dimmed">Ten agent nie ma jeszcze wersji — powstaje przy pierwszym runie.</Text>
                    ) : (
                        <Table>
                            <Table.Thead>
                                <Table.Tr>
                                    <Table.Th>Etykieta</Table.Th>
                                    <Table.Th>Utworzona</Table.Th>
                                    <Table.Th>Id</Table.Th>
                                </Table.Tr>
                            </Table.Thead>
                            <Table.Tbody>
                                {versions.map((version) => (
                                    <Table.Tr key={version.id}>
                                        <Table.Td>{versionLabel(version)}</Table.Td>
                                        <Table.Td>{formatTime(version.createdAt)}</Table.Td>
                                        <Table.Td>
                                            <Anchor href={versionHref(agent.name, version.id)}>{version.id}</Anchor>
                                        </Table.Td>
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
