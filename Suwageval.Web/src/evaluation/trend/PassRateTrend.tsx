import { Paper, Stack, Text, Title } from "@mantine/core";

import { HeaderHelp } from "../../help/HeaderHelp";
import { percentText } from "../../charts/chartStyle";
import { TrendChart } from "./TrendChart";
import { TREND_MIN_RUNS, trendRows, trendSeries } from "./trend";
import { type RunPassRates } from "./api";

/** What the chart shows, said on hover over its heading. */
const PASS_RATE_HELP =
    "The share of checks that passed in each finished run, oldest to newest. A check counts once per scenario: passed when enough of its samples passed. All comes only from runs of every scenario; a tag's line also takes runs limited to tags including it. A line joins the runs that measured it, so a point is a reading, the line between is not. A re-checked run shows its latest re-check. Runs started in a group are not included. Click a point to open the run.";

/** The pass rate over the agent's finished runs: one line for all checks and one per tag. Clicking the chart opens the nearest run. */
export function PassRateTrend({
    points,
    picked,
    recent,
}: {
    points: RunPassRates[];
    picked: readonly string[];
    recent: readonly string[];
}) {
    return (
        <Paper withBorder p="md">
            <Stack gap="sm">
                <Title order={3}>
                    <HeaderHelp label="Pass rate" help={PASS_RATE_HELP} />
                </Title>
                {points.length < TREND_MIN_RUNS ? (
                    <Text c="dimmed">The trend appears after two finished runs.</Text>
                ) : (
                    <TrendChart
                        points={points}
                        rows={trendRows(points)}
                        names={trendSeries(points)}
                        picked={picked}
                        recent={recent}
                        format={percentText}
                        domain={[0, 100]}
                        wholeNumbers
                    />
                )}
            </Stack>
        </Paper>
    );
}
