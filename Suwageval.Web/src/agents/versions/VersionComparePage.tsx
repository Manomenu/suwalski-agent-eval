import { Anchor, Group, Paper, Stack, Text, Title } from "@mantine/core";

import { type AgentVersion } from "../api";
import { Page } from "../Page";
import { useRequest } from "../../api/useRequest";
import { LineDiffView } from "../../diff/LineDiffView";
import { agentHref, versionHref } from "../../routes/links";
import { formatTime } from "../../format/time";
import { versionTitle } from "../../format/names";
import { fetchVersionDiff } from "./api";

function VersionCard({ name, role, version }: { name: string; role: string; version: AgentVersion }) {
    return (
        <Paper withBorder p="md">
            <Stack gap={4}>
                <Text size="sm" c="dimmed">
                    {role}
                </Text>
                <Anchor href={versionHref(name, version.id)} fw={600}>
                    {versionTitle(version.id, version.label)}
                </Anchor>
                <Text size="sm">created {formatTime(version.createdAt)}</Text>
                <Text size="sm">models {version.modelNames.join(", ")}</Text>
            </Stack>
        </Paper>
    );
}

/** `/agents/<name>/versions/compare?a=…&b=…`: what changed from the older of two versions to the newer — the line diff the server made. */
export function VersionComparePage({ name, a, b }: { name: string; a: string; b: string }) {
    const state = useRequest(`${name}/${a}/${b}`, (signal) => fetchVersionDiff(name, a, b, signal));

    return (
        <Page state={state} notFound={`Agent ${name} has no version ${a} or ${b}.`}>
            {(diff) => (
                <Stack gap="md">
                    <Anchor href={agentHref(name)}>← {name}</Anchor>
                    <Title order={2}>Compare versions</Title>
                    <Group align="stretch" gap="md" grow>
                        <VersionCard name={name} role="Base (older)" version={diff.from} />
                        <VersionCard name={name} role="Newer" version={diff.to} />
                    </Group>
                    <Title order={3}>Diff</Title>
                    <LineDiffView blocks={diff.blocks} />
                </Stack>
            )}
        </Page>
    );
}
