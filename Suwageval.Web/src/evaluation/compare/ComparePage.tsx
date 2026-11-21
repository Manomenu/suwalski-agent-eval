import { Alert, Anchor, Badge, cn, formatPercent, Paper, SegmentedControl, Spinner, Tooltip } from "@maniumek/suwfrontkit";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";

import { ModelJudgedIcon } from "../../verdict/ModelJudgedIcon";
import { HeaderHelp } from "../../help/HeaderHelp";
import { NoteBox } from "../../note/NoteBox";
import { ChecksCount, SamplesCount } from "../runs/RunCounts";
import { durationSummary, RUN_CHECKS_HELP, RUN_SAMPLES_HELP } from "../runs/results";
import { filteredLabel, keepsTags, selectedTagRows } from "../../tagFilter/tagSelection";
import { TagFilter } from "../../tagFilter/TagFilter";
import { useTagFilter } from "../../tagFilter/useTagFilter";
import { compareKeys, comparedVersionsQuery, comparisonQuery, setGroupNote, type ComparedRun, type RunComparison } from "./api";
import { ColumnHeader, type BaselineInfo } from "./ColumnHeader";
import { ChangeNote } from "./ChangeNote";
import {
    baselineOf,
    BETTER_BACKGROUND,
    changeBackground,
    changeOfTrend,
    checksChange,
    deltaText,
    samplesChange,
    searchWithBaseline,
    TIME_DELTA_HELP,
    timeDeltaText,
    toggledBaseline,
    trendClass,
    type Change,
    referenceColumn,
} from "./compare";
import { contractDifferences, type Difference } from "./contractDiff";
import {
    COLUMN_MIN_WIDTH,
    COMPARE_SCROLL,
    COMPARE_TABLE,
    LABEL_COLUMN_WIDTH,
    PRINT_COLUMN_WIDTH,
    PRINT_LABEL_COLUMN_WIDTH,
    tableMinWidth,
} from "./layout";
import { comparePrintLine } from "./printing";
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
import { agentEvaluationHref } from "../../routes/links";
import { useReportAgent } from "../../shell/reportAgent";
import { PrintButton } from "../../print/PrintButton";
import { usePrintOnLoad } from "../../print/usePrintOnLoad";
import { ToggleChip } from "../../toggle/ToggleChip";

const PRINT_WIDTHS = {
    "--print-label-width": `${String(PRINT_LABEL_COLUMN_WIDTH)}px`,
    "--print-column-width": `${String(PRINT_COLUMN_WIDTH)}px`,
};

export function ComparePage({ runs, group, agent, baseline }: { runs: string; group: string; agent: string; baseline: string }) {
    const selection = selectionOf(runs, group, agent);
    return selection.kind === "error" ? (
        <Problem message={selection.message} />
    ) : (
        <Comparison selection={selection} baseline={baselineOf(baseline)} />
    );
}

function Comparison({ selection, baseline }: { selection: ValidSelection; baseline: string | null }) {
    const [baselineId, setBaselineId] = useState(baseline);
    const chooseBaseline = (runId: string) => {
        const next = toggledBaseline(baselineId, runId);
        setBaselineId(next);
        const { pathname, search, hash } = window.location;
        window.history.replaceState(null, "", `${pathname}${searchWithBaseline(search, next)}${hash}`);
    };
    const query = useQuery(comparisonQuery(selection, baselineId));

    if (query.isPending) return <Spinner label="Loading the comparison" />;
    if (query.isError) return <Problem message={query.error.message} />;
    return <Loaded selection={selection} comparison={query.data} onBaseline={chooseBaseline} />;
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
    useReportAgent(agent);
    const versionIds = [...new Set(comparison.runs.map((column) => column.run.agentVersionId))];
    const base = useQuery(comparedVersionsQuery(agent, versionIds));

    if (base.isPending) return <Spinner label="Loading the comparison" />;
    if (base.isError) return <Problem message={base.error.message} />;
    const details = base.data;
    const differences =
        comparison.runs.length > 1
            ? contractDifferences(
                  comparison.runs.map((column) => ({
                      versionId: column.run.agentVersionId,
                      contract: (details.find((detail) => detail.id === column.run.agentVersionId) ?? details[0])?.content ?? {
                          agents: [],
                      },
                      dataVersion: column.run.dataVersion ?? null,
                  })),
                  referenceColumn(comparison),
              )
            : [];
    return <View selection={selection} variants={comparison} differences={differences} onBaseline={onBaseline} />;
}

function View({
    selection,
    variants,
    differences,
    onBaseline,
}: {
    selection: ValidSelection;
    variants: RunComparison;
    differences: Difference[];
    onBaseline: (runId: string) => void;
}) {
    const queryClient = useQueryClient();
    const [onlyDifferences, setOnlyDifferences] = useState(selection.kind === "runs");
    const [byTag, setByTag] = useState(false);
    const [selectedTags, selectTags] = useTagFilter();
    const [open, toggleOpen] = useOpenScenarios();
    usePrintOnLoad(true);
    const filtered = selectedTags.length > 0;
    const columns = variants.runs.length;
    const tagged = variants.scenarios.filter((scenario) => keepsTags(scenario.tags, selectedTags));
    const scenarios = tagged.filter((scenario) => !onlyDifferences || scenarioDiffers(scenario.cells));
    const sections = scenarioSections(scenarios, byTag, selectedTags);
    const tagRows = visibleTagRows(selectedTagRows(variants.tags, selectedTags));
    const { baseline } = variants;
    const compared = baseline?.runs ?? null;
    const comparedTags = baseline == null ? null : selectedTagRows(baseline.tags, selectedTags);
    const baselineColumn = baseline == null ? -1 : variants.runs.findIndex((variant) => variant.run.id === baseline.runId);
    const totalsOf = (column: number) => (filtered ? columnTotals(scenarios, column) : variants.runs[column]?.run);
    const summaryChange = (column: number, change: typeof checksChange | typeof samplesChange): Change | null => {
        const own = totalsOf(column);
        const base = totalsOf(baselineColumn);
        return baseline == null || column === baselineColumn || own === undefined || base === undefined ? null : change(own, base);
    };

    return (
        <div className="flex flex-col gap-4">
            <div className="no-print flex flex-wrap items-center gap-2.5">
                <Anchor href="/">Agents</Anchor>/<Anchor href={agentEvaluationHref(variants.agent)}>{variants.agent}</Anchor>
            </div>
            <h2 className="text-[26px] leading-tight font-bold">Compare runs of {variants.agent}</h2>
            <p className="text-muted-foreground">
                {variants.group == null ? "" : `Group ${variants.group} · `}
                {columns} {columns === 1 ? "run" : "runs"}, one column each.
                {compared === null
                    ? " A check passes when its pass rate reaches its threshold; the best column of a row is marked. Pick a baseline by a column's header to read the others against it."
                    : " Every run is read against the baseline: a cell better than the baseline's is green, a worse one red. Pick another by its header, or click the baseline again to go back to the plain view. A check passes when its pass rate reaches its threshold."}
            </p>
            {selection.kind === "group" && (
                <NoteBox
                    key={selection.group}
                    initial={variants.groupNote}
                    save={async (note) => {
                        await setGroupNote(variants.agent, selection.group, note);
                        await queryClient.invalidateQueries({ queryKey: compareKeys.all });
                    }}
                    placeholder="What is this experiment about?"
                />
            )}
            <p className="print-only print-line">
                {comparePrintLine({
                    agent: variants.agent,
                    group: variants.group,
                    runIds: variants.runs.map((variant) => variant.run.id),
                    baselineId: baseline?.runId,
                    selectedTags,
                    onlyDifferences,
                    byTag,
                })}
            </p>
            <div className="no-print">
                <TagFilter
                    tags={[...variants.scenarios.flatMap((scenario) => scenario.tags), ...variants.tags.flatMap((row) => row.tag ?? [])]}
                    selected={selectedTags}
                    onChange={selectTags}
                />
            </div>
            <PrintButton />
            <Paper className="compare-paper">
                <div className={COMPARE_SCROLL}>
                    <table className={COMPARE_TABLE} style={{ ...PRINT_WIDTHS, minWidth: tableMinWidth(columns) }}>
                        <thead>
                            <tr>
                                <th style={{ width: LABEL_COLUMN_WIDTH }} className="compare-label-column" />
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
                                        <th
                                            key={variant.run.id}
                                            style={{ minWidth: COLUMN_MIN_WIDTH }}
                                            className="compare-run-column align-top"
                                        >
                                            <ColumnHeader agent={variants.agent} variant={variant} baseline={baselineInfo} />
                                        </th>
                                    );
                                })}
                            </tr>
                        </thead>
                        <WhatDiffers
                            agent={variants.agent}
                            columns={variants.runs}
                            reference={referenceColumn(variants)}
                            differences={differences}
                            colSpan={columns + 1}
                        />
                        <tbody>
                            <SectionRow columns={columns} title="Summary" />
                            <tr>
                                <td className="font-medium">
                                    <Tooltip label={RUN_CHECKS_HELP}>
                                        <span className="cursor-help">{filteredLabel("Checks", selectedTags)}</span>
                                    </Tooltip>
                                </td>
                                {variants.runs.map((variant, column) => {
                                    const change = summaryChange(column, checksChange);
                                    return (
                                        <td
                                            key={variant.run.id}
                                            className={cn("no-wrap", changeBackground(change))}
                                            data-change={change ?? undefined}
                                        >
                                            <ChecksCount run={filtered ? columnTotals(scenarios, column) : variant.run} />
                                            <ChangeNote change={change} />
                                        </td>
                                    );
                                })}
                            </tr>
                            <tr>
                                <td className="font-medium">
                                    <Tooltip label={RUN_SAMPLES_HELP}>
                                        <span className="cursor-help">{filteredLabel("Samples", selectedTags)}</span>
                                    </Tooltip>
                                </td>
                                {variants.runs.map((variant, column) => {
                                    const change = summaryChange(column, samplesChange);
                                    return (
                                        <td
                                            key={variant.run.id}
                                            className={cn("no-wrap", changeBackground(change))}
                                            data-change={change ?? undefined}
                                        >
                                            <SamplesCount run={filtered ? columnTotals(scenarios, column) : variant.run} />
                                            <ChangeNote change={change} />
                                        </td>
                                    );
                                })}
                            </tr>
                            <tr>
                                <td className="font-medium">{filtered ? "Turn time (all scenarios)" : "Turn time"}</td>
                                {variants.runs.map((variant, column) => (
                                    <td key={variant.run.id}>
                                        <span>{durationSummary(variant)?.replace("Turn time: ", "") ?? "—"}</span>
                                        <TimeChanges compared={compared?.[column]} />
                                    </td>
                                ))}
                            </tr>
                            {variants.runs.some((variant) => variant.judgeModel != null) && (
                                <tr>
                                    <td className="font-medium">Judge</td>
                                    {variants.runs.map((variant, column) => (
                                        <td key={variant.run.id}>
                                            <Judge
                                                model={variant.judgeModel}
                                                changed={compared?.[column]?.judgeChanged ?? false}
                                                baselineModel={baselineJudgeModel(variants)}
                                            />
                                        </td>
                                    ))}
                                </tr>
                            )}
                        </tbody>
                        <tbody>
                            <SectionRow columns={columns} title="Pass rate per tag" help={TAG_RATE_HELP} />
                            {tagRows.map((row) => {
                                const best = baseline == null ? bestColumns(row.rates.map(({ passRate }) => passRate)) : new Set<number>();
                                const changes = comparedTags?.find((candidate) => candidate.tag === row.tag);
                                return (
                                    <tr key={row.tag ?? "\u0000all"}>
                                        <td className="wrap-key font-medium">{row.tag ?? "All"}</td>
                                        {row.rates.map((rate, column) => {
                                            const { passRate } = rate;
                                            const change = changes?.rates[column]?.change;
                                            const cellChange = changeOfTrend(change?.trend);
                                            const hint = coverageHint(row.tag, rate);
                                            return (
                                                <td
                                                    key={variants.runs[column]?.run.id}
                                                    className={cn(
                                                        "no-wrap",
                                                        best.has(column)
                                                            ? cn(BETTER_BACKGROUND, "font-bold")
                                                            : changeBackground(cellChange),
                                                    )}
                                                    data-best={best.has(column) ? "" : undefined}
                                                    data-change={cellChange ?? undefined}
                                                >
                                                    <div className="flex items-center gap-2.5">
                                                        <span>{passRate == null ? "—" : formatPercent(passRate)}</span>
                                                        {hint !== null && (
                                                            <span
                                                                className="wrap-key text-xs font-normal whitespace-normal text-muted-foreground"
                                                                title={hint}
                                                            >
                                                                {hint}
                                                            </span>
                                                        )}
                                                        {change != null && (
                                                            <span className={trendClass(change.trend)}>{deltaText(change)}</span>
                                                        )}
                                                    </div>
                                                    <ChangeNote change={cellChange} />
                                                </td>
                                            );
                                        })}
                                    </tr>
                                );
                            })}
                        </tbody>
                        <tbody>
                            <tr>
                                <th colSpan={columns + 1} className="bg-secondary">
                                    <div className="flex flex-wrap items-center justify-between gap-4">
                                        Scenarios
                                        <div className="no-print flex flex-wrap items-center gap-5">
                                            <ToggleChip
                                                label="Only rows that differ"
                                                checked={onlyDifferences}
                                                onChange={setOnlyDifferences}
                                            />
                                            <SegmentedControl
                                                label="List scenarios"
                                                value={byTag ? "tag" : "scenario"}
                                                onValueChange={(value) => {
                                                    setByTag(value === "tag");
                                                }}
                                                options={[
                                                    { value: "scenario", label: "By scenario" },
                                                    { value: "tag", label: "By tag" },
                                                ]}
                                            />
                                        </div>
                                    </div>
                                </th>
                            </tr>
                            {sections.map((section) => (
                                <ScenarioRows
                                    key={section.title ?? ""}
                                    section={section}
                                    columns={variants.runs}
                                    baseline={baseline ?? null}
                                    open={open}
                                    onToggle={toggleOpen}
                                />
                            ))}
                        </tbody>
                    </table>
                </div>
                {scenarios.length === 0 && (
                    <p className="mt-3 text-muted-foreground">
                        {tagged.length > 0
                            ? "Every scenario came out the same in all runs — switch off Only rows that differ to see them."
                            : filtered
                              ? "No scenario has any of the selected tags."
                              : "No run has a result for a scenario yet."}
                    </p>
                )}
            </Paper>
        </div>
    );
}

function baselineJudgeModel({ baseline }: RunComparison): string | null {
    return baseline?.runs.find((compared) => compared.run.id === baseline.runId)?.judgeModel ?? null;
}

function TimeChanges({ compared }: { compared: ComparedRun | undefined }) {
    if (compared === undefined) return null;
    const changes = [
        ...(compared.medianChange == null ? [] : [{ label: "median", change: compared.medianChange }]),
        ...(compared.p95Change == null ? [] : [{ label: "p95", change: compared.p95Change }]),
    ];
    if (changes.length === 0) return null;
    return (
        <Tooltip label={TIME_DELTA_HELP}>
            <div className="flex w-fit cursor-help items-center gap-2.5 text-xs">
                {changes.map(({ label, change }) => (
                    <span key={label} className={trendClass(change.trend)}>
                        {label} {timeDeltaText(change)}
                    </span>
                ))}
            </div>
        </Tooltip>
    );
}

function Judge({ model, changed, baselineModel }: { model: string | null | undefined; changed: boolean; baselineModel: string | null }) {
    if (model == null) return <span>—</span>;
    return (
        <div className="flex items-center gap-1">
            <span className="no-print">
                <ModelJudgedIcon model={model} />
            </span>
            <span className="wrap-key">{model}</span>
            {changed && (
                <Tooltip
                    label={`The baseline was judged by ${baselineModel ?? "another model"}: a different verdict may come from the judge, not the agent`}
                >
                    <Badge variant="neutral" className="cursor-help">
                        ≠ baseline
                    </Badge>
                </Tooltip>
            )}
        </div>
    );
}

function SectionRow({ columns, title, help }: { columns: number; title: string; help?: string }) {
    return (
        <tr>
            <th colSpan={columns + 1} className="bg-secondary">
                {help === undefined ? title : <HeaderHelp label={title} help={help} />}
            </th>
        </tr>
    );
}

function Problem({ message }: { message: string }) {
    return (
        <div className="flex flex-col gap-3">
            <Alert variant="destructive">
                <p className="font-semibold">Cannot compare</p>
                <p>{message}</p>
            </Alert>
            <Anchor href="/">Back to the list of agents</Anchor>
        </div>
    );
}
