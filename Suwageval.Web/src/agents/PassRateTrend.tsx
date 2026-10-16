import { LineChart } from "@mantine/charts";
import { Button, ColorSwatch, Group, Paper, Stack, Text, Title } from "@mantine/core";
import { useState } from "react";

import type { RunPassRates } from "./api";
import {
    ALL_SERIES,
    DETAILS_KEY,
    LABEL_KEY,
    RECHECK_KEY,
    RUN_KEY,
    TREND_MIN_RUNS,
    asTrendRow,
    measuredSeries,
    hasRechecks,
    legendColor,
    seriesColor,
    toggleHidden,
    trendRows,
    trendSeries,
} from "./trend";
import { HeaderHelp } from "./HeaderHelp";
import { runHref } from "./view";

/** What the chart shows, said on hover over its heading. */
const PASS_RATE_HELP =
    "The share of checks that passed in each finished run, oldest to newest. A check counts once per scenario: passed when enough of its samples passed. All comes only from runs of every scenario; a tag's line also takes runs limited to that tag. A line joins the runs that measured it, so a point is a reading, the line between is not. A re-checked run shows its latest re-check. Click a point to open the run.";

const TREND_HEIGHT = 300;
const ALL_STROKE_WIDTH = 4;
const TAG_STROKE_WIDTH = 2;

/** The pass rate over the agent's finished runs: one line for all checks and one per tag. Clicking the chart opens the nearest run. */
export function PassRateTrend({ points }: { points: RunPassRates[] }) {
    // The series the user switched off through the legend.
    const [hidden, setHidden] = useState<ReadonlySet<string>>(new Set());
    const rows = trendRows(points);
    const names = trendSeries(points);
    const shown = names.filter((name) => !hidden.has(name));

    return (
        <Paper withBorder p="md">
            <Stack gap="sm">
                <Title order={3}>
                    <HeaderHelp label="Pass rate" help={PASS_RATE_HELP} />
                </Title>
                {points.length < TREND_MIN_RUNS ? (
                    <Text c="dimmed">The trend appears after two finished runs.</Text>
                ) : (
                    <>
                        <LineChart
                            h={TREND_HEIGHT}
                            data={rows}
                            dataKey={LABEL_KEY}
                            series={shown.map((name) => ({ name, color: seriesColor(name, names) }))}
                            curveType="linear"
                            connectNulls
                            withDots
                            withLegend={false}
                            unit="%"
                            yAxisProps={{ domain: [0, 100] }}
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
                                            {typeof row[RECHECK_KEY] === "string" && (
                                                <Text size="xs" c="dimmed">
                                                    Re-checked: these are the results of its latest re-check
                                                </Text>
                                            )}
                                            {measuredSeries(row, shown).map((name) => (
                                                <Text key={name} size="sm" c={seriesColor(name, names)}>
                                                    {name}: {String(row[name])}%
                                                </Text>
                                            ))}
                                        </Paper>
                                    );
                                },
                            }}
                        />
                        <Group gap="xs" aria-label="Series">
                            {names.map((name) => (
                                <Button
                                    key={name}
                                    size="compact-sm"
                                    variant={hidden.has(name) ? "subtle" : "default"}
                                    c={hidden.has(name) ? "dimmed" : "var(--mantine-color-text)"}
                                    leftSection={
                                        <ColorSwatch
                                            color={`var(--mantine-color-${legendColor(name, names)}-5)`}
                                            size={10}
                                            withShadow={false}
                                            opacity={hidden.has(name) ? 0.3 : 1}
                                        />
                                    }
                                    aria-pressed={!hidden.has(name)}
                                    onClick={() => {
                                        setHidden(toggleHidden(hidden, name));
                                    }}
                                >
                                    {name}
                                </Button>
                            ))}
                        </Group>
                        {hasRechecks(points) && (
                            <Text size="xs" c="dimmed">
                                A point of a re-checked run shows the results of its latest re-check; the run keeps its place and time.
                            </Text>
                        )}
                    </>
                )}
            </Stack>
        </Paper>
    );
}
