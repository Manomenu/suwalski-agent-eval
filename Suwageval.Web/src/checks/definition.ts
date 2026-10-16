// What the definition drawer shows, as plain functions: the check as YAML, a one-line summary and the note that the check
// changed since the run.
import { stringify } from "yaml";

import type { CheckDefinition } from "./api";

/** Keys that open the YAML, in this order; the rest follow as stored, and these two close it. */
const FIRST_KEYS = ["key", "type"];
const LAST_KEYS = ["where", "passThreshold"];

/** The definition as YAML: `key` and `type` first, the check's own fields as stored, `where` and `passThreshold` last. */
export function definitionYaml(definition: CheckDefinition): string {
    const entries = Object.entries(definition);
    const take = (names: string[]) => names.flatMap((name) => entries.filter(([entryName]) => entryName === name));
    const middle = entries.filter(([name]) => !FIRST_KEYS.includes(name) && !LAST_KEYS.includes(name));
    return stringify(Object.fromEntries([...take(FIRST_KEYS), ...middle, ...take(LAST_KEYS)]));
}

function subject(definition: CheckDefinition): string {
    switch (definition.type) {
        case "tool-called":
        case "tool-not-called":
            return `${definition.type} ${definition.tool}`;
        case "event":
            return `event ${definition.event}`;
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
        default:
            return "check";
    }
}

/** The turn of `where` is a number, "last" or "any" (the default, not worth saying). */
function turnPart(turn: unknown): string | null {
    if (typeof turn === "number" || turn === "last") return `turn ${String(turn)}`;
    return null;
}

/** One line: `tool-called refund_order · turn 1 · after get_order · threshold 100%`; absent parts are left out. */
export function definitionSummary(definition: CheckDefinition): string {
    const where = definition.where;
    const threshold = definition.passThreshold;
    const parts = [
        subject(definition),
        turnPart(where?.turn),
        where?.after == null ? null : `after ${where.after}`,
        where?.before == null ? null : `before ${where.before}`,
        threshold === undefined ? null : `threshold ${String(Math.round(Number(threshold) * 100))}%`,
    ];
    return parts.filter((part) => part !== null).join(" · ");
}

/** Said when the check has a newer revision than the run used; `null` when the run saw the latest. */
export function revisionNote(revision: number, latest: number): string | null {
    return latest > revision ? `Changed since this run (rev ${String(revision)} → ${String(latest)})` : null;
}
