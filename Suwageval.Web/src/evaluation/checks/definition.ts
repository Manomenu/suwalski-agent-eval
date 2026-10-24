// What the definition drawer shows, as plain functions: the check as YAML, a one-line summary and the note that the check
// changed since the run.
import { stringify } from "yaml";

import type { CheckDefinition } from "./api";

/** Keys that open the YAML, in this order; the rest follow as stored, and these two close it. */
const FIRST_KEYS = ["key", "type"];
const LAST_KEYS = ["where", "passThreshold", "samples"];

/** Said by the page around the drawer already. */
const LEFT_OUT = ["scenarioKey"];

/**
 * The definition as YAML: `key` and `type` first, the check's own fields as stored, `where`, `passThreshold` and `samples` last; without
 * the scenario's key and the fields left unset.
 */
export function definitionYaml(definition: CheckDefinition): string {
    // The server leaves unset fields out; `where` with none of its own is left out too.
    const where = definition.where != null && Object.keys(definition.where).length > 0 ? definition.where : undefined;
    const entries = Object.entries({ ...definition, where }).filter(([name, value]) => value !== undefined && !LEFT_OUT.includes(name));
    const take = (names: string[]) => names.flatMap((name) => entries.filter(([entryName]) => entryName === name));
    const middle = entries.filter(([name]) => !FIRST_KEYS.includes(name) && !LAST_KEYS.includes(name));
    // No folding: long text stays on one line in the YAML and the drawer wraps it on screen.
    return stringify(Object.fromEntries([...take(FIRST_KEYS), ...middle, ...take(LAST_KEYS)]), { lineWidth: 0 });
}

function subject(definition: CheckDefinition): string {
    switch (definition.type) {
        case "tool-called":
        case "tool-not-called":
            return [definition.type, definition.tool, definition.by == null ? null : `by ${definition.by}`]
                .filter((part) => part !== null)
                .join(" ");
        case "event":
        case "event-not-emitted":
            return `${definition.type} ${definition.event}`;
        case "cited":
            return `cited ${definition.sources.join(", ")}`;
        case "text": {
            const parts = [
                definition.contains == null ? null : `contains ${JSON.stringify(definition.contains)}`,
                definition.notContains == null ? null : `not contains ${JSON.stringify(definition.notContains)}`,
                definition.pattern == null ? null : `pattern ${JSON.stringify(definition.pattern)}`,
            ];
            return ["text", ...parts.filter((part) => part !== null)].join(" ");
        }
        case "judge":
            return "judge";
        case undefined:
            break;
    }
    return "check";
}

/** The turn of `where` is a number, "last" or "any" (the default, not worth saying). */
function turnPart(turn: unknown): string | null {
    if (typeof turn === "number" || turn === "last") return `turn ${String(turn)}`;
    return null;
}

/** One line: `tool-called refund_order · turn 1 · after get_order · threshold 100%`; absent parts are left out; `samples` shows only when the check sets its own. */
export function definitionSummary(definition: CheckDefinition): string {
    const where = definition.where;
    const threshold = definition.passThreshold;
    const parts = [
        subject(definition),
        turnPart(where?.turn),
        where?.after == null ? null : `after ${where.after}`,
        where?.before == null ? null : `before ${where.before}`,
        threshold === undefined ? null : `threshold ${String(Math.round(threshold * 100))}%`,
        definition.samples == null ? null : `${String(definition.samples)} ${definition.samples === 1 ? "sample" : "samples"}`,
    ];
    return parts.filter((part) => part !== null).join(" · ");
}

/** Said when the check has a newer revision than the run used; `null` when the run saw the latest. */
export function revisionNote(revision: number, latest: number): string | null {
    return latest > revision ? `Changed since this run (rev ${String(revision)} → ${String(latest)})` : null;
}
