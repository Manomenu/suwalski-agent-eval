// What changed between two versions of an agent, as plain functions: a summary of the changes, and a line diff of both
// contracts written as YAML.
import { diffLines } from "diff";
import { stringify } from "yaml";

import type { AgentVersionDetail } from "./api";

type Contract = AgentVersionDetail["content"];
type LlmAgent = Contract["agents"][number];

/** The two versions, the older (the base) first; versions created at the same time stay in the order given. */
export function oldestFirst<T extends { createdAt: string }>(a: T, b: T): [T, T] {
    return Date.parse(b.createdAt) < Date.parse(a.createdAt) ? [b, a] : [a, b];
}

/**
 * The contract as YAML, in the contract's own key order. The server leaves unset fields out and a null inside a schema stays. Nothing folds (`lineWidth: 0`), and a
 * multi-line text such as a prompt becomes a block scalar (`|`), so a change inside it is a change of its own lines.
 */
export function contractYaml(content: Contract): string {
    return stringify(content, { lineWidth: 0 });
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
