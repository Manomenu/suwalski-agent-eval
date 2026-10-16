import { Button, Text } from "@mantine/core";
import { useMemo, useState } from "react";

import { hangingIndent } from "../checks/definition";
import { diffBlocks, diffRows, type DiffRow } from "./versionDiff";

const MARKERS: Record<DiffRow["kind"], string> = { added: "+", removed: "−", unchanged: " " };

function Row({ row }: { row: DiffRow }) {
    const hang = hangingIndent(row.text);
    return (
        <div className={`version-diff-row version-diff-row-${row.kind}`}>
            <span className="version-diff-number">{row.oldNumber}</span>
            <span className="version-diff-number">{row.newNumber}</span>
            <span aria-hidden>{MARKERS[row.kind]}</span>
            <span className="version-diff-text" style={{ paddingLeft: `${String(hang)}ch`, textIndent: `-${String(hang)}ch` }}>
                {row.text}
            </span>
        </div>
    );
}

/** The unified line diff of two texts: changes with their context, long unchanged runs folded until clicked. */
export function VersionDiff({ from, to }: { from: string; to: string }) {
    const blocks = useMemo(() => diffBlocks(diffRows(from, to)), [from, to]);
    const [expanded, setExpanded] = useState<ReadonlySet<number>>(new Set());

    if (blocks.length === 0) return <Text c="dimmed">The contents are identical.</Text>;
    return (
        <div className="version-diff">
            {blocks.map((block, index) =>
                block.kind === "folded" && !expanded.has(index) ? (
                    <Button
                        key={index}
                        variant="subtle"
                        color="gray"
                        fullWidth
                        radius={0}
                        onClick={() => {
                            setExpanded(new Set(expanded).add(index));
                        }}
                    >
                        {`⋯ ${String(block.rows.length)} unchanged lines`}
                    </Button>
                ) : (
                    block.rows.map((row) => <Row key={`${String(row.oldNumber)}-${String(row.newNumber)}`} row={row} />)
                ),
            )}
        </div>
    );
}
