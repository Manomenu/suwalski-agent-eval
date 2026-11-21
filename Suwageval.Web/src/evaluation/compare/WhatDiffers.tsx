import { Button, cn, Diff, Spinner } from "@maniumek/suwfrontkit";
import { IconChevronDown, IconChevronRight } from "@tabler/icons-react";
import { useQuery } from "@tanstack/react-query";
import { useState } from "react";

import { Failure } from "../../api/Failure";
import { type Difference } from "./contractDiff";
import { type VariantRun } from "./api";
import { versionDiffQuery } from "../../versions/api";
import { columnName } from "./variants";
import { readWhatDiffersOpen, saveWhatDiffersOpen, whatDiffersTitle } from "./whatDiffersOpening";

const nameOf = (column: VariantRun | undefined) => (column === undefined ? "" : columnName(column));

const WHAT_DIFFERS_HELP =
    "The fields of the agent's contract that are not the same in the versions these columns ran: models and their parameters, tools, other config lines, the retrieval data version. Each column is read against the baseline when one is picked, else against the first column. A prompt is only marked changed — open its diff to read it. Folded, the section still prints whole.";

export function WhatDiffers({
    agent,
    columns,
    reference,
    differences,
    colSpan,
}: {
    agent: string;
    columns: readonly VariantRun[];
    reference: number;
    differences: Difference[] | null;
    colSpan: number;
}) {
    const [openDiffs, setOpenDiffs] = useState<readonly number[]>([]);
    const [open, setOpen] = useState(readWhatDiffersOpen);
    const versions = new Set(columns.map((column) => column.run.agentVersionId));
    const toggle = (column: number) => {
        setOpenDiffs(openDiffs.includes(column) ? openDiffs.filter((opened) => opened !== column) : [...openDiffs, column]);
    };
    const toggleSection = () => {
        saveWhatDiffersOpen(!open);
        setOpen(!open);
    };
    const title = whatDiffersTitle(versions.size === 1 ? 0 : (differences?.length ?? null));
    const folded = open ? undefined : "folded-row";

    return (
        <tbody>
            <tr>
                <th colSpan={colSpan} className="bg-secondary" title={WHAT_DIFFERS_HELP}>
                    <button
                        type="button"
                        onClick={toggleSection}
                        aria-expanded={open}
                        className="no-print flex items-center gap-1 text-left"
                    >
                        {open ? <IconChevronDown size={16} aria-hidden /> : <IconChevronRight size={16} aria-hidden />}
                        {title}
                    </button>
                    <span className="print-only-inline">What differs</span>
                </th>
            </tr>
            {versions.size === 1 && (
                <tr className={folded}>
                    <td colSpan={colSpan}>
                        <p className="text-sm text-muted-foreground">
                            Every column ran the same agent version: its contract does not differ.
                        </p>
                    </td>
                </tr>
            )}
            {versions.size > 1 && differences === null && (
                <tr className={folded}>
                    <td colSpan={colSpan}>
                        <Spinner size="xs" label="Loading the contracts" />
                    </td>
                </tr>
            )}
            {versions.size > 1 && differences?.length === 0 && (
                <tr className={folded}>
                    <td colSpan={colSpan}>
                        <p className="text-sm text-muted-foreground">
                            The versions differ in nothing this view lists (models, parameters, tools, prompts, other config, data version).
                        </p>
                    </td>
                </tr>
            )}
            {differences?.map((difference) => (
                <DifferenceRows
                    key={difference.label}
                    agent={agent}
                    columns={columns}
                    reference={reference}
                    difference={difference}
                    openDiffs={openDiffs}
                    onToggle={toggle}
                    folded={!open}
                    colSpan={colSpan}
                />
            ))}
        </tbody>
    );
}

function DifferenceRows({
    agent,
    columns,
    reference,
    difference,
    openDiffs,
    onToggle,
    folded,
    colSpan,
}: {
    agent: string;
    columns: readonly VariantRun[];
    reference: number;
    difference: Difference;
    openDiffs: readonly number[];
    onToggle: (column: number) => void;
    folded: boolean;
    colSpan: number;
}) {
    const changed = difference.isPrompt ? difference.cells.flatMap((cell, column) => (cell.changed ? [column] : [])) : [];
    return (
        <>
            <tr className={folded ? "folded-row" : undefined}>
                <td className="wrap-key font-medium">{difference.label}</td>
                {difference.cells.map((cell, column) => (
                    <td key={columns[column]?.run.id} className="wrap-key">
                        {difference.isPrompt && cell.changed ? (
                            <>
                                <Button
                                    className="no-print"
                                    size="xs"
                                    variant="outline"
                                    aria-expanded={openDiffs.includes(column)}
                                    aria-label={`Prompt changed in ${nameOf(columns[column])}: ${openDiffs.includes(column) ? "hide" : "show"} the diff`}
                                    onClick={() => {
                                        onToggle(column);
                                    }}
                                >
                                    {cell.text ?? ""} · {openDiffs.includes(column) ? "hide diff" : "show diff"}
                                </Button>
                                <span className="print-only-inline font-semibold">{cell.text ?? ""}</span>
                            </>
                        ) : (
                            <span className={cn(cell.changed && "font-semibold", cell.text === null && "text-muted-foreground")}>
                                {cell.text ?? "—"}
                            </span>
                        )}
                    </td>
                ))}
            </tr>
            {changed.map((column) => (
                <tr key={`diff-${String(column)}`} className={!folded && openDiffs.includes(column) ? undefined : "folded-row"}>
                    <td colSpan={colSpan}>
                        <PromptDiff agent={agent} columns={columns} reference={reference} column={column} />
                    </td>
                </tr>
            ))}
        </>
    );
}

function PromptDiff({
    agent,
    columns,
    reference,
    column,
}: {
    agent: string;
    columns: readonly VariantRun[];
    reference: number;
    column: number;
}) {
    const first = columns[reference];
    const other = columns[column];
    const diff = useQuery({
        ...versionDiffQuery(agent, first?.run.agentVersionId ?? "", other?.run.agentVersionId ?? ""),
        enabled: first !== undefined && other !== undefined,
    });
    if (first === undefined || other === undefined) return null;
    const firstIsOlder = diff.data?.from.id === first.run.agentVersionId;
    return (
        <>
            <p className="mb-1 text-sm font-medium">
                Contract diff: {columnName(first)} → {columnName(other)}
            </p>
            {diff.isPending && <Spinner size="xs" label="Loading the diff" />}
            {diff.isError && <Failure className="text-sm text-destructive" error={diff.error} />}
            {diff.data !== undefined && (
                <Diff
                    before={firstIsOlder ? diff.data.fromYaml : diff.data.toYaml}
                    after={firstIsOlder ? diff.data.toYaml : diff.data.fromYaml}
                />
            )}
        </>
    );
}
