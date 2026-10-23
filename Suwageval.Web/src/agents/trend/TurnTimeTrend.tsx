import { Group, Paper, SegmentedControl, Stack, Text, Title } from "@mantine/core";
import { useState } from "react";

import type { RunPassRates } from "../api";
import { HeaderHelp } from "../../runs/HeaderHelp";
import { TrendChart } from "./TrendChart";
import { TREND_MIN_RUNS, trendRows, trendSeries, turnTimeMeasure, withTurnTimes, type TurnTimeStat } from "./trend";

/** What the chart shows, said on hover over its heading. */
const TURN_TIME_HELP =
    "How long the agent took to answer one turn, measured by Suwageval around the call (the network included), per finished run: the median or the 95th percentile of its samples that made a call. The series follow the rules of the pass-rate chart: All comes only from runs of every scenario; a tag's line also takes runs limited to tags including it. Click a point to open the run. Lower is better.";

const STATS: { value: TurnTimeStat; label: string }[] = [
    { value: "median", label: "Median" },
    { value: "p95", label: "p95" },
];

/** The turn time over the agent's finished runs, like the pass-rate chart: all scenarios and one line per tag, median or p95. */
export function TurnTimeTrend({ points }: { points: RunPassRates[] }) {
    const [stat, setStat] = useState<TurnTimeStat>("median");
    const measured = withTurnTimes(points);

    return (
        <Paper withBorder p="md">
            <Stack gap="sm">
                <Group justify="space-between">
                    <Title order={3}>
                        <HeaderHelp label="Turn time" help={TURN_TIME_HELP} />
                    </Title>
                    <SegmentedControl
                        size="xs"
                        aria-label="Turn time statistic"
                        data={STATS}
                        value={stat}
                        onChange={(value) => {
                            setStat(value === "p95" ? "p95" : "median");
                        }}
                    />
                </Group>
                {measured.length < TREND_MIN_RUNS ? (
                    <Text c="dimmed">The trend appears after two finished runs that called the agent.</Text>
                ) : (
                    <TrendChart
                        points={measured}
                        rows={trendRows(measured, turnTimeMeasure(stat))}
                        names={trendSeries(measured)}
                        format={(value) => `${value.toFixed(1)} s`}
                        domain={[0, "auto"]}
                    />
                )}
            </Stack>
        </Paper>
    );
}
