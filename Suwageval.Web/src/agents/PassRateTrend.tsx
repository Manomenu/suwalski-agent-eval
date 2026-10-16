import { LineChart } from "@mantine/charts";
import { Button, Group, Paper, Stack, Text, Title } from "@mantine/core";
import { useState } from "react";

import type { RunPassRates } from "./api";
import {
    ALL_SERIES,
    DETAILS_KEY,
    LABEL_KEY,
    RUN_KEY,
    TREND_MIN_RUNS,
    asTrendRow,
    seriesColor,
    toggleHidden,
    trendRows,
    trendSeries,
} from "./trend";
import { runHref } from "./view";

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
                <Title order={3}>Pass rate</Title>
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
                            valueFormatter={(value) => `${String(value)}%`}
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
                                            {shown.map((name) => (
                                                <Text key={name} size="sm" c={seriesColor(name, names)}>
                                                    {name}: {row[name] === null || row[name] === undefined ? "—" : `${String(row[name])}%`}
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
                                    color={seriesColor(name, names)}
                                    variant={hidden.has(name) ? "subtle" : "light"}
                                    aria-pressed={!hidden.has(name)}
                                    onClick={() => {
                                        setHidden(toggleHidden(hidden, name));
                                    }}
                                >
                                    {name}
                                </Button>
                            ))}
                        </Group>
                    </>
                )}
            </Stack>
        </Paper>
    );
}
