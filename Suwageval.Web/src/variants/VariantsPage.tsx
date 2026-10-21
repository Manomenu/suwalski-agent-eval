import { Alert, Anchor, Badge, Group, Loader, Paper, SegmentedControl, Stack, Table, Text, Title, Tooltip } from "@mantine/core";
import { useState } from "react";

import { useLoaded } from "../agents/useLoaded";
import { agentHref } from "../agents/view";
import { ChecksCount, SamplesCount } from "../runs/RunCounts";
import {
    durationSummary,
    formatTime,
    percent,
    RUN_CHECKS_HELP,
    RUN_SAMPLES_HELP,
    runHref,
    shortId,
    statusColor,
    statusLabel,
} from "../runs/results";
import { fetchVariants, type RunVariants, type VariantCell } from "./api";
import { COLUMN_MIN_WIDTH, LABEL_COLUMN_WIDTH, tableMinWidth } from "./layout";
import {
    bestColumns,
    cellShare,
    cellText,
    noAnswerText,
    scenarioSections,
    selectionOf,
    variantName,
    type ValidSelection,
} from "./variants";

const BEST_BACKGROUND = "var(--mantine-color-green-light)";
const HELP_WIDTH = 280;

/**
 * `/variants?runs=<id>,<id>,…` or `/variants?group=<name>[&agent=<name>]`: runs of one agent side by side as variants, a column per
 * run (typically one per model or configuration). The parameters are the query as it came; this checks them.
 */
export function VariantsPage({ runs, group, agent }: { runs: string; group: string; agent: string }) {
    const selection = selectionOf(runs, group, agent);
    // A wrong query asks for nothing: no request is made.
    return selection.kind === "error" ? <Problem message={selection.message} /> : <Variants selection={selection} />;
}

function Variants({ selection }: { selection: ValidSelection }) {
    const state = useLoaded(JSON.stringify(selection), (signal) => fetchVariants(selection, signal));

    if (state.kind === "loading") return <Loader aria-label="Loading the variants" />;
    if (state.kind === "error") return <Problem message={state.error.message} />;
    return <Loaded variants={state.value} />;
}

function Loaded({ variants }: { variants: RunVariants }) {
    const [byTag, setByTag] = useState(false);
    const columns = variants.runs.length;
    const sections = scenarioSections(variants.scenarios, byTag);

    return (
        <Stack gap="md">
            <Group gap="xs">
                <Anchor href="/">Agents</Anchor>/<Anchor href={agentHref(variants.agent)}>{variants.agent}</Anchor>
            </Group>
            <Title order={2}>Variants of {variants.agent}</Title>
            <Text c="dimmed">
                {variants.group == null ? "" : `Group ${variants.group} · `}
                {columns} {columns === 1 ? "run" : "runs"}, one column each, named by the label of the agent version it ran. A check passes
                when its pass rate reaches its threshold; the best column of a row is marked.
            </Text>
            <Paper withBorder p="md">
                <Table.ScrollContainer minWidth={tableMinWidth(columns)}>
                    <Table layout="fixed" verticalSpacing="xs">
                        <Table.Thead>
                            <Table.Tr>
                                <Table.Th w={LABEL_COLUMN_WIDTH} />
                                {variants.runs.map((variant) => (
                                    <Table.Th key={variant.run.id} miw={COLUMN_MIN_WIDTH} style={{ verticalAlign: "top" }}>
                                        <Stack gap={2}>
                                            <Anchor href={runHref(variant.run.id)} className="wrap-key">
                                                {variantName(variant)}
                                            </Anchor>
                                            <Text size="xs" fw="normal" className="wrap-key">
                                                {variant.modelNames.length === 0 ? "unknown model" : variant.modelNames.join(", ")}
                                            </Text>
                                            <Text size="xs" c="dimmed" fw="normal">
                                                run {shortId(variant.run.id)} · {formatTime(variant.run.createdAt)}
                                            </Text>
                                            {variant.run.status !== "Done" && (
                                                <Badge size="xs" w="fit-content" color={statusColor(variant.run.status)}>
                                                    {statusLabel(variant.run.status)}
                                                </Badge>
                                            )}
                                        </Stack>
                                    </Table.Th>
                                ))}
                            </Table.Tr>
                        </Table.Thead>
                        <Table.Tbody>
                            <SectionRow columns={columns} title="Summary" />
                            <Table.Tr>
                                <Table.Td fw={500}>
                                    <Tooltip label={RUN_CHECKS_HELP} multiline w={HELP_WIDTH}>
                                        <Text span style={{ cursor: "help" }}>
                                            Checks
                                        </Text>
                                    </Tooltip>
                                </Table.Td>
                                {variants.runs.map((variant) => (
                                    <Table.Td key={variant.run.id}>
                                        <ChecksCount run={variant.run} />
                                    </Table.Td>
                                ))}
                            </Table.Tr>
                            <Table.Tr>
                                <Table.Td fw={500}>
                                    <Tooltip label={RUN_SAMPLES_HELP} multiline w={HELP_WIDTH}>
                                        <Text span style={{ cursor: "help" }}>
                                            Samples
                                        </Text>
                                    </Tooltip>
                                </Table.Td>
                                {variants.runs.map((variant) => (
                                    <Table.Td key={variant.run.id}>
                                        <SamplesCount run={variant.run} />
                                    </Table.Td>
                                ))}
                            </Table.Tr>
                            <Table.Tr>
                                <Table.Td fw={500}>Turn time</Table.Td>
                                {variants.runs.map((variant) => (
                                    <Table.Td key={variant.run.id}>
                                        <Text size="sm">{durationSummary(variant)?.replace("Turn time: ", "") ?? "—"}</Text>
                                    </Table.Td>
                                ))}
                            </Table.Tr>
                        </Table.Tbody>
                        <Table.Tbody>
                            <SectionRow columns={columns} title="Pass rate per tag" />
                            {variants.tags.map((row) => {
                                const best = bestColumns(row.rates.map(({ passRate }) => passRate));
                                return (
                                    <Table.Tr key={row.tag ?? "\u0000all"}>
                                        <Table.Td fw={500} className="wrap-key">
                                            {row.tag ?? "All"}
                                        </Table.Td>
                                        {row.rates.map(({ passRate }, column) => (
                                            <Table.Td
                                                key={variants.runs[column]?.run.id}
                                                bg={best.has(column) ? BEST_BACKGROUND : "transparent"}
                                                fw={best.has(column) ? 700 : 400}
                                            >
                                                {passRate == null ? "—" : percent(passRate)}
                                            </Table.Td>
                                        ))}
                                    </Table.Tr>
                                );
                            })}
                        </Table.Tbody>
                        <Table.Tbody>
                            <Table.Tr>
                                <Table.Th colSpan={columns + 1} bg="var(--mantine-color-default)">
                                    <Group justify="space-between">
                                        Scenarios
                                        <SegmentedControl
                                            size="xs"
                                            aria-label="List scenarios"
                                            value={byTag ? "tag" : "scenario"}
                                            onChange={(value) => {
                                                setByTag(value === "tag");
                                            }}
                                            data={[
                                                { value: "scenario", label: "By scenario" },
                                                { value: "tag", label: "By tag" },
                                            ]}
                                        />
                                    </Group>
                                </Table.Th>
                            </Table.Tr>
                            {sections.map((section) => (
                                <ScenarioRows key={section.title ?? ""} section={section} columns={columns} />
                            ))}
                        </Table.Tbody>
                    </Table>
                </Table.ScrollContainer>
                {variants.scenarios.length === 0 && (
                    <Text c="dimmed" mt="sm">
                        No run has a result for a scenario yet.
                    </Text>
                )}
            </Paper>
        </Stack>
    );
}

function SectionRow({ columns, title }: { columns: number; title: string }) {
    return (
        <Table.Tr>
            <Table.Th colSpan={columns + 1} bg="var(--mantine-color-default)">
                {title}
            </Table.Th>
        </Table.Tr>
    );
}

function ScenarioRows({ section, columns }: { section: ReturnType<typeof scenarioSections>[number]; columns: number }) {
    return (
        <>
            {section.title !== null && (
                <Table.Tr>
                    <Table.Td colSpan={columns + 1} fz="xs" c="dimmed" fw={600} className="wrap-key">
                        {section.title}
                    </Table.Td>
                </Table.Tr>
            )}
            {section.scenarios.map((scenario) => {
                const best = bestColumns(scenario.cells.map(cellShare));
                return (
                    <Table.Tr key={scenario.key}>
                        <Table.Td className="wrap-key">{scenario.key}</Table.Td>
                        {scenario.cells.map((cell, column) => (
                            <CellView key={column} cell={cell} best={best.has(column)} />
                        ))}
                    </Table.Tr>
                );
            })}
        </>
    );
}

function CellView({ cell, best }: { cell: VariantCell | null; best: boolean }) {
    const noAnswer = noAnswerText(cell);
    return (
        <Table.Td bg={best ? BEST_BACKGROUND : "transparent"}>
            <Text size="sm" fw={best ? 700 : 400}>
                {cellText(cell)}
            </Text>
            {cell !== null && cell.failedChecks.length > 0 && (
                <Text size="xs" c="red" className="wrap-key">
                    failed: {cell.failedChecks.join(", ")}
                </Text>
            )}
            {noAnswer !== null && (
                <Text size="xs" c="dimmed">
                    {noAnswer}
                </Text>
            )}
        </Table.Td>
    );
}

function Problem({ message }: { message: string }) {
    return (
        <Stack gap="sm">
            <Alert color="red" title="Cannot show the variants">
                {message}
            </Alert>
            <Anchor href="/">Back to the list of agents</Anchor>
        </Stack>
    );
}
