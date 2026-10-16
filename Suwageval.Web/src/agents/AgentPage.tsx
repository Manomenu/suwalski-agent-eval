import { Anchor, Badge, Group, Paper, Stack, Table, Text, Title } from "@mantine/core";
import { useState } from "react";

import { fetchRuns } from "../runs/api";
import { formatTime, num, progress, statusColor, statusLabel } from "../runs/results";
import { fetchAgent, fetchVersions } from "./api";
import { Page } from "./Page";
import { useLoaded } from "./useLoaded";
import { runHref, shortId, versionHref, visibleRuns } from "./view";
import { VersionsSection } from "./VersionsSection";

/** `/agents/<name>`: the agent, its runs (newest first) and its versions. */
export function AgentPage({ name }: { name: string }) {
    const [showArchived, setShowArchived] = useState(false);
    // Bumped after versions are archived or restored: the lists are loaded again (the page stays until the new ones arrive).
    const [reload, setReload] = useState(0);
    const state = useLoaded(`${name}/${reload}`, async (signal) => {
        const [agent, runs, versions] = await Promise.all([fetchAgent(name, signal), fetchRuns(name, signal), fetchVersions(name, signal)]);
        return { agent, runs, versions };
    });

    return (
        <Page state={state} notFound={`There is no agent ${name}.`}>
            {({ agent, runs: allRuns, versions }) => {
                const runs = visibleRuns(allRuns, versions, showArchived);
                return (
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
                            <Text c="dimmed">
                                {allRuns.length === 0
                                    ? "This agent has no runs yet."
                                    : "Every run is of an archived version — switch on Show archived to see them."}
                            </Text>
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

                        <VersionsSection
                            agentName={agent.name}
                            versions={versions}
                            showArchived={showArchived}
                            onShowArchivedChange={setShowArchived}
                            onChanged={() => {
                                setReload(reload + 1);
                            }}
                        />
                    </Stack>
                );
            }}
        </Page>
    );
}
