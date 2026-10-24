import { Group, Paper, Stack, Table, Text, Title } from "@mantine/core";

import { branchesText, historyText, samplesText } from "./configuration";
import { HeaderHelp } from "../../help/HeaderHelp";
import { CONFIGURATION_HELP, REGRESSION_BRANCHES_HELP, REGRESSION_HISTORY_HELP, SAMPLES_HELP } from "./view";

/** What agent.yaml sets and a version does not hold: the endpoint, the default samples and the regression settings. */
export function ConfigurationSection({
    endpointUrl,
    samples,
    regression,
}: {
    endpointUrl: string;
    samples: number | null | undefined;
    regression: { branches?: readonly string[] | null; history?: number | null } | null | undefined;
}) {
    const sampleCount = samplesText(samples);
    const branches = branchesText(regression?.branches);
    const history = historyText(regression?.history);
    return (
        <Stack gap="sm">
            <Title order={3}>
                <HeaderHelp label="Configuration" help={CONFIGURATION_HELP} />
            </Title>
            <Paper withBorder p="md">
                <Table>
                    <Table.Tbody>
                        <Table.Tr>
                            <Table.Th>Endpoint</Table.Th>
                            <Table.Td className="wrap-key">{endpointUrl}</Table.Td>
                        </Table.Tr>
                        <Table.Tr>
                            <Table.Th>
                                <HeaderHelp label="Samples" help={SAMPLES_HELP} />
                            </Table.Th>
                            <Table.Td>
                                <Group gap={4}>
                                    <Text span inherit>
                                        {sampleCount.text}
                                    </Text>
                                    {sampleCount.isDefault && (
                                        <Text span inherit c="dimmed">
                                            (default)
                                        </Text>
                                    )}
                                </Group>
                            </Table.Td>
                        </Table.Tr>
                        <Table.Tr>
                            <Table.Th>
                                <HeaderHelp label="Regression branches" help={REGRESSION_BRANCHES_HELP} />
                            </Table.Th>
                            <Table.Td className="wrap-key">
                                <Text span inherit c={branches.isDefault ? "dimmed" : "inherit"}>
                                    {branches.text}
                                </Text>
                            </Table.Td>
                        </Table.Tr>
                        <Table.Tr>
                            <Table.Th>
                                <HeaderHelp label="Regression history" help={REGRESSION_HISTORY_HELP} />
                            </Table.Th>
                            <Table.Td>
                                <Group gap={4}>
                                    <Text span inherit>
                                        {history.text}
                                    </Text>
                                    {history.isDefault && (
                                        <Text span inherit c="dimmed">
                                            (default)
                                        </Text>
                                    )}
                                </Group>
                            </Table.Td>
                        </Table.Tr>
                    </Table.Tbody>
                </Table>
            </Paper>
        </Stack>
    );
}
