// "What differs" of the comparison: which fields of the contract are not the same in the versions the columns ran. A plain function over
// the contracts the server stored; the line diff of the whole contract is the server's (`GET /agents/<name>/versions/diff`).
import { shortDataVersion } from "../dataVersion/dataVersion";
import type { AgentVersionDetail } from "../agents/api";

type Contract = AgentVersionDetail["content"];

/** A column's contract with the version it came from and its title (what the row shows for the column). */
export interface ColumnContract {
    versionId: string;
    contract: Contract;
}

/** One cell of a difference row: the value, or null where the column has nothing (no such agent, no retrieval). `changed`: it differs from the first column's. */
interface DifferenceCell {
    text: string | null;
    changed: boolean;
}

export interface Difference {
    label: string;
    cells: DifferenceCell[];
    /** A prompt (an agent's `config`): its cells say "changed" and the page offers the diff against the first column's version. */
    isPrompt: boolean;
}

const PRESENT = "present";
const ABSENT = "absent";
const SAME = "same";
const CHANGED = "changed";

const json = (value: unknown): string => JSON.stringify(value);

/** The keys and values of a model's parameters when they are an object, else the whole value under one key. */
function parameterEntries(parameters: unknown): [string, string][] {
    if (parameters == null) return [];
    if (typeof parameters === "object" && !Array.isArray(parameters)) {
        return Object.entries(parameters).map(([key, value]) => [key, json(value)]);
    }
    return [["parameters", json(parameters)]];
}

const lines = (text: string | null | undefined): string[] =>
    (text ?? "")
        .split("\n")
        .map((line) => line.trim())
        .filter((line) => line !== "");

/** A row of values: shown only when the columns do not all have the same one. */
/** A row of the values that differ; `show` writes a value for its cell (the comparison is on the whole value). */
function valueRow(label: string, values: (string | null)[], show: (value: string) => string = (value) => value): Difference[] {
    if (values.every((value) => value === values[0])) return [];
    return [
        {
            label,
            isPrompt: false,
            cells: values.map((value, column) => ({
                text: value === null ? null : show(value),
                changed: column > 0 && value !== values[0],
            })),
        },
    ];
}

/** A row of "present"/"absent" for a member the columns do not all have. */
function presenceRow(label: string, has: boolean[]): Difference[] {
    if (has.every((value) => value === has[0])) return [];
    return [
        {
            label,
            isPrompt: false,
            cells: has.map((value, column) => ({ text: value ? PRESENT : ABSENT, changed: column > 0 && value !== has[0] })),
        },
    ];
}

const unique = (names: string[]) => [...new Set(names)];

/**
 * The contract fields that differ between the columns, the first column being the one the others are read against: per LLM agent its
 * model, each model parameter, its tools (added, removed, changed) and its prompt (just "changed"); the lines of `otherConfig` that
 * not every column has; the retrieval data version. Nothing for columns of one version.
 */
export function contractDifferences(columns: readonly ColumnContract[]): Difference[] {
    const contracts = columns.map((column) => column.contract);
    const differences: Difference[] = [];

    const agentNames = unique(contracts.flatMap((contract) => contract.agents.map((agent) => agent.name)));
    for (const name of agentNames) {
        const agents = contracts.map((contract) => contract.agents.find((agent) => agent.name === name));
        differences.push(
            ...presenceRow(
                `Agent ${name}`,
                agents.map((agent) => agent !== undefined),
            ),
        );
        differences.push(
            ...valueRow(
                `Agent ${name} · model`,
                agents.map((agent) => agent?.model.name ?? null),
            ),
        );

        const parameters = agents.map((agent) => new Map(parameterEntries(agent?.model.parameters)));
        for (const key of unique(parameters.flatMap((entries) => [...entries.keys()]))) {
            differences.push(
                ...valueRow(
                    `Agent ${name} · ${key}`,
                    parameters.map((entries) => entries.get(key) ?? null),
                ),
            );
        }

        const prompts = agents.map((agent) => agent?.config ?? null);
        if (!prompts.every((prompt) => prompt === prompts[0])) {
            differences.push({
                label: `Agent ${name} · prompt`,
                isPrompt: true,
                cells: prompts.map((prompt, column) => ({
                    text: prompt === null ? null : column === 0 ? SAME : prompt === prompts[0] ? SAME : CHANGED,
                    changed: column > 0 && prompt !== prompts[0],
                })),
            });
        }

        const tools = agents.map(
            (agent) => new Map((agent?.tools ?? []).map((tool) => [tool.name, json([tool.description, tool.parameters])])),
        );
        for (const tool of unique(tools.flatMap((entries) => [...entries.keys()]))) {
            const label = `Agent ${name} · tool ${tool}`;
            const has = tools.map((entries) => entries.has(tool));
            if (has.every(Boolean)) {
                const definitions = tools.map((entries) => entries.get(tool) ?? null);
                differences.push(
                    ...valueRow(label, definitions).map((row) => ({
                        ...row,
                        cells: row.cells.map((cell) => ({ ...cell, text: cell.text === null ? null : cell.changed ? CHANGED : SAME })),
                    })),
                );
            } else {
                differences.push(...presenceRow(label, has));
            }
        }
    }

    const otherLines = contracts.map((contract) => lines(contract.otherConfig));
    for (const line of unique(otherLines.flat())) {
        differences.push(
            ...presenceRow(
                `Other config: ${line}`,
                otherLines.map((own) => own.includes(line)),
            ),
        );
    }

    differences.push(
        ...valueRow(
            "Retrieval data version",
            contracts.map((contract) => contract.context?.retrieval?.dataVersion ?? null),
            shortDataVersion,
        ),
    );
    return differences;
}
