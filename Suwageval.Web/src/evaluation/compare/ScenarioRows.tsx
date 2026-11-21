import { Anchor, Badge, cn } from "@maniumek/suwfrontkit";
import { IconChevronDown, IconChevronRight } from "@tabler/icons-react";

import type { BaselineComparison, VariantCell, VariantRun, VariantScenario } from "./api";
import { ChangeNote } from "./ChangeNote";
import { BETTER_BACKGROUND, changeBackground, checkCellChange, scenarioCellChange, type Change } from "./compare";
import { COLUMN_MIN_WIDTH, COMPARE_SCROLL, COMPARE_TABLE, LABEL_COLUMN_WIDTH, tableMinWidth } from "./layout";
import {
    bestColumns,
    cellShare,
    cellStatus,
    cellText,
    checkMatrix,
    checkVerdictText,
    columnName,
    noAnswerRows,
    noAnswerText,
    type ScenarioSection,
} from "./variants";
import { OnPaper } from "../../print/OnPaper";
import { ModelJudgedIcon } from "../../verdict/ModelJudgedIcon";
import { scenarioInRunHref } from "../../routes/links";
import { shortId } from "../../format/names";

export function ScenarioRows({
    section,
    columns,
    baseline,
    open,
    onToggle,
}: {
    section: ScenarioSection;
    columns: readonly VariantRun[];
    baseline: BaselineComparison | null;
    open: readonly string[];
    onToggle: (key: string) => void;
}) {
    return (
        <>
            {section.title !== null && (
                <tr>
                    <td colSpan={columns.length + 1} className="wrap-key text-xs font-semibold text-muted-foreground">
                        {section.title}
                    </td>
                </tr>
            )}
            {section.scenarios.map((scenario) => {
                const best = baseline === null ? bestColumns(scenario.cells.map(cellShare)) : new Set<number>();
                const baselineColumn = baseline === null ? -1 : columns.findIndex((column) => column.run.id === baseline.runId);
                const baselineCell = scenario.cells[baselineColumn] ?? null;
                const changes =
                    baseline === null
                        ? []
                        : scenario.cells.map((cell, column) => (column === baselineColumn ? null : scenarioCellChange(cell, baselineCell)));
                const expanded = open.includes(scenario.key);
                const detailId = `scenario-detail-${scenario.key}`;
                return (
                    <ScenarioRow
                        key={scenario.key}
                        scenario={scenario}
                        columns={columns}
                        best={best}
                        changes={changes}
                        baseline={baseline}
                        baselineColumn={baselineColumn}
                        expanded={expanded}
                        detailId={detailId}
                        onToggle={() => {
                            onToggle(scenario.key);
                        }}
                    />
                );
            })}
        </>
    );
}

function ScenarioRow({
    scenario,
    columns,
    best,
    changes,
    baseline,
    baselineColumn,
    expanded,
    detailId,
    onToggle,
}: {
    scenario: VariantScenario;
    columns: readonly VariantRun[];
    best: ReadonlySet<number>;
    changes: readonly (Change | null)[];
    baseline: BaselineComparison | null;
    baselineColumn: number;
    expanded: boolean;
    detailId: string;
    onToggle: () => void;
}) {
    return (
        <>
            <tr>
                <td className="wrap-key">
                    <button
                        type="button"
                        onClick={onToggle}
                        aria-expanded={expanded}
                        aria-controls={detailId}
                        className="no-print flex w-full items-start gap-1 text-left"
                    >
                        {expanded ? <IconChevronDown size={16} aria-hidden /> : <IconChevronRight size={16} aria-hidden />}
                        <span className="wrap-key">{scenario.key}</span>
                    </button>
                    <span className="wrap-key print-only-inline">{scenario.key}</span>
                </td>
                {scenario.cells.map((cell, column) => (
                    <CellView key={columns[column]?.run.id} cell={cell} best={best.has(column)} change={changes[column] ?? null} />
                ))}
            </tr>
            <tr className={expanded ? undefined : "folded-row"}>
                <td colSpan={columns.length + 1} id={detailId}>
                    <ScenarioDetail scenario={scenario} columns={columns} baseline={baseline} baselineColumn={baselineColumn} />
                </td>
            </tr>
        </>
    );
}

function CellView({ cell, best, change }: { cell: VariantCell | null; best: boolean; change: Change | null }) {
    const status = cellStatus(cell);
    const noAnswer = noAnswerText(cell);
    return (
        <td className={best ? BETTER_BACKGROUND : changeBackground(change)} data-change={change ?? undefined}>
            {status === null ? (
                <span>{cellText(cell)}</span>
            ) : (
                <div className="flex flex-col items-start gap-0.5">
                    <Badge variant={status.variant}>{status.label}</Badge>
                    <span className={cn("text-xs", best && "font-bold")}>{cellText(cell)}</span>
                    {noAnswer !== null && <span className="text-xs text-muted-foreground">{noAnswer}</span>}
                    <ChangeNote change={change} />
                </div>
            )}
        </td>
    );
}

function ScenarioDetail({
    scenario,
    columns,
    baseline,
    baselineColumn,
}: {
    scenario: VariantScenario;
    columns: readonly VariantRun[];
    baseline: BaselineComparison | null;
    baselineColumn: number;
}) {
    const matrix = checkMatrix(scenario.cells);
    const noAnswers = noAnswerRows(scenario.cells);
    return (
        <div className="flex flex-col gap-3 py-2.5">
            {matrix.length === 0 ? (
                <p className="text-sm text-muted-foreground">No check ran for this scenario.</p>
            ) : (
                <div className={COMPARE_SCROLL}>
                    <table
                        className={cn(COMPARE_TABLE, "[&_td+td]:border-l [&_th+th]:border-l")}
                        style={{ minWidth: tableMinWidth(columns.length) }}
                        aria-label={`Checks of ${scenario.key} by run`}
                    >
                        <thead>
                            <tr>
                                <th style={{ width: LABEL_COLUMN_WIDTH }} className="compare-label-column">
                                    Check
                                </th>
                                {columns.map((column) => (
                                    <th key={column.run.id} style={{ minWidth: COLUMN_MIN_WIDTH }} className="wrap-key compare-run-column">
                                        {columnName(column)}
                                    </th>
                                ))}
                            </tr>
                        </thead>
                        <tbody>
                            {matrix.map((row) => {
                                const compared = baseline?.checks.find(
                                    (candidate) => candidate.scenarioKey === scenario.key && candidate.checkKey === row.key,
                                );
                                const baselineCheck = row.checks[baselineColumn] ?? null;
                                return (
                                    <tr key={row.key}>
                                        <td className="wrap-key">
                                            <div className="flex items-start gap-1">
                                                {row.checks.some((check) => check?.judgedByModel === true) && (
                                                    <OnPaper text="model-judged">
                                                        <ModelJudgedIcon />
                                                    </OnPaper>
                                                )}
                                                <span>{row.key}</span>
                                            </div>
                                        </td>
                                        {row.checks.map((check, column) => {
                                            const change =
                                                baseline === null || column === baselineColumn
                                                    ? null
                                                    : checkCellChange(check, baselineCheck, compared, column);
                                            return (
                                                <td
                                                    key={columns[column]?.run.id}
                                                    className={changeBackground(change)}
                                                    data-change={change ?? undefined}
                                                >
                                                    {check === null ? (
                                                        <span className="text-muted-foreground">—</span>
                                                    ) : (
                                                        <div className="flex flex-col items-start gap-0.5">
                                                            <Badge variant={check.passed ? "success" : "danger"}>
                                                                {checkVerdictText(check)}
                                                            </Badge>
                                                            <ul className="text-xs">
                                                                {check.failures.map((failure) => (
                                                                    <li key={failure.sample} className="wrap-key">
                                                                        {check.samples > 1 ? `sample ${String(failure.sample)}: ` : ""}
                                                                        {failure.message}
                                                                    </li>
                                                                ))}
                                                            </ul>
                                                        </div>
                                                    )}
                                                    <ChangeNote change={change} />
                                                </td>
                                            );
                                        })}
                                    </tr>
                                );
                            })}
                        </tbody>
                    </table>
                </div>
            )}
            {noAnswers.length > 0 && (
                <div className="flex flex-col gap-0.5">
                    <h5 className="text-base font-bold">Samples without an answer</h5>
                    <ul className="text-sm">
                        {noAnswers.map((item) => (
                            <li key={`${String(item.column)}-${String(item.sample)}`} className="wrap-key">
                                {nameOf(columns[item.column])}, sample {item.sample}: {item.error ?? "the agent returned nothing"}
                            </li>
                        ))}
                    </ul>
                </div>
            )}
            <div className="no-print flex flex-wrap items-center gap-4">
                {columns.map((column, index) =>
                    scenario.cells[index] == null ? null : (
                        <Anchor key={column.run.id} className="text-sm" href={scenarioInRunHref(column.run.id, scenario.key)}>
                            Open in run {shortId(column.run.id)}
                        </Anchor>
                    ),
                )}
            </div>
        </div>
    );
}

const nameOf = (column: VariantRun | undefined) => (column === undefined ? "" : columnName(column));
