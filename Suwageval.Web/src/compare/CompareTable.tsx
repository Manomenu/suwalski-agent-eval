import { Anchor, Badge, Button, Group, Paper, Stack, Switch, Table, Text } from "@mantine/core";
import { useState } from "react";

import { shortId, versionLabel } from "../agents/view";
import { formatTime, percent } from "../runs/results";
import type { Comparison } from "./api";
import {
    alignRows,
    cellText,
    cellTone,
    checksPassedText,
    delta,
    deltaText,
    tagRows,
    trend,
    trendColor,
    trendMarker,
    visibleRows,
    type CheckCell,
} from "./compare";
import { COLUMN_MIN_WIDTH, LABEL_COLUMN_WIDTH, tableMinWidth } from "./layout";

/** One table, so the columns line up: a header per run, the pass rate per tag, then the checks. */
export function CompareTable({ comparison, firstBaseline }: { comparison: Comparison; firstBaseline: string }) {
    const { runs, versions, tags } = comparison;
    const [baselineId, setBaselineId] = useState(firstBaseline);
    const [onlyDifferences, setOnlyDifferences] = useState(true);
    const [hideUncomparable, setHideUncomparable] = useState(false);

    const columns = runs.length;
    const baseline = Math.max(
        0,
        runs.findIndex((results) => results.run.id === baselineId),
    );
    const rows = visibleRows(alignRows(runs), { onlyDifferences, hideUncomparable });

    return (
        <Stack gap="md">
            <Paper withBorder p="md">
                <Table.ScrollContainer minWidth={tableMinWidth(columns)}>
                    <Table layout="fixed" verticalSpacing="xs">
                        <Table.Thead>
                            <Table.Tr>
                                <Table.Th w={LABEL_COLUMN_WIDTH} />
                                {runs.map(({ run }, column) => {
                                    const version = versions.find((candidate) => candidate.id === run.agentVersionId);
                                    const isBaseline = column === baseline;
                                    return (
                                        <Table.Th
                                            key={run.id}
                                            miw={COLUMN_MIN_WIDTH}
                                            onClick={() => {
                                                setBaselineId(run.id);
                                            }}
                                            style={{ cursor: isBaseline ? "default" : "pointer", verticalAlign: "top" }}
                                        >
                                            <Stack gap={2}>
                                                <Anchor href={`/runs/${run.id}`}>Run {shortId(run.id)}</Anchor>
                                                <Text size="xs" c="dimmed" fw="normal">
                                                    version {version === undefined ? shortId(run.agentVersionId) : versionLabel(version)}
                                                </Text>
                                                <Text size="xs" fw="normal">
                                                    {version?.modelName ?? "unknown model"}
                                                </Text>
                                                <Text size="xs" c="dimmed" fw="normal">
                                                    {formatTime(run.createdAt)}
                                                </Text>
                                                <Text size="xs" fw="normal">
                                                    {checksPassedText(run)}
                                                </Text>
                                                {isBaseline ? (
                                                    <Badge variant="filled" w="fit-content">
                                                        Baseline
                                                    </Badge>
                                                ) : (
                                                    <Button
                                                        size="compact-xs"
                                                        variant="light"
                                                        w="fit-content"
                                                        aria-label={`Use run ${shortId(run.id)} as the baseline`}
                                                    >
                                                        Use as baseline
                                                    </Button>
                                                )}
                                            </Stack>
                                        </Table.Th>
                                    );
                                })}
                            </Table.Tr>
                        </Table.Thead>
                        <Table.Tbody>
                            <Table.Tr>
                                <Table.Th colSpan={columns + 1} bg="var(--mantine-color-default-hover)">
                                    Pass rate per tag
                                </Table.Th>
                            </Table.Tr>
                            {tagRows(runs, tags).map((row) => (
                                <Table.Tr key={row.tag ?? "\u0000all"}>
                                    <Table.Td fw={500}>{row.tag ?? "All"}</Table.Td>
                                    {row.rates.map((rate, column) => {
                                        const change = column === baseline ? null : delta(rate, row.rates[baseline] ?? null);
                                        return (
                                            <Table.Td key={runs[column]?.run.id}>
                                                <Group gap="xs" wrap="nowrap">
                                                    <Text>{rate === null ? "—" : percent(rate)}</Text>
                                                    {change !== null && (
                                                        <Text size="sm" c={trendColor(change.trend)}>
                                                            {deltaText(change)}
                                                        </Text>
                                                    )}
                                                </Group>
                                            </Table.Td>
                                        );
                                    })}
                                </Table.Tr>
                            ))}
                        </Table.Tbody>
                        <Table.Tbody>
                            <Table.Tr>
                                <Table.Th colSpan={columns + 1} bg="var(--mantine-color-default-hover)">
                                    <Group justify="space-between">
                                        Checks
                                        <Group gap="lg">
                                            <Switch
                                                label="Only differences"
                                                checked={onlyDifferences}
                                                onChange={(event) => {
                                                    setOnlyDifferences(event.currentTarget.checked);
                                                }}
                                            />
                                            <Switch
                                                label="Hide uncomparable"
                                                checked={hideUncomparable}
                                                onChange={(event) => {
                                                    setHideUncomparable(event.currentTarget.checked);
                                                }}
                                            />
                                        </Group>
                                    </Group>
                                </Table.Th>
                            </Table.Tr>
                            {rows.map((row) => (
                                <Table.Tr key={JSON.stringify([row.scenarioKey, row.checkKey])}>
                                    <Table.Td>
                                        <Text size="xs" c="dimmed">
                                            {row.scenarioKey}
                                        </Text>
                                        <Text>{row.checkKey}</Text>
                                    </Table.Td>
                                    {row.cells.map((cell, column) => (
                                        <CheckCellView key={runs[column]?.run.id} cell={cell} baseline={row.cells[baseline] ?? null} />
                                    ))}
                                </Table.Tr>
                            ))}
                        </Table.Tbody>
                    </Table>
                </Table.ScrollContainer>
                {rows.length === 0 && (
                    <Text c="dimmed" mt="sm">
                        No check to show — switch off Only differences or Hide uncomparable to see more.
                    </Text>
                )}
            </Paper>
        </Stack>
    );
}

function CheckCellView({ cell, baseline }: { cell: CheckCell | null; baseline: CheckCell | null }) {
    const change = trend(cell, baseline);
    const { color, emphasised } = cellTone(cell, change);
    const text = cellText(cell);
    return (
        <Table.Td bg={`var(--mantine-color-${color}-light)`} bd={emphasised ? `2px solid var(--mantine-color-${color}-filled)` : undefined}>
            <Group gap={6} wrap="nowrap" align="baseline">
                <Text fw={emphasised ? 700 : 400}>{text.rate}</Text>
                <Text size="xs" c="dimmed">
                    {text.samples}
                </Text>
                <Text size="sm" fw={700} c={trendColor(change)}>
                    {trendMarker(change)}
                </Text>
            </Group>
        </Table.Td>
    );
}
