import { Accordion, Alert, Badge, Card, Group, List, Stack, Text, Title } from "@mantine/core";

import type { ScenarioRunResult } from "./api";
import { ConversationView } from "./ConversationView";
import { checkSummary, failedEvidenceKeys, notJudgedCount, num } from "./results";

/** One scenario: its checks with verdicts, the warnings of its samples, and what the agent produced. */
export function ScenarioCard({ scenario }: { scenario: ScenarioRunResult }) {
    return (
        <Card withBorder padding="md">
            <Stack gap="sm">
                <Title order={3}>{scenario.key}</Title>

                {scenario.samples.map((sample) => (
                    <Stack key={sample.sample} gap="xs">
                        {sample.status === "Failed" && (
                            <Alert color="red" title={`Sample ${num(sample.sample)}: the agent returned nothing`}>
                                {sample.error}
                            </Alert>
                        )}
                        {(sample.undeclared?.length ?? 0) > 0 && (
                            <Alert
                                color="yellow"
                                title={`Sample ${num(sample.sample)}: the agent returned what its version does not declare`}
                            >
                                {sample.undeclared?.join(", ")}
                            </Alert>
                        )}
                    </Stack>
                ))}

                {scenario.checks.length === 0 && <Text c="dimmed">No checks.</Text>}
                {scenario.checks.map((check) => (
                    <Stack key={check.key} gap={4}>
                        <Group gap="xs">
                            <Badge color={check.passed ? "green" : "red"}>{check.passed ? "passed" : "failed"}</Badge>
                            {notJudgedCount(check) > 0 && (
                                <Badge color="yellow" variant="light">
                                    {`not judged: ${notJudgedCount(check)} of ${check.samples.length} samples`}
                                </Badge>
                            )}
                            <Text fw={600}>{check.key}</Text>
                            <Text c="dimmed">{checkSummary(check)}</Text>
                        </Group>
                        <List size="sm" listStyleType="none">
                            {check.samples
                                .filter((sample) => !sample.passed || check.samples.length === 1)
                                .map((sample) => (
                                    <List.Item key={sample.sample}>
                                        {check.samples.length > 1 ? `sample ${num(sample.sample)}: ` : ""}
                                        {sample.message}
                                    </List.Item>
                                ))}
                        </List>
                    </Stack>
                ))}

                <Accordion multiple variant="contained">
                    {scenario.samples.map((sample) => (
                        <Accordion.Item key={sample.sample} value={String(num(sample.sample))}>
                            <Accordion.Control>Agent output, sample {num(sample.sample)}</Accordion.Control>
                            <Accordion.Panel>
                                <ConversationView
                                    items={sample.output ?? []}
                                    highlight={failedEvidenceKeys(scenario, num(sample.sample))}
                                />
                            </Accordion.Panel>
                        </Accordion.Item>
                    ))}
                </Accordion>
            </Stack>
        </Card>
    );
}
