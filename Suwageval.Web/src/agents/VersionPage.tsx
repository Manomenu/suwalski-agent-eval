import { Anchor, Badge, Code, Stack, Text, Title } from "@mantine/core";

import { formatTime } from "../runs/results";
import { fetchVersion } from "./api";
import { Page } from "./Page";
import { useLoaded } from "./useLoaded";
import { agentHref, jsonText, versionTitle } from "./view";

/** `/agents/<name>/versions/<id>`: the contract the agent served in that version. */
export function VersionPage({ name, versionId }: { name: string; versionId: string }) {
    const state = useLoaded(`${name}/${versionId}`, (signal) => fetchVersion(name, versionId, signal));

    return (
        <Page state={state} notFound={`Nie ma wersji ${versionId} agenta ${name}.`}>
            {(version) => {
                const { content } = version;
                const tools = content.tools ?? [];
                const events = content.context?.events ?? [];
                return (
                    <Stack gap="md">
                        <Anchor href={agentHref(name)}>← {name}</Anchor>
                        <Title order={2}>Wersja {versionTitle(version.id, content.label)}</Title>
                        <Text c="dimmed">
                            id {version.id} · utworzona {formatTime(version.createdAt)} · hash {version.contentHash}
                        </Text>

                        <Title order={3}>Prompt systemowy</Title>
                        <Code block>{content.systemPrompt}</Code>

                        <Title order={3}>Model</Title>
                        <Text>{content.model.name}</Text>
                        {content.model.parameters != null && <Code block>{jsonText(content.model.parameters)}</Code>}

                        <Title order={3}>Narzędzia</Title>
                        {tools.length === 0 && <Text c="dimmed">Brak narzędzi.</Text>}
                        {tools.map((tool) => (
                            <Stack key={tool.name} gap="xs">
                                <Text fw={600}>{tool.name}</Text>
                                <Text>{tool.description}</Text>
                                <Code block>{jsonText(tool.parameters)}</Code>
                            </Stack>
                        ))}

                        <Title order={3}>Kontekst</Title>
                        <Badge variant="light" w="fit-content">
                            {content.context?.usesRetrieval === true ? "używa retrievalu" : "bez retrievalu"}
                        </Badge>
                        {events.length === 0 && <Text c="dimmed">Brak zdarzeń.</Text>}
                        {events.map((event) => (
                            <Stack key={event.type} gap="xs">
                                <Text fw={600}>Zdarzenie {event.type}</Text>
                                <Code block>{jsonText(event.dataSchema)}</Code>
                            </Stack>
                        ))}
                    </Stack>
                );
            }}
        </Page>
    );
}
