// What changed between two versions of an agent, as plain functions: a summary of the changes, and a line diff of both
// contracts written as YAML.
import { diffLines } from "diff";
import { stringify } from "yaml";

import type { AgentVersionDetail } from "./api";

type Contract = AgentVersionDetail["content"];
type LlmAgent = Contract["agents"][number];

/** Unchanged lines kept around a change; a longer unchanged run folds. */
const DIFF_CONTEXT = 3;

/** The two versions, the older (the base) first; versions created at the same time stay in the order given. */
export function oldestFirst<T extends { createdAt: string }>(a: T, b: T): [T, T] {
    return Date.parse(b.createdAt) < Date.parse(a.createdAt) ? [b, a] : [a, b];
}

/** Keys whose value is a JSON Schema the agent wrote: a null inside one is part of the schema, not an unset field. */
const SCHEMA_KEYS = new Set(["parameters", "dataSchema", "fixturesSchema"]);

/** The contract without the fields it leaves unset (null), so a field that appears reads as added, not as `null` replaced. */
function withoutUnset(value: unknown): unknown {
    if (Array.isArray(value)) return value.map(withoutUnset);
    if (value === null || typeof value !== "object") return value;
    return Object.fromEntries(
        Object.entries(value)
            .filter(([, inner]) => inner != null)
            .map(([key, inner]) => [key, SCHEMA_KEYS.has(key) ? inner : withoutUnset(inner)]),
    );
}

/**
 * The contract as YAML, in the contract's own key order, without its unset fields. Nothing folds (`lineWidth: 0`), and a
 * multi-line text such as a prompt becomes a block scalar (`|`), so a change inside it is a change of its own lines.
 */
export function contractYaml(content: Contract): string {
    return stringify(withoutUnset(content), { lineWidth: 0 });
}

/** How many lines `to` adds and removes compared with `from`. */
export function lineChanges(from: string, to: string): { added: number; removed: number } {
    let added = 0;
    let removed = 0;
    for (const part of diffLines(from, to)) {
        if (part.added) added += part.count;
        else if (part.removed) removed += part.count;
    }
    return { added, removed };
}

const same = (a: unknown, b: unknown) => stringify(a) === stringify(b);

function describeLines(from: string, to: string): string {
    const { added, removed } = lineChanges(from, to);
    return `+${String(added)} −${String(removed)} lines`;
}

const isRecord = (value: unknown): value is Record<string, unknown> => value !== null && typeof value === "object" && !Array.isArray(value);

/** The keys whose values differ (present on one side only counts); an empty list when the values are not objects. */
function changedKeys(from: unknown, to: unknown): string[] {
    const before = isRecord(from) ? from : {};
    const after = isRecord(to) ? to : {};
    return [...new Set([...Object.keys(before), ...Object.keys(after)])].filter((key) => !same(before[key], after[key]));
}

function summarizeTools(agent: string, from: LlmAgent, to: LlmAgent): string[] {
    const before = new Map((from.tools ?? []).map((tool) => [tool.name, tool]));
    const after = new Map((to.tools ?? []).map((tool) => [tool.name, tool]));
    const lines: string[] = [];
    for (const name of before.keys()) {
        if (!after.has(name)) lines.push(`Agent ${agent}: tool ${name} removed`);
    }
    for (const [name, tool] of after) {
        const old = before.get(name);
        if (old === undefined) {
            lines.push(`Agent ${agent}: tool ${name} added`);
            continue;
        }
        const what = [
            old.description === tool.description ? null : "description",
            same(old.parameters, tool.parameters) ? null : "parameters",
        ].filter((part) => part !== null);
        if (what.length > 0) lines.push(`Agent ${agent}: tool ${name} changed (${what.join(", ")})`);
    }
    return lines;
}

function summarizeAgent(from: LlmAgent, to: LlmAgent): string[] {
    const name = to.name;
    const lines: string[] = [];
    if (from.model.name !== to.model.name) lines.push(`Agent ${name}: model ${from.model.name} → ${to.model.name}`);
    if (!same(from.model.parameters, to.model.parameters)) {
        const keys = changedKeys(from.model.parameters, to.model.parameters);
        lines.push(`Agent ${name}: model parameters changed${keys.length > 0 ? ` (${keys.join(", ")})` : ""}`);
    }
    if (from.config !== to.config) lines.push(`Agent ${name}: config changed (${describeLines(from.config, to.config)})`);
    return [...lines, ...summarizeTools(name, from, to)];
}

function summarizeEvents(from: Contract["context"], to: Contract["context"]): string[] {
    const before = new Map((from?.events ?? []).map((event) => [event.type, event]));
    const after = new Map((to?.events ?? []).map((event) => [event.type, event]));
    return [
        ...[...before.keys()].filter((type) => !after.has(type)).map((type) => `Event ${type} removed`),
        ...[...after].flatMap(([type, event]) => {
            const old = before.get(type);
            if (old === undefined) return [`Event ${type} added`];
            return same(old.dataSchema, event.dataSchema) ? [] : [`Event ${type} changed`];
        }),
    ];
}

function summarizeContext(from: Contract["context"], to: Contract["context"]): string[] {
    const lines: string[] = [];
    const before = from?.retrieval ?? null;
    const after = to?.retrieval ?? null;
    if (before === null && after !== null) lines.push(`Retrieval added (data ${after.dataVersion})`);
    else if (before !== null && after === null) lines.push(`Retrieval removed (data ${before.dataVersion})`);
    else if (before !== null && after !== null) {
        if (before.dataVersion !== after.dataVersion) lines.push(`Retrieval data version ${before.dataVersion} → ${after.dataVersion}`);
        if ((before.description ?? "") !== (after.description ?? "")) lines.push("Retrieval description changed");
    }
    lines.push(...summarizeEvents(from, to));
    const oldFixtures = from?.fixturesSchema ?? null;
    const newFixtures = to?.fixturesSchema ?? null;
    if (oldFixtures === null && newFixtures !== null) lines.push("Fixtures schema added");
    else if (oldFixtures !== null && newFixtures === null) lines.push("Fixtures schema removed");
    else if (!same(oldFixtures, newFixtures)) lines.push("Fixtures schema changed");
    return lines;
}

/** One line per difference, in the order of the contract; an empty list for versions with the same content. */
export function summarizeChanges(from: Contract, to: Contract): string[] {
    const lines: string[] = [];
    if ((from.label ?? "") !== (to.label ?? "")) lines.push(`Label "${from.label ?? ""}" → "${to.label ?? ""}"`);
    const before = new Map(from.agents.map((agent) => [agent.name, agent]));
    const after = new Map(to.agents.map((agent) => [agent.name, agent]));
    for (const name of before.keys()) {
        if (!after.has(name)) lines.push(`Agent ${name} removed`);
    }
    for (const [name, agent] of after) {
        const old = before.get(name);
        lines.push(...(old === undefined ? [`Agent ${name} added`] : summarizeAgent(old, agent)));
    }
    if ((from.otherConfig ?? "") !== (to.otherConfig ?? "")) {
        lines.push(`Other config changed (${describeLines(from.otherConfig ?? "", to.otherConfig ?? "")})`);
    }
    return [...lines, ...summarizeContext(from.context, to.context)];
}

export interface DiffRow {
    kind: "added" | "removed" | "unchanged";
    text: string;
    /** The line's number in the older text; null for an added line. */
    oldNumber: number | null;
    /** The line's number in the newer text; null for a removed line. */
    newNumber: number | null;
}

/** Every line of both texts, in order, with its numbers on each side. */
export function diffRows(from: string, to: string): DiffRow[] {
    const rows: DiffRow[] = [];
    let oldNumber = 1;
    let newNumber = 1;
    for (const part of diffLines(from, to)) {
        const lines = part.value.split("\n");
        if (lines[lines.length - 1] === "") lines.pop();
        for (const text of lines) {
            if (part.added) rows.push({ kind: "added", text, oldNumber: null, newNumber: newNumber++ });
            else if (part.removed) rows.push({ kind: "removed", text, oldNumber: oldNumber++, newNumber: null });
            else rows.push({ kind: "unchanged", text, oldNumber: oldNumber++, newNumber: newNumber++ });
        }
    }
    return rows;
}

/** A stretch of the diff: lines shown, or unchanged lines folded away until the reader opens them. */
export interface DiffBlock {
    kind: "lines" | "folded";
    rows: DiffRow[];
}

/**
 * The hunks: every change with `context` unchanged lines around it. A longer unchanged run folds into one block (a run that
 * would hide a single line stays as it is); texts with no change give no blocks at all.
 */
export function diffBlocks(rows: DiffRow[], context = DIFF_CONTEXT): DiffBlock[] {
    if (rows.every((row) => row.kind === "unchanged")) return [];
    const blocks: DiffBlock[] = [];
    let index = 0;
    while (index < rows.length) {
        const start = index;
        const unchanged = rows[index]?.kind === "unchanged";
        while (index < rows.length && (rows[index]?.kind === "unchanged") === unchanged) index++;
        const run = rows.slice(start, index);
        const keepBefore = start === 0 ? 0 : context;
        const keepAfter = index === rows.length ? 0 : context;
        const hidden = run.length - keepBefore - keepAfter;
        if (!unchanged || hidden < 2) {
            blocks.push({ kind: "lines", rows: run });
            continue;
        }
        if (keepBefore > 0) blocks.push({ kind: "lines", rows: run.slice(0, keepBefore) });
        blocks.push({ kind: "folded", rows: run.slice(keepBefore, run.length - keepAfter) });
        if (keepAfter > 0) blocks.push({ kind: "lines", rows: run.slice(run.length - keepAfter) });
    }
    return blocks;
}
