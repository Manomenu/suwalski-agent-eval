import { shortDataVersion } from "../../dataVersion/shortDataVersion";
import type { AgentVersionDetail } from "../../agents/api";

type Contract = AgentVersionDetail["content"];

export interface ColumnContract {
    versionId: string;
    contract: Contract;
    dataVersion?: string | null;
}

interface DifferenceCell {
    text: string | null;
    changed: boolean;
}

export interface Difference {
    label: string;
    cells: DifferenceCell[];
    isPrompt: boolean;
}

const PRESENT = "present";
const ABSENT = "absent";
const SAME = "same";
const CHANGED = "changed";

const json = (value: unknown): string => JSON.stringify(value);

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

function valueRow(
    reference: number,
    label: string,
    values: (string | null)[],
    show: (value: string) => string = (value) => value,
): Difference[] {
    if (values.every((value) => value === values[0])) return [];
    return [
        {
            label,
            isPrompt: false,
            cells: values.map((value, column) => ({
                text: value === null ? null : show(value),
                changed: column !== reference && value !== values[reference],
            })),
        },
    ];
}

function presenceRow(reference: number, label: string, has: boolean[]): Difference[] {
    if (has.every((value) => value === has[0])) return [];
    return [
        {
            label,
            isPrompt: false,
            cells: has.map((value, column) => ({
                text: value ? PRESENT : ABSENT,
                changed: column !== reference && value !== has[reference],
            })),
        },
    ];
}

const unique = (names: string[]) => [...new Set(names)];

type Tool = NonNullable<Contract["agents"][number]["tools"]>[number];

function toolRows(reference: number, owner: string, owned: Tool[][]): Difference[] {
    const tools = owned.map((list) => new Map(list.map((tool) => [tool.name, json([tool.description, tool.parameters])])));
    return unique(tools.flatMap((entries) => [...entries.keys()])).flatMap((tool) => {
        const label = `${owner} ${tool}`;
        const has = tools.map((entries) => entries.has(tool));
        if (!has.every(Boolean)) return presenceRow(reference, label, has);
        return valueRow(
            reference,
            label,
            tools.map((entries) => entries.get(tool) ?? null),
        ).map((row) => ({
            ...row,
            cells: row.cells.map((cell) => ({ ...cell, text: cell.text === null ? null : cell.changed ? CHANGED : SAME })),
        }));
    });
}

export function contractDifferences(columns: readonly ColumnContract[], reference = 0): Difference[] {
    const contracts = columns.map((column) => column.contract);
    const differences: Difference[] = [];

    const agentNames = unique(contracts.flatMap((contract) => contract.agents.map((agent) => agent.name)));
    for (const name of agentNames) {
        const agents = contracts.map((contract) => contract.agents.find((agent) => agent.name === name));
        differences.push(
            ...presenceRow(
                reference,
                `Agent ${name}`,
                agents.map((agent) => agent !== undefined),
            ),
        );
        differences.push(
            ...valueRow(
                reference,
                `Agent ${name} · model`,
                agents.map((agent) => agent?.model.name ?? null),
            ),
        );

        const parameters = agents.map((agent) => new Map(parameterEntries(agent?.model.parameters)));
        for (const key of unique(parameters.flatMap((entries) => [...entries.keys()]))) {
            differences.push(
                ...valueRow(
                    reference,
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
                    text: prompt === null ? null : column === reference || prompt === prompts[reference] ? SAME : CHANGED,
                    changed: column !== reference && prompt !== prompts[reference],
                })),
            });
        }

        differences.push(
            ...toolRows(
                reference,
                `Agent ${name} · tool`,
                agents.map((agent) => agent?.tools ?? []),
            ),
        );
    }

    differences.push(
        ...toolRows(
            reference,
            "System's code (no agent) · tool",
            contracts.map((contract) => contract.context?.tools ?? []),
        ),
    );

    const otherLines = contracts.map((contract) => lines(contract.otherConfig));
    for (const line of unique(otherLines.flat())) {
        differences.push(
            ...presenceRow(
                reference,
                `Other config: ${line}`,
                otherLines.map((own) => own.includes(line)),
            ),
        );
    }

    differences.push(
        ...valueRow(
            reference,
            "Retrieval data version",
            columns.map((column) => column.dataVersion ?? null),
            shortDataVersion,
        ),
    );
    return differences;
}
