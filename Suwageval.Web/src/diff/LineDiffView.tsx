import { Button, Text } from "@mantine/core";
import { useState } from "react";

import type { components } from "../api/openapi";
import { hangingIndent } from "../checks/definition";

type DiffBlock = components["schemas"]["DiffBlock"];
type DiffRow = components["schemas"]["DiffRow"];

const MARKERS: Record<DiffRow["kind"], string> = { Added: "+", Removed: "−", Same: " " };
const CLASS_SUFFIX: Record<DiffRow["kind"], string> = { Added: "added", Removed: "removed", Same: "unchanged" };

function Row({ row }: { row: DiffRow }) {
    const hang = hangingIndent(row.text);
    return (
        <div className={`line-diff-row line-diff-row-${CLASS_SUFFIX[row.kind]}`}>
            <span className="line-diff-number">{row.fromLine}</span>
            <span className="line-diff-number">{row.toLine}</span>
            <span aria-hidden>{MARKERS[row.kind]}</span>
            <span className="line-diff-text" style={{ paddingLeft: `${String(hang)}ch`, textIndent: `-${String(hang)}ch` }}>
                {row.text}
            </span>
        </div>
    );
}

/** The unified line diff the server computed: changes with their context, long unchanged runs folded until clicked. */
export function LineDiffView({ blocks }: { blocks: readonly DiffBlock[] }) {
    const [expanded, setExpanded] = useState<ReadonlySet<number>>(new Set());

    if (blocks.length === 0) return <Text c="dimmed">The contents are identical.</Text>;
    return (
        <div className="line-diff">
            {blocks.map((block, index) =>
                block.folded && !expanded.has(index) ? (
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
                    block.rows.map((row) => <Row key={`${String(row.fromLine)}-${String(row.toLine)}`} row={row} />)
                ),
            )}
        </div>
    );
}
