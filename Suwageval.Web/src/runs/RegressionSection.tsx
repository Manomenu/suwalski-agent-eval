import { Accordion, Anchor, Group, List, Paper, Stack, Text, Title } from "@mantine/core";

import type { RunResults } from "./api";
import {
    comparedWithHeading,
    groupChecks,
    historyText,
    knownFailuresText,
    compareLink,
    stateLabel,
    statedChecks,
    type StatedCheck,
} from "./regression";
import { CheckHistory } from "./CheckHistory";
import { runHref, shortId } from "./results";
import { StateBadge } from "./StateBadge";

/** What a check's state says about it: since when it fails, and what to compare with. */
function CheckLine({ item, runId }: { item: StatedCheck; runId: string }) {
    const regression = item.check.regression;
    const compare = compareLink(runId, regression);
    const failingSince = regression?.failingSince;
    return (
        <List.Item>
            <Group gap="xs">
                <Text fw={600}>
                    {item.scenarioKey} / {item.check.key}
                </Text>
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
                {groups.known.length > 0 && (
                    <Accordion variant="contained">
                        <Accordion.Item value="known">
                            <Accordion.Control>{knownFailuresText(groups.known.length)}</Accordion.Control>
                            <Accordion.Panel>
                                <List listStyleType="none" size="sm">
                                    {groups.known.map((item) => (
                                        <CheckLine key={`${item.scenarioKey}/${item.check.key}`} item={item} runId={runId} />
                                    ))}
                                </List>
                            </Accordion.Panel>
                        </Accordion.Item>
                    </Accordion>
                )}
            </Stack>
        </Paper>
    );
}
