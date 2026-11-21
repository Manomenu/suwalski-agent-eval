import { Paper } from "@maniumek/suwfrontkit";

import { changeMarks } from "../../charts/chartStyle";
import { LineTrend } from "../../charts/LineTrend";

import {
    ALL_SERIES,
    DETAILS_KEY,
    LABEL_KEY,
    RECHECK_KEY,
    RUN_KEY,
    asTrendRow,
    hasRechecks,
    measuredSeries,
    seriesColor,
    shownSeries,
    runChanges,
    type TrendRow,
} from "./trend";
import { runHref } from "../../routes/links";
import { type RunPassRates } from "./api";

const GROUP_RUNS_NOT_INCLUDED = "Runs started in a group are experiments and are not included in this chart.";

const ALL_STROKE_WIDTH = 4;
const TAG_STROKE_WIDTH = 2;

export function TrendChart({
    points,
    rows,
    names,
    picked,
    recent,
    format,
    domain,
    wholeNumbers = false,
}: {
    points: RunPassRates[];
    rows: TrendRow[];
    names: string[];
    picked: readonly string[];
    recent: readonly string[];
    format: (value: number) => string;
    domain: [number, number | "auto"];
    wholeNumbers?: boolean;
}) {
    const shown = shownSeries(names, picked, recent);

    const labelOf = new Map(rows.map((row) => [row[RUN_KEY], row[LABEL_KEY]]));
    const marks = changeMarks(runChanges(points, rows));
    const markOf = new Map(marks.map((mark) => [mark.x, mark.label]));

    return (
        <div className="flex flex-col gap-3">
            <LineTrend
                rows={rows}
                dataKey={RUN_KEY}
                xTickFormatter={(runId) => String(labelOf.get(runId) ?? "")}
                marks={marks}
                series={shown.map((name) => ({
                    name,
                    color: seriesColor(name, names),
                    strokeWidth: name === ALL_SERIES ? ALL_STROKE_WIDTH : TAG_STROKE_WIDTH,
                }))}
                format={format}
                domain={domain}
                wholeNumbers={wholeNumbers}
                onPointClick={(index) => {
                    const runId = rows[index]?.[RUN_KEY];
                    if (typeof runId === "string") window.location.assign(runHref(runId));
                }}
                tooltip={(value) => {
                    const row = asTrendRow(value);
                    if (!row) return null;
                    return (
                        <Paper className="flex flex-col gap-0.5 p-2.5 shadow-sm">
                            <p className="font-medium">{row[LABEL_KEY]}</p>
                            <p className="text-xs text-muted-foreground">{row[DETAILS_KEY]}</p>
                            {typeof row[RUN_KEY] === "string" && markOf.has(row[RUN_KEY]) && (
                                <p className="text-xs font-semibold">New from this run: {markOf.get(row[RUN_KEY])}</p>
                            )}
                            {typeof row[RECHECK_KEY] === "string" && (
                                <p className="text-xs text-muted-foreground">Re-checked: these are the results of its latest re-check</p>
                            )}
                            {measuredSeries(row, shown).map((name) => (
                                <p key={name} className="text-sm" style={{ color: seriesColor(name, names) }}>
                                    {name}: {format(Number(row[name]))}
                                </p>
                            ))}
                        </Paper>
                    );
                }}
            />
            <p className="text-xs text-muted-foreground">{GROUP_RUNS_NOT_INCLUDED}</p>
            {hasRechecks(points) && (
                <p className="text-xs text-muted-foreground">
                    A point of a re-checked run shows the results of its latest re-check; the run keeps its place and time.
                </p>
            )}
        </div>
    );
}
