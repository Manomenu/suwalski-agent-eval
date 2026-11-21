import { Anchor, Label, SegmentedControl, Select, Spinner, Table } from "@maniumek/suwfrontkit";
import { useQuery } from "@tanstack/react-query";
import { lazy, Suspense, useId, useState } from "react";

import { Failure } from "../../api/Failure";
import { percentText, seriesColor } from "../../charts/chartStyle";

const MAIN_STROKE_WIDTH = 4;
import { HeaderHelp } from "../../help/HeaderHelp";
import { ListPagination } from "../../pagination/ListPagination";
import { pageOfItems } from "../../pagination/pages";
import { usePageParam } from "../../pagination/usePageParam";
import { failedConversationsHref } from "../../routes/links";
import { monitoringChecksQuery, monitoringTrendQuery, type MonitoringTrend } from "./api";
import {
    MONITORING_CHECKS_PAGE_SIZE,
    checkKeys,
    CONVERSATIONS_SERIES,
    DAY_KEY,
    dayMarks,
    hasFailures,
    MONITORING_CHECKS_HELP,
    MONITORING_TREND_HELP,
    passRateRows,
    rateText,
    TREND_DAYS,
    volumeRows,
    volumeSeries,
    type Rate,
    type TrendDays,
} from "./monitoring";

const LineTrend = lazy(() => import("../../charts/LineTrend").then((module) => ({ default: module.LineTrend })));

export function ChecksSection({
    agent,
    environment,
    onEnvironmentChange,
}: {
    agent: string;
    environment: string | null;
    onEnvironmentChange: (environment: string | null) => void;
}) {
    const checks = useQuery(monitoringChecksQuery(agent, environment));
    const environmentId = useId();
    const [days, setDays] = useState<TrendDays>(30);
    const [checksPage, setChecksPage] = usePageParam("checksPage");
    const trend = useQuery(monitoringTrendQuery(agent, days, environment));
    const environments = trend.data?.environments ?? [];
    const rate = (key: string, value: Rate) =>
        hasFailures(value) ? (
            <Anchor className="text-destructive" href={failedConversationsHref(agent, key, environment)}>
                {rateText(value)}
            </Anchor>
        ) : (
            rateText(value)
        );
    return (
        <div className="flex flex-col gap-3">
            {(environments.length > 1 || environment !== null) && (
                <div className="flex max-w-65 flex-col gap-1">
                    <Label htmlFor={environmentId}>Environment</Label>
                    <Select
                        id={environmentId}
                        value={environment ?? ""}
                        onChange={(event) => {
                            onEnvironmentChange(event.target.value === "" ? null : event.target.value);
                        }}
                    >
                        <option value="">All environments</option>
                        {environments.map((name) => (
                            <option key={name} value={name}>
                                {name}
                            </option>
                        ))}
                    </Select>
                </div>
            )}
            <h3 className="text-[22px] font-bold">
                <HeaderHelp label="Monitoring checks" help={MONITORING_CHECKS_HELP} />
            </h3>
            {checks.isError && <Failure className="text-destructive" error={checks.error} />}
            {checks.data?.length === 0 && (
                <p className="text-muted-foreground">No monitoring checks: write them in suwageval/monitoring-checks.yaml and push.</p>
            )}
            {checks.data !== undefined && checks.data.length > 0 && (
                <div className="flex flex-col gap-3">
                    <Table className="min-w-[700px]">
                        <thead>
                            <tr>
                                <th>Check</th>
                                <th>What it makes sure of</th>
                                <th>7 days</th>
                                <th>30 days</th>
                            </tr>
                        </thead>
                        <tbody>
                            {pageOfItems(checks.data, checksPage, MONITORING_CHECKS_PAGE_SIZE).items.map(
                                ({ check, last7Days, last30Days }) => (
                                    <tr key={check.key}>
                                        <td className="wrap-key">{check.key}</td>
                                        <td>{check.description}</td>
                                        <td>{rate(check.key, last7Days)}</td>
                                        <td>{rate(check.key, last30Days)}</td>
                                    </tr>
                                ),
                            )}
                        </tbody>
                    </Table>
                    <ListPagination
                        totalCount={checks.data.length}
                        page={checksPage}
                        pageSize={MONITORING_CHECKS_PAGE_SIZE}
                        onChange={setChecksPage}
                    />
                </div>
            )}
            <div className="flex flex-wrap items-end justify-between gap-4">
                <h4 className="text-lg font-bold">
                    <HeaderHelp label="Traffic and pass rates" help={MONITORING_TREND_HELP} />
                </h4>
                <SegmentedControl
                    label="Days"
                    value={String(days)}
                    options={TREND_DAYS.map((value) => ({ value: String(value), label: `${String(value)} days` }))}
                    onValueChange={(value) => {
                        setDays(TREND_DAYS.find((option) => String(option) === value) ?? 30);
                    }}
                />
            </div>
            {trend.isError && <Failure className="text-destructive" error={trend.error} />}
            {trend.data !== undefined && (
                <Suspense fallback={<Spinner label="Loading the charts" />}>
                    <TrendCharts trend={trend.data} />
                </Suspense>
            )}
        </div>
    );
}

function TrendCharts({ trend }: { trend: MonitoringTrend }) {
    if (trend.days.length === 0) return <p className="text-muted-foreground">No conversations in these days.</p>;
    const marks = dayMarks(trend);
    const keys = checkKeys(trend);
    return (
        <div className="flex flex-col gap-4">
            <p className="text-sm font-medium">Conversations</p>
            <LineTrend
                rows={volumeRows(trend)}
                dataKey={DAY_KEY}
                marks={marks}
                series={volumeSeries(trend).map((name, index) => ({
                    name,
                    color: seriesColor(index),
                    ...(name === CONVERSATIONS_SERIES ? { strokeWidth: MAIN_STROKE_WIDTH } : {}),
                }))}
                format={String}
                wholeNumbers
            />
            {keys.length > 0 && (
                <>
                    <p className="text-sm font-medium">Pass rate per check</p>
                    <LineTrend
                        rows={passRateRows(trend)}
                        dataKey={DAY_KEY}
                        marks={marks}
                        series={keys.map((key, index) => ({ name: key, color: seriesColor(index + 1) }))}
                        format={percentText}
                        domain={[0, 100]}
                        wholeNumbers
                    />
                </>
            )}
        </div>
    );
}
