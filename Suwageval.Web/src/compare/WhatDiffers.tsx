import { Button, Loader, Table, Text } from "@mantine/core";
import { useState } from "react";

import { useRequest } from "../api/useRequest";
import { LineDiffView } from "../diff/LineDiffView";
import { blocksFrom } from "../diff/lineDiff";
import { type Difference } from "./contractDiff";
import type { VariantRun } from "./api";
import { columnName } from "./variants";
import { fetchVersionDiff } from "../agents/versions/api";

const nameOf = (column: VariantRun | undefined) => (column === undefined ? "" : columnName(column));

const WHAT_DIFFERS_HELP =
    "The fields of the agent's contract that are not the same in the versions these columns ran: models and their parameters, tools, other config lines, the retrieval data version. Each column is read against the baseline when one is picked, else against the first column. A prompt is only marked changed — open its diff to read it.";

/** "What differs" as the table's first section: one row per contract field that differs between the columns' versions; none when they ran one version. */
export function WhatDiffers({
    agent,
    columns,
    reference,
    differences,
    colSpan,
}: {
    agent: string;
    columns: readonly VariantRun[];
    /** The column the others are read against: the baseline's, else the first. */
    reference: number;
    /** Null while the contracts are loading. */
    differences: Difference[] | null;
    colSpan: number;
}) {
    // The columns whose prompt diff (against the reference column's version) is open.
    const [openDiffs, setOpenDiffs] = useState<readonly number[]>([]);
    const versions = new Set(columns.map((column) => column.run.agentVersionId));
    const toggle = (column: number) => {
        setOpenDiffs(openDiffs.includes(column) ? openDiffs.filter((open) => open !== column) : [...openDiffs, column]);
    };

    return (
        <Table.Tbody>
            <Table.Tr>
                <Table.Th colSpan={colSpan} bg="var(--mantine-color-default)" title={WHAT_DIFFERS_HELP}>
                    What differs
                </Table.Th>
            </Table.Tr>
            {versions.size === 1 && (
                <Table.Tr>
                    <Table.Td colSpan={colSpan}>
                        <Text size="sm" c="dimmed">
                            Every column ran the same agent version: its contract does not differ.
                        </Text>
                    </Table.Td>
                </Table.Tr>
            )}
            {versions.size > 1 && differences === null && (
                <Table.Tr>
                    <Table.Td colSpan={colSpan}>
                        <Loader size="xs" aria-label="Loading the contracts" />
                    </Table.Td>
                </Table.Tr>
            )}
            {versions.size > 1 && differences?.length === 0 && (
                <Table.Tr>
                    <Table.Td colSpan={colSpan}>
                        <Text size="sm" c="dimmed">
                            The versions differ in nothing this view lists (models, parameters, tools, prompts, other config, data version).
                        </Text>
                    </Table.Td>
                </Table.Tr>
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
                    colSpan={colSpan}
                />
            ))}
        </Table.Tbody>
    );
}

function DifferenceRows({
    agent,
    columns,
    reference,
    difference,
    openDiffs,
    onToggle,
    colSpan,
}: {
    agent: string;
    columns: readonly VariantRun[];
    reference: number;
    difference: Difference;
    openDiffs: readonly number[];
    onToggle: (column: number) => void;
    colSpan: number;
}) {
    // Every changed prompt's diff is in the page; one the person did not open is hidden on the screen and still printed (print.css).
    const changed = difference.isPrompt ? difference.cells.flatMap((cell, column) => (cell.changed ? [column] : [])) : [];
    return (
        <>
            <Table.Tr>
                <Table.Td fw={500} className="wrap-key">
                    {difference.label}
                </Table.Td>
                {difference.cells.map((cell, column) => (
                    <Table.Td key={columns[column]?.run.id} className="wrap-key">
                        {difference.isPrompt && cell.changed ? (
                            <>
                                <Button
                                    className="no-print"
                                    size="compact-xs"
                                    variant="light"
                                    aria-expanded={openDiffs.includes(column)}
                                    aria-label={`Prompt changed in ${nameOf(columns[column])}: ${openDiffs.includes(column) ? "hide" : "show"} the diff`}
                                    onClick={() => {
                                        onToggle(column);
                                    }}
                                >
                                    {cell.text ?? ""} · {openDiffs.includes(column) ? "hide diff" : "show diff"}
                                </Button>
                                <Text size="sm" fw={600} className="print-only-inline">
                                    {cell.text ?? ""}
                                </Text>
                            </>
                        ) : (
                            <Text size="sm" fw={cell.changed ? 600 : 400} c={cell.text === null ? "dimmed" : "inherit"}>
                                {cell.text ?? "—"}
                            </Text>
                        )}
                    </Table.Td>
                ))}
            </Table.Tr>
            {changed.map((column) => (
                <Table.Tr key={`diff-${String(column)}`} className={openDiffs.includes(column) ? undefined : "folded-row"}>
                    <Table.Td colSpan={colSpan}>
                        <PromptDiff agent={agent} columns={columns} reference={reference} column={column} />
                    </Table.Td>
                </Table.Tr>
            ))}
        </>
    );
}

/** The server's line diff of the whole contract between the reference column's version and this column's. */
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
    const state = useRequest(`${first?.run.agentVersionId ?? ""}/${other?.run.agentVersionId ?? ""}`, (signal) =>
        fetchVersionDiff(agent, first?.run.agentVersionId ?? "", other?.run.agentVersionId ?? "", signal),
    );
    if (first === undefined || other === undefined) return null;
    return (
        <>
            <Text size="sm" fw={500} mb={4}>
                Contract diff: {columnName(first)} → {columnName(other)}
            </Text>
            {state.kind === "loading" && <Loader size="xs" aria-label="Loading the diff" />}
            {state.kind === "error" && (
                <Text size="sm" c="red">
                    {state.error.message}
                </Text>
            )}
            {state.kind === "loaded" && <LineDiffView blocks={blocksFrom(state.value, first.run.agentVersionId)} />}
        </>
    );
}
