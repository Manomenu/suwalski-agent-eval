import { Anchor, Group, List, Paper, Stack, Text, Title } from "@mantine/core";

import { formatTime } from "../runs/results";
import { fetchVersion, type AgentVersionDetail } from "./api";
import { Page } from "./Page";
import { useLoaded } from "./useLoaded";
import { VersionDiffView } from "./VersionDiffView";
import { contractYaml, oldestFirst, summarizeChanges } from "./versionDiff";
import { agentHref, versionHref, versionTitle } from "./view";

function VersionCard({ name, role, version }: { name: string; role: string; version: AgentVersionDetail }) {
    const models = [...new Set(version.content.agents.map((agent) => agent.model.name))].join(", ");
    return (
        <Paper withBorder p="md">
            <Stack gap={4}>
                <Text size="sm" c="dimmed">
                    {role}
                </Text>
                <Anchor href={versionHref(name, version.id)} fw={600}>
                    {versionTitle(version.id, version.content.label)}
                </Anchor>
                <Text size="sm">created {formatTime(version.createdAt)}</Text>
                <Text size="sm">models {models}</Text>
            </Stack>
        </Paper>
    );
}

/** `/agents/<name>/versions/compare?a=…&b=…`: what changed from the older of two versions to the newer — a summary, then the line diff. */
export function VersionComparePage({ name, a, b }: { name: string; a: string; b: string }) {
    const state = useLoaded(`${name}/${a}/${b}`, (signal) => Promise.all([fetchVersion(name, a, signal), fetchVersion(name, b, signal)]));

    return (
        <Page state={state} notFound={`Agent ${name} has no version ${a} or ${b}.`}>
            {(versions) => {
                const [base, next] = oldestFirst(...versions);
                const summary = summarizeChanges(base.content, next.content);
                return (
                    <Stack gap="md">
                        <Anchor href={agentHref(name)}>← {name}</Anchor>
                        <Title order={2}>Compare versions</Title>
                        <Group align="stretch" gap="md" grow>
                            <VersionCard name={name} role="Base (older)" version={base} />
                            <VersionCard name={name} role="Newer" version={next} />
                        </Group>
                        <Title order={3}>Summary of changes</Title>
                        {summary.length === 0 ? (
                            <Text>No differences</Text>
                        ) : (
                            <List>
                                {summary.map((line) => (
                                    <List.Item key={line}>{line}</List.Item>
                                ))}
                            </List>
                        )}
                        <Title order={3}>Diff</Title>
                        <VersionDiffView from={contractYaml(base.content)} to={contractYaml(next.content)} />
                    </Stack>
                );
            }}
        </Page>
    );
}
