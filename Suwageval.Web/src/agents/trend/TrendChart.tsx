import { LineChart } from "@mantine/charts";
import { ColorSwatch, Group, Paper, Stack, Text } from "@mantine/core";

import {
    ALL_SERIES,
    DETAILS_KEY,
    LABEL_KEY,
    RECHECK_KEY,
    RUN_KEY,
    asTrendRow,
    hasRechecks,
    legendColor,
    measuredSeries,
    seriesColor,
    shownSeries,
    versionChanges,
    type TrendRow,
} from "./trend";
import { runHref } from "../../routes/links";
import { type RunPassRates } from "./api";

/** Said under every trend chart: group runs are experiments and never make a point. */
const GROUP_RUNS_NOT_INCLUDED = "Runs started in a group are experiments and are not included in this chart.";

const TREND_HEIGHT = 300;
const ALL_STROKE_WIDTH = 4;
const TAG_STROKE_WIDTH = 2;

/**
 * The line chart the trend cards share: "All" and the picked tags' lines (every tag's with none picked) with their legend, a
 * tooltip of the series the run measured, a click that opens the run. `format` writes a value with its unit; `domain` is the y axis's.
 */
export function TrendChart({
    points,
    rows,
    names,
    picked,
    format,
    domain,
}: {
    points: RunPassRates[];
    rows: TrendRow[];
    names: string[];
    /** The tags picked above the charts; none: every tag. */
    picked: readonly string[];
    format: (value: number) => string;
    domain: [number, number | "auto"];
}) {
    // The colours follow `names`, so a line keeps its colour whatever else is picked.
    const shown = shownSeries(names, picked);
    // The x axis is keyed by run (two runs can share a minute); its ticks show the run's short date.
    const labelOf = new Map(rows.map((row) => [row[RUN_KEY], row[LABEL_KEY]]));
    const changes = versionChanges(points, rows);
    const newVersionOf = new Map(changes.map((change) => [change.runId, change.label]));

    return (
        <Stack gap="sm">
            <LineChart
                h={TREND_HEIGHT}
                data={rows}
                dataKey={RUN_KEY}
                xAxisProps={{ tickFormatter: (runId: string) => String(labelOf.get(runId) ?? "") }}
                referenceLines={changes.map((change) => ({
                    x: change.runId,
                    label: change.label,
                    labelPosition: "insideTopLeft",
                    color: "gray.6",
                    strokeDasharray: "4 4",
                }))}
                series={shown.map((name) => ({ name, color: seriesColor(name, names) }))}
                curveType="linear"
                connectNulls
                withDots
                withLegend={false}
                valueFormatter={format}
                yAxisProps={{ domain }}
                lineProps={(series) => ({ strokeWidth: series.name === ALL_SERIES ? ALL_STROKE_WIDTH : TAG_STROKE_WIDTH })}
                lineChartProps={{
                    onClick: (state) => {
                        const row = rows[Number(state.activeIndex)];
                        const runId = row?.[RUN_KEY];
                        if (typeof runId === "string") window.location.assign(runHref(runId));
                    },
                }}
                tooltipProps={{
                    content: ({ payload }) => {
                        const row = asTrendRow(payload[0]?.payload);
                        if (!row) return null;
                        return (
                            <Paper withBorder shadow="sm" p="xs">
                                <Text fw={500}>{row[LABEL_KEY]}</Text>
                                <Text size="xs" c="dimmed">
                                    {row[DETAILS_KEY]}
                                </Text>
                                {typeof row[RUN_KEY] === "string" && newVersionOf.has(row[RUN_KEY]) && (
                                    <Text size="xs" fw={600}>
                                        New agent version from this run: {newVersionOf.get(row[RUN_KEY])}
                                    </Text>
                                )}
                                {typeof row[RECHECK_KEY] === "string" && (
                                    <Text size="xs" c="dimmed">
                                        Re-checked: these are the results of its latest re-check
                                    </Text>
                                )}
                                {measuredSeries(row, shown).map((name) => (
                                    <Text key={name} size="sm" c={seriesColor(name, names)}>
                                        {name}: {format(Number(row[name]))}
                                    </Text>
                                ))}
                            </Paper>
                        );
                    },
                }}
            />
            <Group gap="md" role="list" aria-label="Series">
                {shown.map((name) => (
                    <Group key={name} gap={6} wrap="nowrap" role="listitem">
                        <ColorSwatch color={`var(--mantine-color-${legendColor(name, names)}-5)`} size={10} withShadow={false} />
                        <Text size="sm">{name}</Text>
                    </Group>
                ))}
            </Group>
            <Text size="xs" c="dimmed">
                {GROUP_RUNS_NOT_INCLUDED}
            </Text>
            {hasRechecks(points) && (
                <Text size="xs" c="dimmed">
                    A point of a re-checked run shows the results of its latest re-check; the run keeps its place and time.
                </Text>
            )}
        </Stack>
    );
}
