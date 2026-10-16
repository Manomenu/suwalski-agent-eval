import { Anchor, Badge, Button, Group, Paper, Stack, Switch, Table, Text, Tooltip } from "@mantine/core";
import { useState } from "react";

import { dataText, shortId, versionLabel } from "../agents/view";
import { CheckDefinitionButton } from "../checks/CheckDefinitionButton";
import { judgedByModel } from "../checks/definition";
import { ModelJudgedIcon } from "../checks/ModelJudgedIcon";
import { UnstableCheckIcon } from "../checks/UnstableCheckIcon";
import { durationSummary, formatTime, num, percent, turnTimes } from "../runs/results";
import { unstableInRuns, unstableRowTooltip } from "../runs/stability";
import { useUnstableChecks } from "../runs/useUnstableChecks";
import type { ScenarioRunResult } from "../runs/api";
import type { Comparison } from "./api";
import {
    alignRows,
    cellText,
    cellTone,
    checkOfRow,
    checksPassedText,
    delta,
    deltaText,
    tagRows,
    TIME_DELTA_HELP,
    timeDelta,
    timeDeltaText,
    trend,
    trendColor,
    trendMarker,
    visibleRows,
    type CheckCell,
} from "./compare";
import { COLUMN_MIN_WIDTH, LABEL_COLUMN_WIDTH, tableMinWidth } from "./layout";

const TIME_HELP_WIDTH = 280;

/** The median and p95 change of a run's turn time against the baseline's; none when either run made no call. */
function timeChanges(scenarios: ScenarioRunResult[], baseline: ScenarioRunResult[]) {
    const times = turnTimes(scenarios);
    const base = turnTimes(baseline);
    const median = timeDelta(times?.medianMs ?? null, base?.medianMs ?? null);
    const p95 = timeDelta(times?.p95Ms ?? null, base?.p95Ms ?? null);
    return [...(median === null ? [] : [{ label: "median", change: median }]), ...(p95 === null ? [] : [{ label: "p95", change: p95 }])];
}

/** One table, so the columns line up: a header per run, the pass rate per tag, then the checks. */
export function CompareTable({ comparison, firstBaseline }: { comparison: Comparison; firstBaseline: string }) {
    const { runs, versions, tags } = comparison;
    const [baselineId, setBaselineId] = useState(firstBaseline);
    const [onlyDifferences, setOnlyDifferences] = useState(true);
    const [hideUncomparable, setHideUncomparable] = useState(false);

    // The checks that are unstable in the version of any compared run.
    const versionIds = [...new Set(runs.map((results) => results.run.agentVersionId))].sort();
    const unstable = useUnstableChecks(runs[0]?.run.agent ?? null, versionIds);

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
                                {runs.map((results, column) => {
                                    const { run } = results;
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
                                                    {version === undefined ? "unknown model" : version.modelNames.join(", ")}
                                                </Text>
                                                {version !== undefined && dataText(version) !== null && (
                                                    <Text size="xs" fw="normal">
                                                        {dataText(version)}
                                                    </Text>
                                                )}
                                                <Text size="xs" c="dimmed" fw="normal">
                                                    {formatTime(run.createdAt)}
                                                </Text>
                                                <Text size="xs" fw="normal">
                                                    {checksPassedText(run)}
                                                </Text>
                                                <Text size="xs" fw="normal">
                                                    {durationSummary(results.scenarios) ?? "Turn time: —"}
                                                </Text>
                                                {!isBaseline && (
                                                    <Tooltip label={TIME_DELTA_HELP} multiline w={TIME_HELP_WIDTH}>
                                                        <Group gap="xs" wrap="nowrap" w="fit-content" style={{ cursor: "help" }}>
                                                            {timeChanges(results.scenarios, runs[baseline]?.scenarios ?? []).map(
                                                                ({ label, change }) => (
                                                                    <Text key={label} size="xs" fw="normal" c={trendColor(change.trend)}>
                                                                        {label} {timeDeltaText(change)}
                                                                    </Text>
                                                                ),
                                                            )}
                                                        </Group>
                                                    </Tooltip>
                                                )}
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
                                <Table.Th colSpan={columns + 1} bg="var(--mantine-color-default)">
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
                                <Table.Th colSpan={columns + 1} bg="var(--mantine-color-default)">
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
                            {rows.map((row) => {
                                const check = checkOfRow(runs, row, baseline);
                                const unstableIn = unstableInRuns(unstable, runs, row.scenarioKey, row.checkKey);
                                return (
                                    <Table.Tr key={JSON.stringify([row.scenarioKey, row.checkKey])}>
                                        <Table.Td>
                                            <Text size="xs" c="dimmed">
                                                {row.scenarioKey}
                                            </Text>
                                            <Group gap={4} wrap="nowrap">
                                                <Text>{row.checkKey}</Text>
                                                {check !== null && judgedByModel(check.definition) && <ModelJudgedIcon />}
                                                {unstableIn.length > 0 && <UnstableCheckIcon label={unstableRowTooltip(unstableIn)} />}
                                                {check !== null && (
                                                    <CheckDefinitionButton
                                                        definition={check.definition}
                                                        revision={num(check.revision)}
                                                        latestRevision={num(check.latestRevision)}
                                                    />
                                                )}
                                            </Group>
                                        </Table.Td>
                                        {row.cells.map((cell, column) => (
                                            <CheckCellView key={runs[column]?.run.id} cell={cell} baseline={row.cells[baseline] ?? null} />
                                        ))}
                                    </Table.Tr>
                                );
                            })}
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
