import { Anchor, Group, Paper, Stack, Text, Title } from "@mantine/core";

import type { UnstableCheck } from "../runs/api";
import { cardRows, passedOfRuns } from "../runs/stability";
import { runHref, versionTitle } from "./view";

/**
 * The checks that passed in some runs of one version and failed in others, so a failure there may be noise. Nothing at all
 * when there are none. Each row opens the newest run that failed the check.
 */
export function UnstableChecksCard({ checks }: { checks: UnstableCheck[] }) {
    if (checks.length === 0) return null;
    return (
        <Paper withBorder p="md">
            <Stack gap="sm">
                <Title order={3}>Unstable checks</Title>
                <Text c="dimmed" size="sm">
                    Passed in some runs of a version and failed in others.
                </Text>
                {cardRows(checks).map((check) => (
                    <Group
                        key={JSON.stringify([check.agentVersionId, check.scenarioKey, check.checkKey, check.checkRevision])}
                        justify="space-between"
                        wrap="nowrap"
                        align="baseline"
                    >
                        <Anchor href={runHref(check.newestFailedRunId)} style={{ overflowWrap: "anywhere" }}>
                            {`${check.scenarioKey} / ${check.checkKey}`}
                        </Anchor>
                        <Group gap="xs" wrap="nowrap">
                            <Text fw={600}>{passedOfRuns(check)}</Text>
                            <Text c="dimmed" size="sm">
                                {versionTitle(check.agentVersionId, check.versionLabel)}
                            </Text>
                        </Group>
                    </Group>
                ))}
            </Stack>
        </Paper>
    );
}
