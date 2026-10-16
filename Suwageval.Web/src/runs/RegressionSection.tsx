import { Accordion, Anchor, Group, List, Paper, Stack, Text, Title } from "@mantine/core";

import type { RunResults } from "./api";
import {
    AGENT_FAILED_NOTE,
    comparedWithHeading,
    groupChecks,
    historyText,
    knownFailuresText,
    compareLink,
    stateLabel,
    statedChecks,
    summaryChips,
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
                <StateBadge state={item.state}>{stateLabel(item.state)}</StateBadge>
                <Text fw={600}>
                    {item.scenarioKey} / {item.check.key}
                </Text>
                <CheckHistory check={item.check} />
                {failingSince != null && (
                    <Text span c="dimmed">
                        failing since <Anchor href={runHref(failingSince)}>run {shortId(failingSince)}</Anchor>
                    </Text>
                )}
                {regression?.agentFailed === true && (
                    <Text span c="dimmed">
                        {AGENT_FAILED_NOTE}
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
                <Group gap="xs">
                    {summaryChips(summary).map((chip) => (
                        <StateBadge key={chip.state} state={chip.state} variant="light">
                            {chip.count} {chip.label.toLowerCase()}
                        </StateBadge>
                    ))}
                </Group>
                {groups.attention.length > 0 && (
                    <List listStyleType="none" spacing="xs">
                        {groups.attention.map((item) => (
                            <CheckLine key={`${item.scenarioKey}/${item.check.key}`} item={item} runId={runId} />
                        ))}
                    </List>
                )}
                {groups.brief.length > 0 && (
                    <List listStyleType="none" size="sm">
                        {groups.brief.map((item) => (
                            <CheckLine key={`${item.scenarioKey}/${item.check.key}`} item={item} runId={runId} />
                        ))}
                    </List>
                )}
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
