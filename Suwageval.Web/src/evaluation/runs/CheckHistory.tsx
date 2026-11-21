import { Anchor, Tooltip } from "@maniumek/suwfrontkit";

import type { CheckRunResult } from "./api";
import { CHECK_HISTORY_EXPLANATION, historyMarks, type HistoryMark } from "./regression";

function Glyph({ outcome }: { outcome: HistoryMark["outcome"] }) {
    switch (outcome) {
        case "passed":
            return <span className="size-2.5 rounded-full bg-success" />;
        case "failed":
            return <span className="text-sm leading-none font-black text-destructive">✗</span>;
        case "agentError":
            return <span className="text-sm leading-none font-black text-muted-foreground">–</span>;
    }
}

function Mark({ mark }: { mark: HistoryMark }) {
    return (
        <Tooltip
            label={
                <div className="flex flex-col gap-1">
                    <p className="text-sm font-semibold">{mark.tooltip}</p>
                    <p className="text-xs">{CHECK_HISTORY_EXPLANATION}</p>
                </div>
            }
        >
            {mark.href === null ? (
                <span aria-label={mark.label} role="img" className="inline-flex">
                    <Glyph outcome={mark.outcome} />
                </span>
            ) : (
                <Anchor href={mark.href} aria-label={mark.label} className="inline-flex">
                    <Glyph outcome={mark.outcome} />
                </Anchor>
            )}
        </Tooltip>
    );
}

export function CheckHistory({ check }: { check: Pick<CheckRunResult, "passed" | "regression"> }) {
    const marks = historyMarks(check);
    if (marks.length === 0) return null;
    return (
        <div className="flex items-center gap-1" role="group" aria-label="History of the check">
            {marks.map((mark, index) => (
                <div key={mark.key} className="flex items-center gap-1">
                    {index === marks.length - 1 && <span className="text-xs text-muted-foreground">→</span>}
                    <Mark mark={mark} />
                </div>
            ))}
        </div>
    );
}
