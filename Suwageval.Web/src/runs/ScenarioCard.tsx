import { useEffect, useState } from "react";
import { Accordion, Alert, Badge, Card, Group, List, Stack, Text, Title } from "@mantine/core";

import { CheckDefinitionButton } from "../checks/CheckDefinitionButton";
import { ModelJudgedIcon } from "../checks/ModelJudgedIcon";
import { UnstableCheckIcon } from "../checks/UnstableCheckIcon";
import type { ScenarioRunResult, UnstableCheck } from "./api";
import { ConversationView } from "./ConversationView";
import { ScenarioDefinitionButton } from "./ScenarioDefinitionButton";
import { CheckHistory } from "./CheckHistory";
import { stateLabel } from "./regression";
import { StateBadge } from "./StateBadge";
import { findUnstable, unstableTooltip } from "./stability";
import { checkAnchor, checkSummary, failedEvidenceKeys, notJudgedCount, samplesToOpen } from "./results";
import { isSimulated, sampleTitle } from "./simulation";

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
    const [openSamples, setOpenSamples] = useState<string[]>([]);
    // A link to one of this scenario's checks (`#check-…`) opens the outputs it is about, so the answer is right below.
    useEffect(() => {
        const openFromHash = () => {
            const target = scenario.checks.find((check) => `#${checkAnchor(scenario.key, check.key)}` === window.location.hash);
            if (target !== undefined) setOpenSamples((open) => [...new Set([...open, ...samplesToOpen(target)])]);
        };
        openFromHash();
        window.addEventListener("hashchange", openFromHash);
        return () => window.removeEventListener("hashchange", openFromHash);
    }, [scenario]);
    return (
        <Card withBorder padding="md">
            <Stack gap="sm">
                <Group gap="xs">
                    <Title order={3}>{scenario.key}</Title>
                    <ScenarioDefinitionButton scenarioKey={scenario.key} revision={scenario.revision} yaml={scenario.definitionYaml} />
                </Group>

                {scenario.samples.map((sample) => (
                    <Stack key={sample.sample} gap="xs">
                        {sample.status === "Failed" && (
                            <Alert color="red" title={`Sample ${sample.sample}: the agent returned nothing`}>
                                {sample.error}
                            </Alert>
                        )}
                        {sample.status === "NotSimulated" && (
                            <Alert color="orange" title={`Sample ${sample.sample}: the simulated user could not be played`}>
                                {sample.error} Not the agent's doing: run it again.
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
                        <Stack key={check.key} gap={4} id={checkAnchor(scenario.key, check.key)} className="check-target">
                            <Group justify="space-between" wrap="nowrap" align="flex-start">
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
                                    <Text c="dimmed">{checkSummary(check)}</Text>
                                </Group>
                                {/* The marks and the definition sit at the right edge, apart from the check's text. */}
                                <Group gap={4} wrap="nowrap">
                                    {check.judgedByModel && <ModelJudgedIcon model={check.judgeModel} />}
                                    {unstableCheck !== undefined && <UnstableCheckIcon label={unstableTooltip(unstableCheck)} />}
                                    <CheckDefinitionButton
                                        definition={check.definition}
                                        revision={check.revision}
                                        latestRevision={check.latestRevision}
                                    />
                                </Group>
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

                <Accordion multiple variant="contained" value={openSamples} onChange={setOpenSamples}>
                    {scenario.samples.map((sample) => (
                        <Accordion.Item key={sample.sample} value={String(sample.sample)}>
                            <Accordion.Control>{sampleTitle(sample)}</Accordion.Control>
                            <Accordion.Panel>
                                <ConversationView
                                    items={sample.output ?? []}
                                    highlight={failedEvidenceKeys(scenario, sample.sample)}
                                    simulated={isSimulated(sample)}
                                />
                            </Accordion.Panel>
                        </Accordion.Item>
                    ))}
                </Accordion>
            </Stack>
        </Card>
    );
}
