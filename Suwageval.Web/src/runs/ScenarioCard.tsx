import { Accordion, Alert, Badge, Card, Group, List, Stack, Text, Title } from "@mantine/core";

import { CheckDefinitionButton } from "../checks/CheckDefinitionButton";
import { ModelJudgedIcon } from "../checks/ModelJudgedIcon";
import { UnstableCheckIcon } from "../checks/UnstableCheckIcon";
import type { ScenarioRunResult, UnstableCheck } from "./api";
import { ConversationView } from "./ConversationView";
import { CheckHistory } from "./CheckHistory";
import { stateLabel } from "./regression";
import { StateBadge } from "./StateBadge";
import { findUnstable, unstableTooltip } from "./stability";
import { checkSummary, failedEvidenceKeys, formatSeconds, notJudgedCount } from "./results";

/** One scenario: its checks with verdicts, the warnings of its samples, and what the agent produced. */
export function ScenarioCard({
    scenario,
    versionId,
    unstable,
}: {
    scenario: ScenarioRunResult;
    /** The agent version the run is of, and the agent's unstable checks: a check unstable in that version is marked. */
    versionId: string;
    unstable: UnstableCheck[];
}) {
    return (
        <Card withBorder padding="md">
            <Stack gap="sm">
                <Title order={3}>{scenario.key}</Title>

                {scenario.samples.map((sample) => (
                    <Stack key={sample.sample} gap="xs">
                        {sample.status === "Failed" && (
                            <Alert color="red" title={`Sample ${sample.sample}: the agent returned nothing`}>
                                {sample.error}
                            </Alert>
                        )}
                        {(sample.undeclared?.length ?? 0) > 0 && (
                            <Alert color="yellow" title={`Sample ${sample.sample}: the agent returned what its version does not declare`}>
                                {sample.undeclared?.join(", ")}
                            </Alert>
                        )}
                    </Stack>
                ))}

                {scenario.checks.length === 0 && <Text c="dimmed">No checks.</Text>}
                {scenario.checks.map((check) => {
                    const unstableCheck = findUnstable(unstable, versionId, scenario.key, check.key, check.revision);
                    return (
                        <Stack key={check.key} gap={4}>
                            <Group gap="xs">
                                <Badge color={check.passed ? "green" : "red"}>{check.passed ? "passed" : "failed"}</Badge>
                                {check.regression != null && check.regression.state !== "Passed" && (
                                    <StateBadge state={check.regression.state} variant="light">
                                        {stateLabel(check.regression.state)}
                                    </StateBadge>
                                )}
                                <CheckHistory check={check} />
                                {notJudgedCount(check) > 0 && (
                                    <Badge color="yellow" variant="light">
                                        {`not judged: ${notJudgedCount(check)} of ${check.samples.length} samples`}
                                    </Badge>
                                )}
                                <Text fw={600}>{check.key}</Text>
                                {check.judgedByModel && <ModelJudgedIcon />}
                                {unstableCheck !== undefined && <UnstableCheckIcon label={unstableTooltip(unstableCheck)} />}
                                <CheckDefinitionButton
                                    definition={check.definition}
                                    revision={check.revision}
                                    latestRevision={check.latestRevision}
                                />
                                <Text c="dimmed">{checkSummary(check)}</Text>
                            </Group>
                            <List size="sm" listStyleType="none">
                                {check.samples
                                    .filter((sample) => !sample.passed || check.samples.length === 1)
                                    .map((sample) => (
                                        <List.Item key={sample.sample}>
                                            {check.samples.length > 1 ? `sample ${sample.sample}: ` : ""}
                                            {check.judgedByModel && !sample.notJudged ? (
                                                <Text span fs="italic" inherit>
                                                    {sample.message}
                                                </Text>
                                            ) : (
                                                sample.message
                                            )}
                                        </List.Item>
                                    ))}
                            </List>
                        </Stack>
                    );
                })}

                <Accordion multiple variant="contained">
                    {scenario.samples.map((sample) => (
                        <Accordion.Item key={sample.sample} value={String(sample.sample)}>
                            <Accordion.Control>
                                Agent output, sample {sample.sample}
                                {sample.durationMs != null && ` · ${formatSeconds(sample.durationMs)}`}
                            </Accordion.Control>
                            <Accordion.Panel>
                                <ConversationView items={sample.output ?? []} highlight={failedEvidenceKeys(scenario, sample.sample)} />
                            </Accordion.Panel>
                        </Accordion.Item>
                    ))}
                </Accordion>
            </Stack>
        </Card>
    );
}
