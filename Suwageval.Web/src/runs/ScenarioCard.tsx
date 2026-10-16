import { Accordion, Alert, Badge, Card, Group, List, Stack, Text, Title } from "@mantine/core";

import type { ScenarioRunResult } from "./api";
import { ConversationView } from "./ConversationView";
import { checkSummary, failedEvidenceKeys, num } from "./results";

/** One scenario: its checks with verdicts, the warnings of its samples, and what the agent produced. */
export function ScenarioCard({ scenario }: { scenario: ScenarioRunResult }) {
    return (
        <Card withBorder padding="md">
            <Stack gap="sm">
                <Title order={3}>{scenario.key}</Title>

                {scenario.samples.map((sample) => (
                    <Stack key={sample.sample} gap="xs">
                        {sample.status === "Failed" && (
                            <Alert color="red" title={`Próbka ${num(sample.sample)}: agent nic nie zwrócił`}>
                                {sample.error}
                            </Alert>
                        )}
                        {(sample.undeclared?.length ?? 0) > 0 && (
                            <Alert color="yellow" title={`Próbka ${num(sample.sample)}: agent zwrócił to, czego jego wersja nie deklaruje`}>
                                {sample.undeclared?.join(", ")}
                            </Alert>
                        )}
                    </Stack>
                ))}

                {scenario.checks.length === 0 && <Text c="dimmed">Brak checków.</Text>}
                {scenario.checks.map((check) => (
                    <Stack key={check.key} gap={4}>
                        <Group gap="xs">
                            <Badge color={check.passed ? "green" : "red"}>{check.passed ? "zaliczony" : "niezaliczony"}</Badge>
                            <Text fw={600}>{check.key}</Text>
                            <Text c="dimmed">{checkSummary(check)}</Text>
                        </Group>
                        <List size="sm" listStyleType="none">
                            {check.samples
                                .filter((sample) => !sample.passed || check.samples.length === 1)
                                .map((sample) => (
                                    <List.Item key={sample.sample}>
                                        {check.samples.length > 1 ? `próbka ${num(sample.sample)}: ` : ""}
                                        {sample.message}
                                    </List.Item>
                                ))}
                        </List>
                    </Stack>
                ))}

                <Accordion multiple variant="contained">
                    {scenario.samples.map((sample) => (
                        <Accordion.Item key={sample.sample} value={String(num(sample.sample))}>
                            <Accordion.Control>Wynik agenta, próbka {num(sample.sample)}</Accordion.Control>
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
