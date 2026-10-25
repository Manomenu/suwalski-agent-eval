import { Paper, Stack, Text } from "@mantine/core";

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
    versionChanges,
    type TrendRow,
} from "./trend";
import { runHref } from "../../routes/links";
import { type RunPassRates } from "./api";

/** Said under every trend chart: group runs are experiments and never make a point. */
const GROUP_RUNS_NOT_INCLUDED = "Runs started in a group are experiments and are not included in this chart.";

const ALL_STROKE_WIDTH = 4;
const TAG_STROKE_WIDTH = 2;

/**
 * The line chart the trend cards share: "All" and the picked tags' lines (the recent tags' with none picked) with their legend, a
 * tooltip of the series the run measured, a click that opens the run. `format` writes a value with its unit; `domain` is the y axis's.
 */
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
    /** The tags picked above the charts; none: the recent ones. */
    picked: readonly string[];
    /** The tags drawn while none is picked. */
    recent: readonly string[];
    format: (value: number) => string;
    domain: [number, number | "auto"];
    /** The y axis in whole numbers: percentages; turn times keep their decimals. */
    wholeNumbers?: boolean;
}) {
    // The colours follow `names`, so a line keeps its colour whatever else is picked.
    const shown = shownSeries(names, picked, recent);
    // The x axis is keyed by run (two runs can share a minute); its ticks show the run's short date.
    const labelOf = new Map(rows.map((row) => [row[RUN_KEY], row[LABEL_KEY]]));
    const changes = versionChanges(points, rows);
    const newVersionOf = new Map(changes.map((change) => [change.runId, change.label]));

    return (
        <Stack gap="sm">
            <LineTrend
                rows={rows}
                dataKey={RUN_KEY}
                xTickFormatter={(runId) => String(labelOf.get(runId) ?? "")}
                marks={changes.map((change) => ({ x: change.runId, label: change.label }))}
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
                }}
            />
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
