import { Anchor, Group, SegmentedControl, Stack, Table, Text, Title } from "@mantine/core";
import { useState } from "react";

import { useRequest } from "../../api/useRequest";
import { percentText, seriesColor } from "../../charts/chartStyle";
import { LineTrend } from "../../charts/LineTrend";
import { HeaderHelp } from "../../help/HeaderHelp";
import { failedConversationsHref } from "../../routes/links";
import { fetchMonitoringChecks, fetchMonitoringTrend, type MonitoringTrend } from "./api";
import {
    CHECKED_SERIES,
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
    type Rate,
    type TrendDays,
} from "./monitoring";

/** The agent's monitoring checks: a table of their 7- and 30-day pass rates, and the charts of the last days. */
export function ChecksSection({ agent }: { agent: string }) {
    const checks = useRequest(agent, (signal) => fetchMonitoringChecks(agent, signal));
    const [days, setDays] = useState<TrendDays>(30);
    const trend = useRequest(JSON.stringify([agent, days]), (signal) => fetchMonitoringTrend(agent, days, signal));
    const rate = (key: string, value: Rate) =>
        hasFailures(value) ? (
            <Anchor c="red" href={failedConversationsHref(agent, key)}>
                {rateText(value)}
            </Anchor>
        ) : (
            rateText(value)
        );
    return (
        <Stack gap="sm">
            <Title order={3}>
                <HeaderHelp label="Monitoring checks" help={MONITORING_CHECKS_HELP} />
            </Title>
            {checks.kind === "error" && <Text c="red">{checks.error.message}</Text>}
            {checks.kind === "loaded" && checks.value.length === 0 && (
                <Text c="dimmed">No monitoring checks: write them in suwageval/monitoring-checks.yaml and push.</Text>
            )}
            {checks.kind === "loaded" && checks.value.length > 0 && (
                <Table.ScrollContainer minWidth={700}>
                    <Table striped>
                        <Table.Thead>
                            <Table.Tr>
                                <Table.Th>Check</Table.Th>
                                <Table.Th>What it makes sure of</Table.Th>
                                <Table.Th>7 days</Table.Th>
                                <Table.Th>30 days</Table.Th>
                            </Table.Tr>
                        </Table.Thead>
                        <Table.Tbody>
                            {checks.value.map(({ check, last7Days, last30Days }) => (
                                <Table.Tr key={check.key}>
                                    <Table.Td className="wrap-key">{check.key}</Table.Td>
                                    <Table.Td>{check.description}</Table.Td>
                                    <Table.Td>{rate(check.key, last7Days)}</Table.Td>
                                    <Table.Td>{rate(check.key, last30Days)}</Table.Td>
                                </Table.Tr>
                            ))}
                        </Table.Tbody>
                    </Table>
                </Table.ScrollContainer>
            )}
            <Group justify="space-between" align="flex-end">
                <Title order={4}>
                    <HeaderHelp label="Traffic and pass rates" help={MONITORING_TREND_HELP} />
                </Title>
                <SegmentedControl
                    aria-label="Days"
                    value={String(days)}
                    data={TREND_DAYS.map((value) => ({ value: String(value), label: `${String(value)} days` }))}
                    onChange={(value) => {
                        setDays(TREND_DAYS.find((option) => String(option) === value) ?? 30);
                    }}
                />
            </Group>
            {trend.kind === "error" && <Text c="red">{trend.error.message}</Text>}
            {trend.kind === "loaded" && <TrendCharts trend={trend.value} />}
        </Stack>
    );
}

function TrendCharts({ trend }: { trend: MonitoringTrend }) {
    if (trend.days.length === 0) return <Text c="dimmed">No conversations in these days.</Text>;
    const marks = dayMarks(trend);
    const keys = checkKeys(trend);
    return (
        <Stack gap="md">
            <Text size="sm" fw={500}>
                Conversations
            </Text>
            <LineTrend
                rows={volumeRows(trend)}
                dataKey={DAY_KEY}
                marks={marks}
                series={[
                    { name: CONVERSATIONS_SERIES, color: seriesColor(0) },
                    { name: CHECKED_SERIES, color: seriesColor(1) },
                ]}
                format={String}
                wholeNumbers
            />
            {keys.length > 0 && (
                <>
                    <Text size="sm" fw={500}>
                        Pass rate per check
                    </Text>
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
        </Stack>
    );
}
