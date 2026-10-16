import { Anchor, Collapse, Group, List, Paper, Stack, Text, Title } from "@mantine/core";
import { useState } from "react";

import type { RunResults } from "./api";
import { comparedWithHeading, groupChecks, historyText, compareLink, stateLabel, statedChecks, type StatedCheck } from "./regression";
import { CheckHistory } from "./CheckHistory";
import { checkAnchor, runHref, shortId } from "./results";
import { StateBadge } from "./StateBadge";

/** What a check's state says about it: since when it fails, and what to compare with. */
function CheckLine({ item, runId }: { item: StatedCheck; runId: string }) {
    const regression = item.check.regression;
    const compare = compareLink(runId, regression);
    const failingSince = regression?.failingSince;
    return (
        <List.Item>
            <Group gap="xs">
                <Anchor href={`#${checkAnchor(item.scenarioKey, item.check.key)}`} fw={600} c="inherit">
                    {item.scenarioKey} / {item.check.key}
                </Anchor>
                <CheckHistory check={item.check} />
                {failingSince != null && (
                    <Text span c="dimmed">
                        failing since <Anchor href={runHref(failingSince)}>run {shortId(failingSince)}</Anchor>
                    </Text>
                )}
                {compare !== null && <Anchor href={compare.href}>{compare.label}</Anchor>}
            </Group>
        </List.Item>
    );
}

/** Known failures are not news: the same header as the other groups, the checks folded until asked for. */
function KnownFailures({ known, runId }: { known: readonly StatedCheck[]; runId: string }) {
    const [opened, setOpened] = useState(false);
    return (
        <Stack gap="xs">
            <Group gap="xs">
                <StateBadge state="KnownFailure" variant="light">
                    {stateLabel("KnownFailure")}
                </StateBadge>
                <Text span c="dimmed">
                    ({known.length})
                </Text>
                <Anchor component="button" type="button" size="sm" aria-expanded={opened} onClick={() => setOpened((open) => !open)}>
                    {opened ? "hide" : "show"}
                </Anchor>
            </Group>
            <Collapse expanded={opened}>
                <List listStyleType="none" spacing="xs" ml="md">
                    {known.map((item) => (
                        <CheckLine key={`${item.scenarioKey}/${item.check.key}`} item={item} runId={runId} />
                    ))}
                </List>
            </Collapse>
        </Stack>
    );
}

/** The run's checks against their history: what broke, what is new, what is only noise. Nothing without a regression summary. */
export function RegressionSection({ results }: { results: RunResults }) {
    const summary = results.regression;
    if (summary == null) return null;
    const groups = groupChecks(statedChecks(results));
    const runId = results.run.id;
    return (
        <Paper withBorder p="md" aria-label={comparedWithHeading(summary)}>
            <Stack gap="sm">
                <Title order={3}>{comparedWithHeading(summary)}</Title>
                <Text c="dimmed" size="sm">
                    {historyText(summary)}
                </Text>
                {groups.groups.map((group) => (
                    <Stack key={group.state} gap="xs">
                        <Group gap="xs">
                            <StateBadge state={group.state} variant="light">
                                {stateLabel(group.state)}
                            </StateBadge>
                            <Text span c="dimmed">
                                ({group.checks.length})
                            </Text>
                        </Group>
                        <List listStyleType="none" spacing="xs" ml="md">
                            {group.checks.map((item) => (
                                <CheckLine key={`${item.scenarioKey}/${item.check.key}`} item={item} runId={runId} />
                            ))}
                        </List>
                    </Stack>
                ))}
                {groups.known.length > 0 && <KnownFailures known={groups.known} runId={runId} />}
            </Stack>
        </Paper>
    );
}
