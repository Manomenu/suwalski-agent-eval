import { Anchor, Badge, Button, Group, Paper, Stack, Switch, Table, Text, Tooltip } from "@mantine/core";
import { useState } from "react";

import { dataText } from "../agents/view";
import { CheckDefinitionButton } from "../checks/CheckDefinitionButton";
import { ModelJudgedIcon } from "../checks/ModelJudgedIcon";
import { UnstableCheckIcon } from "../checks/UnstableCheckIcon";
import { ChecksCount } from "../runs/RunCounts";
import { filteredLabel, keepsTags, selectedTagRows } from "../tagFilter/tagSelection";
import { durationSummary, formatTime, percent, RUN_CHECKS_HELP, runHref, runLabel, shortId } from "../runs/results";
import { unstableInRuns, unstableRowTooltip } from "../runs/stability";
import { useUnstableChecks } from "../runs/useUnstableChecks";
import type { AgentVersion } from "../agents/api";
import type { ComparedCell, ComparedRun, RunComparison } from "./api";
import { cellText, cellTone, columnChecksCounts, deltaText, TIME_DELTA_HELP, timeDeltaText, trendColor, trendMarker } from "./compare";
import { COLUMN_MIN_WIDTH, LABEL_COLUMN_WIDTH, tableMinWidth } from "./layout";

const TIME_HELP_WIDTH = 280;

/** The median and p95 change of a run's turn time against the baseline's, as the server computed them; none for the baseline. */
function timeChanges({ medianChange, p95Change }: ComparedRun) {
    return [
        ...(medianChange == null ? [] : [{ label: "median", change: medianChange }]),
        ...(p95Change == null ? [] : [{ label: "p95", change: p95Change }]),
    ];
}

/** One table, so the columns line up: a header per run, the pass rate per tag, then the checks. The first run is the baseline. */
export function CompareTable({
    comparison,
    versions,
    onBaseline,
    selectedTags,
}: {
    comparison: RunComparison;
    /** The tags the view is narrowed to; none means every scenario. */
    selectedTags: readonly string[];
    versions: readonly AgentVersion[];
    /** The run to ask the comparison for again with that run first. */
    onBaseline: (runId: string) => void;
}) {
    const { runs } = comparison;
    const filtered = selectedTags.length > 0;
    const tags = selectedTagRows(comparison.tags, selectedTags);
    const [onlyDifferences, setOnlyDifferences] = useState(true);
    const [hideUncomparable, setHideUncomparable] = useState(false);

    // The checks that are unstable in the version of any compared run.
    const runVersions = runs.map((compared) => compared.run.agentVersionId);
    const versionIds = [...new Set(runVersions)].sort();
    const unstable = useUnstableChecks(runs[0]?.run.agent ?? null, versionIds, true);

    const columns = runs.length;
    const tagged = comparison.checks.filter((row) => keepsTags(row.scenarioTags, selectedTags));
    const rows = tagged.filter((row) => (!onlyDifferences || row.differs) && (!hideUncomparable || row.comparable));

    return (
        <Stack gap="md">
            <Paper withBorder p="md">
                <Table.ScrollContainer minWidth={tableMinWidth(columns)}>
                    <Table layout="fixed" verticalSpacing="xs">
                        <Table.Thead>
                            <Table.Tr>
                                <Table.Th w={LABEL_COLUMN_WIDTH} />
                                {runs.map((compared, column) => {
                                    const { run } = compared;
                                    const version = versions.find((candidate) => candidate.id === run.agentVersionId);
                                    const isBaseline = column === 0;
                                    return (
                                        <Table.Th
                                            key={run.id}
                                            miw={COLUMN_MIN_WIDTH}
                                            onClick={() => {
                                                onBaseline(run.id);
                                            }}
                                            style={{ cursor: isBaseline ? "default" : "pointer", verticalAlign: "top" }}
                                        >
                                            <Stack gap={2}>
                                                <Anchor href={runHref(run.id)}>Run {shortId(run.id)}</Anchor>
                                                <Text size="xs" c="dimmed" fw="normal">
                                                    version {runLabel(run, version?.label)}
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
                                                <Tooltip label={RUN_CHECKS_HELP} multiline w={TIME_HELP_WIDTH}>
                                                    <Text size="xs" fw="normal" w="fit-content" style={{ cursor: "help" }}>
                                                        {filteredLabel("Checks", selectedTags)}:{" "}
                                                        <ChecksCount run={filtered ? columnChecksCounts(tagged, column) : run} />
                                                    </Text>
                                                </Tooltip>
                                                <Text size="xs" fw="normal">
                                                    {durationSummary(compared, filtered ? "Turn time (all scenarios)" : "Turn time") ??
                                                        "Turn time: —"}
                                                </Text>
                                                <RunJudge compared={compared} baselineModel={runs[0]?.judgeModel ?? null} />
                                                {!isBaseline && (
                                                    <Tooltip label={TIME_DELTA_HELP} multiline w={TIME_HELP_WIDTH}>
                                                        <Group gap="xs" wrap="nowrap" w="fit-content" style={{ cursor: "help" }}>
                                                            {timeChanges(compared).map(({ label, change }) => (
                                                                <Text key={label} size="xs" fw="normal" c={trendColor(change.trend)}>
                                                                    {label} {timeDeltaText(change)}
                                                                </Text>
                                                            ))}
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
                            {tags.map((row) => (
                                <Table.Tr key={row.tag ?? "\u0000all"}>
                                    <Table.Td fw={500}>{row.tag ?? "All"}</Table.Td>
                                    {row.rates.map(({ passRate, change }, column) => (
                                        <Table.Td key={runs[column]?.run.id}>
                                            <Group gap="xs" wrap="nowrap">
                                                <Text>{passRate == null ? "—" : percent(passRate)}</Text>
                                                {change != null && (
                                                    <Text size="sm" c={trendColor(change.trend)}>
                                                        {deltaText(change)}
                                                    </Text>
                                                )}
                                            </Group>
                                        </Table.Td>
                                    ))}
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
                                // The definition and its revisions are the baseline's, or the first run's that has the check.
                                const shown = row.cells.find(Boolean);
                                const unstableIn = unstableInRuns(unstable, runVersions, row.cells, row.scenarioKey, row.checkKey);
                                return (
                                    <Table.Tr key={JSON.stringify([row.scenarioKey, row.checkKey])}>
                                        <Table.Td>
                                            <Text size="xs" c="dimmed" className="wrap-key">
                                                {row.scenarioKey}
                                            </Text>
                                            <Group justify="space-between" wrap="nowrap">
                                                <Text className="wrap-key">{row.checkKey}</Text>
                                                {/* The marks and the definition sit at the right edge of the cell, apart from the check's key. */}
                                                <Group gap={4} wrap="nowrap">
                                                    {row.judgedByModel && <ModelJudgedIcon />}
                                                    {unstableIn.length > 0 && <UnstableCheckIcon label={unstableRowTooltip(unstableIn)} />}
                                                    {shown != null && (
                                                        <CheckDefinitionButton
                                                            definition={row.definition}
                                                            revision={shown.revision}
                                                            latestRevision={shown.latestRevision}
                                                        />
                                                    )}
                                                </Group>
                                            </Group>
                                        </Table.Td>
                                        {row.cells.map((cell, column) => (
                                            <CheckCellView key={runs[column]?.run.id} cell={cell} />
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

function CheckCellView({ cell }: { cell: ComparedCell | null }) {
    const change = cell?.trend;
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

/** The run's judge, once per run: the robot with the model's name, and a warning when it is not the baseline's judge. */
function RunJudge({ compared, baselineModel }: { compared: ComparedRun; baselineModel: string | null }) {
    const model = compared.judgeModel;
    if (model == null) {
        return null;
    }
    return (
        <Group gap={4} wrap="nowrap">
            <ModelJudgedIcon model={model} />
            <Text size="xs" fw="normal">
                judge {model}
            </Text>
            {compared.judgeChanged && (
                <Tooltip
                    label={`The baseline was judged by ${baselineModel ?? ""}: a different verdict may come from the judge, not the agent`}
                    multiline
                    w={260}
                >
                    <Badge size="xs" variant="light" color="orange" style={{ cursor: "help" }}>
                        other judge
                    </Badge>
                </Tooltip>
            )}
        </Group>
    );
}
