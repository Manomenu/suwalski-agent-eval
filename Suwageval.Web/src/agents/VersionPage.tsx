import { Anchor, Badge, Code, Paper, Stack, Text, Title } from "@mantine/core";

import { formatTime } from "../runs/results";
import { fetchVersion } from "./api";
import { Page } from "./Page";
import { useLoaded } from "./useLoaded";
import { agentHref, jsonText, versionTitle } from "./view";

/** `/agents/<name>/versions/<id>`: the contract the agent served in that version. */
export function VersionPage({ name, versionId }: { name: string; versionId: string }) {
    const state = useLoaded(`${name}/${versionId}`, (signal) => fetchVersion(name, versionId, signal));

    return (
        <Page state={state} notFound={`There is no version ${versionId} of agent ${name}.`}>
            {(version) => {
                const { content } = version;
                const tools = content.tools ?? [];
                const events = content.context?.events ?? [];
                return (
                    <Stack gap="md">
                        <Anchor href={agentHref(name)}>← {name}</Anchor>
                        <Title order={2}>Version {versionTitle(version.id, content.label)}</Title>
                        <Text c="dimmed">
                            id {version.id} · created {formatTime(version.createdAt)} · hash {version.contentHash}
                        </Text>
                        {version.archivedAt !== null && (
                            <Badge color="gray" variant="light" w="fit-content">
                                Archived {formatTime(version.archivedAt)}
                            </Badge>
                        )}

                        <Paper withBorder p="md">
                            <Stack gap="sm">
                                <Title order={3}>System prompt</Title>
                                <Code block>{content.systemPrompt}</Code>
                            </Stack>
                        </Paper>

                        <Paper withBorder p="md">
                            <Stack gap="sm">
                                <Title order={3}>Model</Title>
                                <Text>{content.model.name}</Text>
                                {content.model.parameters != null && <Code block>{jsonText(content.model.parameters)}</Code>}
                            </Stack>
                        </Paper>

                        <Paper withBorder p="md">
                            <Stack gap="sm">
                                <Title order={3}>Tools</Title>
                                {tools.length === 0 && <Text c="dimmed">No tools.</Text>}
                                {tools.map((tool) => (
                                    <Stack key={tool.name} gap="xs">
                                        <Text fw={600}>{tool.name}</Text>
                                        <Text>{tool.description}</Text>
                                        <Code block>{jsonText(tool.parameters)}</Code>
                                    </Stack>
                                ))}
                            </Stack>
                        </Paper>

                        <Paper withBorder p="md">
                            <Stack gap="sm">
                                <Title order={3}>Context</Title>
                                <Badge variant="light" w="fit-content">
                                    {content.context?.usesRetrieval === true ? "uses retrieval" : "no retrieval"}
                                </Badge>
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
