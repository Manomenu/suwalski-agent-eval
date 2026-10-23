import { Alert, Anchor, Badge, Group, Loader, Paper, SegmentedControl, Stack, Switch, Table, Text, Title, Tooltip } from "@mantine/core";
import { useState } from "react";

import { fetchVersion, fetchVersions, type AgentVersion } from "../agents/api";
import { useRequest } from "../api/useRequest";
import { agentHref } from "../agents/view";
import { ModelJudgedIcon } from "../checks/ModelJudgedIcon";
import { HeaderHelp } from "../runs/HeaderHelp";
import { ChecksCount, SamplesCount } from "../runs/RunCounts";
import { durationSummary, percent, RUN_CHECKS_HELP, RUN_SAMPLES_HELP } from "../runs/results";
import { filteredLabel, keepsTags, selectedTagRows } from "../tagFilter/tagSelection";
import { TagFilter } from "../tagFilter/TagFilter";
import { useTagFilter } from "../tagFilter/useTagFilter";
import { fetchComparison, type ComparedRun, type RunComparison } from "./api";
import { ColumnHeader, type BaselineInfo } from "./ColumnHeader";
import { baselineOf, deltaText, searchWithBaseline, TIME_DELTA_HELP, timeDeltaText, toggledBaseline, trendColor } from "./compare";
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
 * `/compare`: runs of one agent side by side, one column per run. The runs come as `?runs=<id>,<id>,…` or as the
 * `?group=<name>[&agent=<name>]` they were started in; `?baseline=<id>` (one of them) reads the others against that run. The parameters
 * are the query as it came, this checks them.
 */
export function ComparePage({ runs, group, agent, baseline }: { runs: string; group: string; agent: string; baseline: string }) {
    const selection = selectionOf(runs, group, agent);
    // A wrong query asks for nothing: no request is made.
    return selection.kind === "error" ? (
        <Problem message={selection.message} />
    ) : (
        <Comparison selection={selection} baseline={baselineOf(baseline)} />
    );
}

function Comparison({ selection, baseline }: { selection: ValidSelection; baseline: string | null }) {
    // The baseline is in the address so the view can be shared; changing it asks the server again but does not reload the page.
    const [baselineId, setBaselineId] = useState(baseline);
    const chooseBaseline = (runId: string) => {
        const next = toggledBaseline(baselineId, runId);
        setBaselineId(next);
        const { pathname, search, hash } = window.location;
        window.history.replaceState(null, "", `${pathname}${searchWithBaseline(search, next)}${hash}`);
    };
    const state = useRequest(JSON.stringify([selection, baselineId]), (signal) => fetchComparison(selection, baselineId, signal));

    if (state.kind === "loading") return <Loader aria-label="Loading the comparison" />;
    if (state.kind === "error") return <Problem message={state.error.message} />;
    return <Loaded selection={selection} comparison={state.value} onBaseline={chooseBaseline} />;
}

function Loaded({
    selection,
    comparison,
    onBaseline,
}: {
    selection: ValidSelection;
    comparison: RunComparison;
    onBaseline: (runId: string) => void;
}) {
    const agent = comparison.agent;
    const versionIds = [...new Set(comparison.runs.map((column) => column.run.agentVersionId))];
    const base = useRequest(`${agent}/${versionIds.join(",")}`, async (signal) => {
        const [versions, details] = await Promise.all([
            fetchVersions(agent, signal),
            Promise.all(versionIds.map((id) => fetchVersion(agent, id, signal))),
        ]);
        return { versions, details };
    });

    if (base.kind === "loading") return <Loader aria-label="Loading the comparison" />;
    if (base.kind === "error") return <Problem message={base.error.message} />;
    const { versions, details } = base.value;
    const differences =
        versionIds.length > 1
            ? contractDifferences(
                  comparison.runs.map((column) => ({
                      versionId: column.run.agentVersionId,
                      contract: (details.find((detail) => detail.id === column.run.agentVersionId) ?? details[0])?.content ?? {
                          agents: [],
                      },
                  })),
              )
            : [];
    return <View selection={selection} variants={comparison} versions={versions} differences={differences} onBaseline={onBaseline} />;
}

function View({
    selection,
    variants,
    versions,
    differences,
    onBaseline,
}: {
    selection: ValidSelection;
    variants: RunComparison;
    versions: readonly AgentVersion[];
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
    const { baseline } = variants;
    const compared = baseline?.runs ?? null;
    const comparedTags = baseline == null ? null : selectedTagRows(baseline.tags, selectedTags);

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
                    ? " A check passes when its pass rate reaches its threshold; the best column of a row is marked. Pick a baseline by a column's header to read the others against it."
                    : " Every run is read against the baseline; pick another by its header, or click the baseline again to go back to the plain view. A check passes when its pass rate reaches its threshold."}
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
                                {variants.runs.map((variant) => {
                                    const baselineInfo: BaselineInfo | null =
                                        columns < 2
                                            ? null
                                            : {
                                                  isBaseline: variant.run.id === baseline?.runId,
                                                  onToggleBaseline: () => {
                                                      onBaseline(variant.run.id);
                                                  },
                                              };
                                    return (
                                        <Table.Th key={variant.run.id} miw={COLUMN_MIN_WIDTH} style={{ verticalAlign: "top" }}>
                                            <ColumnHeader
                                                agent={variants.agent}
                                                variant={variant}
                                                version={versions.find((candidate) => candidate.id === variant.run.agentVersionId)}
                                                baseline={baselineInfo}
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
                                {variants.runs.map((variant, column) => (
                                    <Table.Td key={variant.run.id}>
                                        <Text size="sm">{durationSummary(variant)?.replace("Turn time: ", "") ?? "—"}</Text>
                                        <TimeChanges compared={compared?.[column]} />
                                    </Table.Td>
                                ))}
                            </Table.Tr>
                            {compared !== null && (
                                <Table.Tr>
                                    <Table.Td fw={500}>Judge</Table.Td>
                                    {compared.map((run) => (
                                        <Table.Td key={run.run.id}>
                                            <Judge compared={run} baselineJudgeModel={baselineJudgeModel(variants)} />
                                        </Table.Td>
                                    ))}
                                </Table.Tr>
                            )}
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

/** The judge of the baseline's own column: what the "other judge" badge of the others is measured against. */
function baselineJudgeModel({ baseline }: RunComparison): string | null {
    return baseline?.runs.find((compared) => compared.run.id === baseline.runId)?.judgeModel ?? null;
}

/** The turn time changes of a run against the baseline's, as the server computed them: median and p95. Nothing on the baseline itself. */
function TimeChanges({ compared }: { compared: ComparedRun | undefined }) {
    if (compared === undefined) return null;
    const changes = [
        ...(compared.medianChange == null ? [] : [{ label: "median", change: compared.medianChange }]),
        ...(compared.p95Change == null ? [] : [{ label: "p95", change: compared.p95Change }]),
    ];
    if (changes.length === 0) return null;
    return (
        <Tooltip label={TIME_DELTA_HELP} multiline w={HELP_WIDTH}>
            <Group gap="xs" wrap="nowrap" w="fit-content" style={{ cursor: "help" }}>
                {changes.map(({ label, change }) => (
                    <Text key={label} size="xs" c={trendColor(change.trend)}>
                        {label} {timeDeltaText(change)}
                    </Text>
                ))}
            </Group>
        </Tooltip>
    );
}

/** The model that judged a run, and a warning when it is not the baseline's: a different verdict may come from the judge. */
function Judge({ compared, baselineJudgeModel }: { compared: ComparedRun; baselineJudgeModel: string | null }) {
    if (compared.judgeModel == null) return <Text size="sm">—</Text>;
    return (
        <Group gap={4} wrap="nowrap">
            <ModelJudgedIcon model={compared.judgeModel} />
            <Text size="sm" className="wrap-key">
                {compared.judgeModel}
            </Text>
            {compared.judgeChanged && (
                <Tooltip
                    label={`The baseline was judged by ${baselineJudgeModel ?? ""}: a different verdict may come from the judge, not the agent`}
                    multiline
                    w={HELP_WIDTH}
                >
                    <Badge size="xs" variant="light" color="orange" style={{ cursor: "help" }}>
                        other judge
                    </Badge>
                </Tooltip>
            )}
        </Group>
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
