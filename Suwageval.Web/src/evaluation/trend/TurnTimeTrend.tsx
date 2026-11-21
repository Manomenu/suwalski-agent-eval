import { Paper, SegmentedControl } from "@maniumek/suwfrontkit";
import { useState } from "react";

import { HeaderHelp } from "../../help/HeaderHelp";
import { TrendChart } from "./TrendChart";
import { CHANGE_MARKS_HELP, TREND_MIN_RUNS, trendRows, trendSeries, turnTimeMeasure, withTurnTimes, type TurnTimeStat } from "./trend";
import { type RunPassRates } from "./api";

const TURN_TIME_HELP =
    "How long the agent took to answer one turn, measured by Suwageval around the call (the network included), per finished run: the median or the 95th percentile of its samples that made a call. The series follow the rules of the pass-rate chart: All comes only from runs of every scenario; a tag's line also takes runs limited to tags including it. Click a point to open the run. Lower is better." +
    ` ${CHANGE_MARKS_HELP}`;

const STATS: readonly { value: TurnTimeStat; label: string }[] = [
    { value: "median", label: "Median" },
    { value: "p95", label: "p95" },
];

export function TurnTimeTrend({
    points,
    picked,
    recent,
}: {
    points: RunPassRates[];
    picked: readonly string[];
    recent: readonly string[];
}) {
    const [stat, setStat] = useState<TurnTimeStat>("median");
    const measured = withTurnTimes(points);

    return (
        <Paper role="region" aria-label="Turn time" className="flex flex-col gap-3">
            <div className="flex flex-wrap items-center justify-between gap-4">
                <h3 className="text-[22px] font-bold">
                    <HeaderHelp label="Turn time" help={TURN_TIME_HELP} />
                </h3>
                <SegmentedControl label="Turn time statistic" options={STATS} value={stat} onValueChange={setStat} />
            </div>
            {measured.length < TREND_MIN_RUNS ? (
                <p className="text-muted-foreground">The trend appears after two finished runs that called the agent.</p>
            ) : (
                <TrendChart
                    points={measured}
                    rows={trendRows(measured, turnTimeMeasure(stat))}
                    names={trendSeries(measured)}
                    picked={picked}
                    recent={recent}
                    format={(value) => `${value.toFixed(1)} s`}
                    domain={[0, "auto"]}
                />
            )}
        </Paper>
    );
}
