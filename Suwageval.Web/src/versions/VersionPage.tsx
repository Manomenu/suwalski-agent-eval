import { DataVersion } from "../dataVersion/DataVersion";
import { Anchor, Badge, Code, Paper, Stack, Text, Title } from "@mantine/core";

import { fetchVersion } from "../agents/api";
import { Page } from "../agents/Page";
import { useRequest } from "../api/useRequest";
import { jsonText } from "./view";
import { agentHref } from "../routes/links";
import { formatTime } from "../format/time";
import { versionTitle } from "../format/names";

/** `/agents/<name>/versions/<id>`: the contract the agent served in that version. */
export function VersionPage({ name, versionId }: { name: string; versionId: string }) {
    const state = useRequest(`${name}/${versionId}`, (signal) => fetchVersion(name, versionId, signal));

    return (
        <Page state={state} notFound={`There is no version ${versionId} of agent ${name}.`}>
            {(version) => {
                const { content } = version;
                const events = content.context?.events ?? [];
                return (
                    <Stack gap="md">
                        <Anchor href={agentHref(name)}>← {name}</Anchor>
                        <Title order={2}>Version {versionTitle(version.id, content.label)}</Title>
                        <Text c="dimmed">
                            id {version.id} · created {formatTime(version.createdAt)} · hash {version.contentHash}
                        </Text>
                        {version.archivedAt != null && (
                            <Badge color="gray" variant="light" w="fit-content">
                                Archived {formatTime(version.archivedAt)}
                            </Badge>
                        )}

                        {content.agents.map((agent) => (
                            <Paper key={agent.name} withBorder p="md">
                                <Stack gap="sm">
                                    <Title order={3}>Agent {agent.name}</Title>
                                    <Title order={4}>Config</Title>
                                    <Code block>{agent.config}</Code>
                                    <Title order={4}>Model</Title>
                                    <Text>{agent.model.name}</Text>
                                    {agent.model.parameters != null && <Code block>{jsonText(agent.model.parameters)}</Code>}
                                    <Title order={4}>Tools</Title>
                                    {(agent.tools ?? []).length === 0 && <Text c="dimmed">No tools.</Text>}
                                    {(agent.tools ?? []).map((tool) => (
                                        <Stack key={tool.name} gap="xs">
                                            <Text fw={600}>{tool.name}</Text>
                                            <Text>{tool.description}</Text>
                                            <Code block>{jsonText(tool.parameters)}</Code>
                                        </Stack>
                                    ))}
                                </Stack>
                            </Paper>
                        ))}

                        {content.otherConfig != null && content.otherConfig !== "" && (
                            <Paper withBorder p="md">
                                <Stack gap="sm">
                                    <Title order={3}>Other config</Title>
                                    <Code block>{content.otherConfig}</Code>
                                </Stack>
                            </Paper>
                        )}

                        <Paper withBorder p="md">
                            <Stack gap="sm">
                                <Title order={3}>Context</Title>
                                <Badge variant="light" w="fit-content">
                                    {content.context?.usesRetrieval === true ? "uses retrieval" : "no retrieval"}
                                </Badge>
                                {content.context?.retrieval != null && (
                                    <DataVersion
                                        size="sm"
                                        dataVersion={content.context.retrieval.dataVersion}
                                        description={content.context.retrieval.description}
                                    />
                                )}
                                {events.length === 0 && <Text c="dimmed">No events.</Text>}
                                {events.map((event) => (
                                    <Stack key={event.type} gap="xs">
                                        <Text fw={600}>Event {event.type}</Text>
                                        <Code block>{jsonText(event.dataSchema)}</Code>
                                    </Stack>
                                ))}
                            </Stack>
                        </Paper>
                    </Stack>
                );
            }}
        </Page>
    );
}
