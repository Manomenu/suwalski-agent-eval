import { Paper } from "@maniumek/suwfrontkit";

import { HeaderHelp } from "../../help/HeaderHelp";
import { percentText } from "../../charts/chartStyle";
import { TrendChart } from "./TrendChart";
import { CHANGE_MARKS_HELP, TREND_MIN_RUNS, trendRows, trendSeries } from "./trend";
import { type RunPassRates } from "./api";

const PASS_RATE_HELP =
    "The share of checks that passed in each finished run, oldest to newest. A check counts once per scenario: passed when enough of its samples passed. All comes only from runs of every scenario; a tag's line also takes runs limited to tags including it. A line joins the runs that measured it, so a point is a reading, the line between is not. A re-checked run shows its latest re-check. Runs started in a group are not included. Click a point to open the run." +
    ` ${CHANGE_MARKS_HELP}`;

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
        <Paper role="region" aria-label="Pass rate" className="flex flex-col gap-3">
            <h3 className="text-[22px] font-bold">
                <HeaderHelp label="Pass rate" help={PASS_RATE_HELP} />
            </h3>
            {points.length < TREND_MIN_RUNS ? (
                <p className="text-muted-foreground">The trend appears after two finished runs.</p>
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
        </Paper>
    );
}
