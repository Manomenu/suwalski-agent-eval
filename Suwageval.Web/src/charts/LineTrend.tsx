import type { ReactNode } from "react";
import { CartesianGrid, Line, LineChart, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";

import { ChartLegend } from "./ChartLegend";
import { CHART_HEIGHT, type ChartMark } from "./chartStyle";

export type ChartRow = Record<string, string | number | null>;

export interface ChartSeries {
    name: string;
    color: string;
    strokeWidth?: number;
}

const AXIS = { stroke: "var(--muted-foreground)", fontSize: 12 } as const;
const DEFAULT_STROKE_WIDTH = 2;

export function LineTrend({
    rows,
    dataKey,
    series,
    marks = [],
    format,
    domain,
    wholeNumbers = false,
    xTickFormatter,
    tooltip,
    onPointClick,
}: {
    rows: ChartRow[];
    dataKey: string;
    series: readonly ChartSeries[];
    marks?: readonly ChartMark[];
    format: (value: number) => string;
    domain?: [number, number | "auto"];
    wholeNumbers?: boolean;
    xTickFormatter?: (value: string) => string;
    tooltip?: (row: ChartRow) => ReactNode;
    onPointClick?: (index: number) => void;
}) {
    return (
        <div className="flex flex-col gap-3">
            <ResponsiveContainer width="100%" height={CHART_HEIGHT}>
                <LineChart
                    data={rows}
                    onClick={(state) => {
                        if (onPointClick !== undefined && state.activeIndex != null) onPointClick(Number(state.activeIndex));
                    }}
                >
                    <CartesianGrid stroke="var(--border)" strokeDasharray="3 3" vertical={false} />
                    <XAxis dataKey={dataKey} {...AXIS} {...(xTickFormatter === undefined ? {} : { tickFormatter: xTickFormatter })} />
                    <YAxis {...AXIS} {...(domain === undefined ? {} : { domain })} allowDecimals={!wholeNumbers} tickFormatter={format} />
                    <Tooltip
                        cursor={{ stroke: "var(--border)" }}
                        {...(tooltip === undefined
                            ? { formatter: (value: unknown) => format(Number(value)) }
                            : {
                                  content: ({ payload }: { payload?: readonly { payload?: unknown }[] }) => {
                                      const row = payload?.[0]?.payload;
                                      return typeof row === "object" && row !== null ? tooltip(row as ChartRow) : null;
                                  },
                              })}
                    />
                    {marks.map((mark) => (
                        <ReferenceLine
                            key={mark.x}
                            x={mark.x}
                            stroke="var(--muted-foreground)"
                            strokeDasharray="4 4"
                            label={{ value: mark.label, position: "insideTopLeft", fill: "var(--muted-foreground)", fontSize: 12 }}
                        />
                    ))}
                    {series.map((line) => (
                        <Line
                            key={line.name}
                            type="linear"
                            dataKey={line.name}
                            stroke={line.color}
                            strokeWidth={line.strokeWidth ?? DEFAULT_STROKE_WIDTH}
                            dot={{ r: 3, fill: line.color }}
                            connectNulls
                            isAnimationActive={false}
                        />
                    ))}
                </LineChart>
            </ResponsiveContainer>
            <ChartLegend series={series} />
        </div>
    );
}
