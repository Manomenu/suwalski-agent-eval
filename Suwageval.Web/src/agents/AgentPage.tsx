import { Anchor, Badge, Group, Paper, Stack, Table, Text, Title } from "@mantine/core";

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
        <Page state={state} notFound={`There is no agent ${name}.`}>
            {({ agent, runs, versions }) => (
                <Stack gap="lg">
                    <Anchor href="/">← Agents</Anchor>
                    <Paper withBorder p="md">
                        <Stack gap={4}>
                            <Title order={2}>{agent.name}</Title>
                            <Text c="dimmed">
                                {agent.endpointUrl} · created {formatTime(agent.createdAt)}
                            </Text>
                            <Text c="dimmed">
                                versions {num(agent.versions)} · scenarios {num(agent.scenarios)} · checks {num(agent.checks)}
                            </Text>
                        </Stack>
                    </Paper>

                    <Title order={3}>Runs</Title>
                    {runs.length === 0 ? (
                        <Text c="dimmed">This agent has no runs yet.</Text>
                    ) : (
                        <Paper withBorder p="md">
                            <Table.ScrollContainer minWidth={900}>
                                <Table highlightOnHover>
                                    <Table.Thead>
                                        <Table.Tr>
                                            <Table.Th>Run</Table.Th>
                                            <Table.Th>Status</Table.Th>
                                            <Table.Th>Tag</Table.Th>
                                            <Table.Th>Version</Table.Th>
                                            <Table.Th>Checks</Table.Th>
                                            <Table.Th>Samples</Table.Th>
                                            <Table.Th>Created</Table.Th>
                                            <Table.Th>Finished</Table.Th>
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
                                                            {num(run.checksPassed)} passed
                                                        </Text>
                                                        <Text span c={num(run.checksFailed) > 0 ? "red" : "dimmed"}>
                                                            {num(run.checksFailed)} failed
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
                            </Table.ScrollContainer>
                        </Paper>
                    )}

                    <Title order={3}>Versions</Title>
                    {versions.length === 0 ? (
                        <Text c="dimmed">This agent has no versions yet — one is created on the first run.</Text>
                    ) : (
                        <Paper withBorder p="md">
                            <Table.ScrollContainer minWidth={500}>
                                <Table highlightOnHover>
                                    <Table.Thead>
                                        <Table.Tr>
                                            <Table.Th>Label</Table.Th>
                                            <Table.Th>Created</Table.Th>
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
                            </Table.ScrollContainer>
                        </Paper>
                    )}
                </Stack>
            )}
        </Page>
    );
}
