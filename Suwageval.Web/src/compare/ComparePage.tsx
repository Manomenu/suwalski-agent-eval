import { Alert, Anchor, Group, Loader, Paper, SegmentedControl, Stack, Switch, Table, Text, Title, Tooltip } from "@mantine/core";
import { useState } from "react";

import { fetchVersion, fetchVersions, type AgentVersion } from "../agents/api";
import { useLoaded } from "../agents/useLoaded";
import { agentHref, COMPARE_MAX_RUNS, COMPARE_MIN_RUNS } from "../agents/view";
import { HeaderHelp } from "../runs/HeaderHelp";
import { ChecksCount, SamplesCount } from "../runs/RunCounts";
import { durationSummary, percent, RUN_CHECKS_HELP, RUN_SAMPLES_HELP } from "../runs/results";
import { filteredLabel, keepsTags, selectedTagRows } from "../tagFilter/tagSelection";
import { TagFilter } from "../tagFilter/TagFilter";
import { useTagFilter } from "../tagFilter/useTagFilter";
import { fetchComparison, fetchVariants, type RunComparison, type RunVariants } from "./api";
import { ColumnHeader, type BaselineInfo } from "./ColumnHeader";
import { baselineFirst, deltaText, trendColor } from "./compare";
import { contractDifferences, type Difference } from "./contractDiff";
import { COLUMN_MIN_WIDTH, LABEL_COLUMN_WIDTH, tableMinWidth } from "./layout";
import { ScenarioRows } from "./ScenarioRows";
import { useOpenScenarios } from "./useOpenScenarios";
import {
    bestColumns,
    columnTotals,
    coverageHint,
    scenarioDiffers,
    scenarioSections,
    selectionOf,
    TAG_RATE_HELP,
    type ValidSelection,
    visibleTagRows,
} from "./variants";
import { WhatDiffers } from "./WhatDiffers";

const BEST_BACKGROUND = "var(--mantine-color-green-light)";
const HELP_WIDTH = 280;

/**
 * `/compare` and `/variants`: runs of one agent side by side, one column per run. The runs come as `?runs=<id>,<id>,…` or as the
 * `?group=<name>[&agent=<name>]` they were started in; the parameters are the query as it came, this checks them.
 */
export function ComparePage({ runs, group, agent }: { runs: string; group: string; agent: string }) {
    const selection = selectionOf(runs, group, agent);
    // A wrong query asks for nothing: no request is made.
    return selection.kind === "error" ? <Problem message={selection.message} /> : <Comparison selection={selection} />;
}

function Comparison({ selection }: { selection: ValidSelection }) {
    // The server reads the first run as the baseline: another baseline is the same runs asked for again, that run first.
    const [baselineId, setBaselineId] = useState<string | null>(null);
    const asked: ValidSelection =
        selection.kind === "runs" ? { kind: "runs", ids: baselineFirst(selection.ids, baselineId ?? selection.ids[0] ?? "") } : selection;
    const state = useLoaded(JSON.stringify(asked), (signal) => fetchVariants(asked, signal));

    if (state.kind === "loading") return <Loader aria-label="Loading the comparison" />;
    if (state.kind === "error") return <Problem message={state.error.message} />;
    return <Loaded selection={asked} variants={state.value} onBaseline={setBaselineId} />;
}

/** Whether runs are read against a baseline: 2 to 5 runs named one by one, none of them an experiment (a run in a group). */
function readsAgainstBaseline(selection: ValidSelection, variants: RunVariants): boolean {
    return (
        selection.kind === "runs" &&
        variants.runs.length >= COMPARE_MIN_RUNS &&
        variants.runs.length <= COMPARE_MAX_RUNS &&
        variants.runs.every((column) => column.run.group == null)
    );
}

function Loaded({
    selection,
    variants,
    onBaseline,
}: {
    selection: ValidSelection;
    variants: RunVariants;
    onBaseline: (runId: string) => void;
}) {
    const agent = variants.agent;
    const withBaseline = readsAgainstBaseline(selection, variants);
    const versionIds = [...new Set(variants.runs.map((column) => column.run.agentVersionId))];
    const base = useLoaded(
        `${agent}/${versionIds.join(",")}/${withBaseline ? variants.runs.map((column) => column.run.id).join(",") : "-"}`,
        async (signal) => {
            const [versions, details, comparison] = await Promise.all([
                fetchVersions(agent, signal),
                Promise.all(versionIds.map((id) => fetchVersion(agent, id, signal))),
                withBaseline
                    ? fetchComparison(
                          variants.runs.map((column) => column.run.id),
                          signal,
                      )
                    : Promise.resolve(null),
            ]);
            return { versions, details, comparison };
        },
    );

    if (base.kind === "loading") return <Loader aria-label="Loading the comparison" />;
    if (base.kind === "error") return <Problem message={base.error.message} />;
    const { versions, details, comparison } = base.value;
    const differences =
        versionIds.length > 1
            ? contractDifferences(
                  variants.runs.map((column) => ({
                      versionId: column.run.agentVersionId,
                      contract: (details.find((detail) => detail.id === column.run.agentVersionId) ?? details[0])?.content ?? {
                          agents: [],
                      },
                  })),
              )
            : [];
    return (
        <View
            selection={selection}
            variants={variants}
            versions={versions}
            comparison={comparison}
            differences={differences}
            onBaseline={onBaseline}
        />
    );
}

function View({
    selection,
    variants,
    versions,
    comparison,
    differences,
    onBaseline,
}: {
    selection: ValidSelection;
    variants: RunVariants;
    versions: readonly AgentVersion[];
    comparison: RunComparison | null;
    differences: Difference[];
    onBaseline: (runId: string) => void;
}) {
    // Runs picked one by one are read for what changed, so rows that came out the same are hidden; a group lists every scenario.
    const [onlyDifferences, setOnlyDifferences] = useState(selection.kind === "runs");
    const [byTag, setByTag] = useState(false);
    const [selectedTags, selectTags] = useTagFilter();
    const [open, toggleOpen] = useOpenScenarios();
    const filtered = selectedTags.length > 0;
    const columns = variants.runs.length;
    const tagged = variants.scenarios.filter((scenario) => keepsTags(scenario.tags, selectedTags));
    const scenarios = tagged.filter((scenario) => !onlyDifferences || scenarioDiffers(scenario.cells));
    const sections = scenarioSections(scenarios, byTag, selectedTags);
    const tagRows = visibleTagRows(selectedTagRows(variants.tags, selectedTags));
    const compared = comparison?.runs ?? null;
    const comparedTags = comparison === null ? null : selectedTagRows(comparison.tags, selectedTags);

    return (
        <Stack gap="md">
            <Group gap="xs">
                <Anchor href="/">Agents</Anchor>/<Anchor href={agentHref(variants.agent)}>{variants.agent}</Anchor>
            </Group>
            <Title order={2}>Compare runs of {variants.agent}</Title>
            <Text c="dimmed">
                {variants.group == null ? "" : `Group ${variants.group} · `}
                {columns} {columns === 1 ? "run" : "runs"}, one column each.
                {compared === null
                    ? " A check passes when its pass rate reaches its threshold; the best column of a row is marked."
                    : " Every run is read against the baseline: the first run, until you pick another by its header. A check passes when its pass rate reaches its threshold."}
            </Text>
            <TagFilter
                tags={[...variants.scenarios.flatMap((scenario) => scenario.tags), ...variants.tags.flatMap((row) => row.tag ?? [])]}
                selected={selectedTags}
                onChange={selectTags}
            />
            <Paper withBorder p="md">
                <Table.ScrollContainer minWidth={tableMinWidth(columns)}>
                    <Table layout="fixed" verticalSpacing="xs">
                        <Table.Thead>
                            <Table.Tr>
                                <Table.Th w={LABEL_COLUMN_WIDTH} />
                                {variants.runs.map((variant, column) => {
                                    const comparedRun = compared?.[column];
                                    const baseline: BaselineInfo | null =
                                        comparedRun === undefined
                                            ? null
                                            : {
                                                  compared: comparedRun,
                                                  baselineJudgeModel: compared?.[0]?.judgeModel ?? null,
                                                  isBaseline: column === 0,
                                                  onUseAsBaseline: () => {
                                                      onBaseline(variant.run.id);
                                                  },
                                              };
                                    return (
                                        <Table.Th key={variant.run.id} miw={COLUMN_MIN_WIDTH} style={{ verticalAlign: "top" }}>
                                            <ColumnHeader
                                                agent={variants.agent}
                                                variant={variant}
                                                version={versions.find((candidate) => candidate.id === variant.run.agentVersionId)}
                                                baseline={baseline}
                                            />
                                        </Table.Th>
                                    );
                                })}
                            </Table.Tr>
                        </Table.Thead>
                        <WhatDiffers agent={variants.agent} columns={variants.runs} differences={differences} colSpan={columns + 1} />
                        <Table.Tbody>
                            <SectionRow columns={columns} title="Summary" />
                            <Table.Tr>
                                <Table.Td fw={500}>
                                    <Tooltip label={RUN_CHECKS_HELP} multiline w={HELP_WIDTH}>
                                        <Text span style={{ cursor: "help" }}>
                                            {filteredLabel("Checks", selectedTags)}
                                        </Text>
                                    </Tooltip>
                                </Table.Td>
                                {variants.runs.map((variant, column) => (
                                    <Table.Td key={variant.run.id} className="no-wrap">
                                        <ChecksCount run={filtered ? columnTotals(scenarios, column) : variant.run} />
                                    </Table.Td>
                                ))}
                            </Table.Tr>
                            <Table.Tr>
                                <Table.Td fw={500}>
                                    <Tooltip label={RUN_SAMPLES_HELP} multiline w={HELP_WIDTH}>
                                        <Text span style={{ cursor: "help" }}>
                                            {filteredLabel("Samples", selectedTags)}
                                        </Text>
                                    </Tooltip>
                                </Table.Td>
                                {variants.runs.map((variant, column) => (
                                    <Table.Td key={variant.run.id} className="no-wrap">
                                        <SamplesCount run={filtered ? columnTotals(scenarios, column) : variant.run} />
                                    </Table.Td>
                                ))}
                            </Table.Tr>
                            <Table.Tr>
                                <Table.Td fw={500}>{filtered ? "Turn time (all scenarios)" : "Turn time"}</Table.Td>
                                {variants.runs.map((variant) => (
                                    <Table.Td key={variant.run.id}>
                                        <Text size="sm">{durationSummary(variant)?.replace("Turn time: ", "") ?? "—"}</Text>
                                    </Table.Td>
                                ))}
                            </Table.Tr>
                        </Table.Tbody>
                        <Table.Tbody>
                            <SectionRow columns={columns} title="Pass rate per tag" help={TAG_RATE_HELP} />
                            {tagRows.map((row) => {
                                const best = bestColumns(row.rates.map(({ passRate }) => passRate));
                                const changes = comparedTags?.find((candidate) => candidate.tag === row.tag);
                                return (
                                    <Table.Tr key={row.tag ?? "\u0000all"}>
                                        <Table.Td fw={500} className="wrap-key">
                                            {row.tag ?? "All"}
                                        </Table.Td>
                                        {row.rates.map((rate, column) => {
                                            const { passRate } = rate;
                                            const change = changes?.rates[column]?.change;
                                            const hint = coverageHint(row.tag, rate);
                                            return (
                                                <Table.Td
                                                    key={variants.runs[column]?.run.id}
                                                    bg={change === undefined && best.has(column) ? BEST_BACKGROUND : "transparent"}
                                                    fw={best.has(column) ? 700 : 400}
                                                    className="no-wrap"
                                                >
                                                    <Group gap="xs" wrap="nowrap">
                                                        <Text inherit>{passRate == null ? "—" : percent(passRate)}</Text>
                                                        {hint !== null && (
                                                            <Text
                                                                size="xs"
                                                                c="dimmed"
                                                                fw="normal"
                                                                className="wrap-key"
                                                                style={{ whiteSpace: "normal" }}
                                                                title={hint}
                                                            >
                                                                {hint}
                                                            </Text>
                                                        )}
                                                        {change != null && (
                                                            <Text size="sm" c={trendColor(change.trend)}>
                                                                {deltaText(change)}
                                                            </Text>
                                                        )}
                                                    </Group>
                                                </Table.Td>
                                            );
                                        })}
                                    </Table.Tr>
                                );
                            })}
                        </Table.Tbody>
                        <Table.Tbody>
                            <Table.Tr>
                                <Table.Th colSpan={columns + 1} bg="var(--mantine-color-default)">
                                    <Group justify="space-between">
                                        Scenarios
                                        <Group gap="lg">
                                            <Switch
                                                label="Only rows that differ"
                                                checked={onlyDifferences}
                                                onChange={(event) => {
                                                    setOnlyDifferences(event.currentTarget.checked);
                                                }}
                                            />
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
                                    </Group>
                                </Table.Th>
                            </Table.Tr>
                            {sections.map((section) => (
                                <ScenarioRows
                                    key={section.title ?? ""}
                                    section={section}
                                    columns={variants.runs}
                                    open={open}
                                    onToggle={toggleOpen}
                                />
                            ))}
                        </Table.Tbody>
                    </Table>
                </Table.ScrollContainer>
                {scenarios.length === 0 && (
                    <Text c="dimmed" mt="sm">
                        {tagged.length > 0
                            ? "Every scenario came out the same in all runs — switch off Only rows that differ to see them."
                            : filtered
                              ? "No scenario has any of the selected tags."
                              : "No run has a result for a scenario yet."}
                    </Text>
                )}
            </Paper>
        </Stack>
    );
}

function SectionRow({ columns, title, help }: { columns: number; title: string; help?: string }) {
    return (
        <Table.Tr>
            <Table.Th colSpan={columns + 1} bg="var(--mantine-color-default)">
                {help === undefined ? title : <HeaderHelp label={title} help={help} />}
            </Table.Th>
        </Table.Tr>
    );
}

function Problem({ message }: { message: string }) {
    return (
        <Stack gap="sm">
            <Alert color="red" title="Cannot compare">
                {message}
            </Alert>
            <Anchor href="/">Back to the list of agents</Anchor>
        </Stack>
    );
}
