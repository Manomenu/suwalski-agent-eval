import { LineChart } from "@mantine/charts";
import { Stack } from "@mantine/core";
import type { ReactNode } from "react";

import { ChartLegend } from "./ChartLegend";
import { CHART_HEIGHT, markLines, type ChartMark } from "./chartStyle";

export type ChartRow = Record<string, string | number | null>;

export interface ChartSeries {
    name: string;
    color: string;
    strokeWidth?: number;
}

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
        <Stack gap="sm">
            <LineChart
                h={CHART_HEIGHT}
                data={rows}
                dataKey={dataKey}
                series={series.map(({ name, color }) => ({ name, color }))}
                referenceLines={markLines(marks)}
                curveType="linear"
                connectNulls
                withDots
                withLegend={false}
                valueFormatter={format}
                xAxisProps={xTickFormatter === undefined ? {} : { tickFormatter: xTickFormatter }}
                yAxisProps={{ ...(domain === undefined ? {} : { domain }), allowDecimals: !wholeNumbers }}
                lineProps={(line) => ({ strokeWidth: series.find((one) => one.name === line.name)?.strokeWidth ?? 2 })}
                lineChartProps={
                    onPointClick === undefined
                        ? {}
                        : {
                              onClick: (state) => {
                                  if (state.activeIndex != null) onPointClick(Number(state.activeIndex));
                              },
                          }
                }
                {...(tooltip === undefined
                    ? {}
                    : {
                          tooltipProps: {
                              content: ({ payload }: { payload?: readonly { payload?: unknown }[] }) => {
                                  const row = payload?.[0]?.payload;
                                  return typeof row === "object" && row !== null ? tooltip(row as ChartRow) : null;
                              },
                          },
                      })}
            />
            <ChartLegend series={series} />
        </Stack>
    );
}
