import { Anchor, Paper, Stack, Table, Text, Title } from "@mantine/core";

import { formatTime, variantsGroupHref } from "../runs/results";
import type { RunGroupSummary } from "./api";
import { HeaderHelp } from "../runs/HeaderHelp";

const GROUPS_HELP =
    "Runs started with `suwageval run --group <name>`: the runs of one sweep, typically one per model or configuration. Open a group to see its runs side by side as variants.";
const GROUPS_TABLE_MIN_WIDTH = 500;

/** The agent's run groups: each opens the variants page of its runs. */
export function RunGroupsSection({ agentName, groups }: { agentName: string; groups: RunGroupSummary[] }) {
    return (
        <Stack gap="sm">
            <Title order={3}>
                <HeaderHelp label="Groups" help={GROUPS_HELP} />
            </Title>
            {groups.length === 0 ? (
                <Text c="dimmed">
                    No run is in a group yet — start runs with <code>suwageval run --group &lt;name&gt;</code> to compare them side by side
                    here.
                </Text>
            ) : (
                <Paper withBorder p="md">
                    <Table.ScrollContainer minWidth={GROUPS_TABLE_MIN_WIDTH}>
                        <Table highlightOnHover>
                            <Table.Thead>
                                <Table.Tr>
                                    <Table.Th>Group</Table.Th>
                                    <Table.Th>Runs</Table.Th>
                                    <Table.Th>Versions</Table.Th>
                                    <Table.Th>Newest run</Table.Th>
                                </Table.Tr>
                            </Table.Thead>
                            <Table.Tbody>
                                {groups.map((group) => (
                                    <Table.Tr key={group.name}>
                                        <Table.Td className="wrap-key">
                                            <Anchor href={variantsGroupHref(group.name, agentName)}>{group.name}</Anchor>
                                        </Table.Td>
                                        <Table.Td>{group.runs}</Table.Td>
                                        <Table.Td>{group.versions}</Table.Td>
                                        <Table.Td>{formatTime(group.newestRunAt)}</Table.Td>
                                    </Table.Tr>
                                ))}
                            </Table.Tbody>
                        </Table>
                    </Table.ScrollContainer>
                </Paper>
            )}
        </Stack>
    );
}
