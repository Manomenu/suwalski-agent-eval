import { describe, expect, it } from "vitest";

import { contractDifferences, type ColumnContract } from "./contractDiff";

type Contract = ColumnContract["contract"];

const agent = (overrides: Partial<Contract["agents"][number]> = {}): Contract["agents"][number] => ({
    name: "main",
    model: { name: "model-a", parameters: { reasoningEffort: "low", temperature: 0 } },
    config: "Be helpful.",
    tools: [{ name: "lookup", description: "Finds things", parameters: {} }],
    ...overrides,
});

const column = (versionId: string, contract: Partial<Contract> = {}, dataVersion: string | null = null): ColumnContract => ({
    versionId,
    contract: { agents: [agent()], ...contract },
    dataVersion,
});

const summary = (columns: ColumnContract[]) =>
    contractDifferences(columns).map((row) => [row.label, row.cells.map((cell) => cell.text)] as const);

describe("contractDifferences", () => {
    it("reads every column against the reference one (the baseline), not the first", () => {
        const b = column("b", { agents: [agent({ model: { name: "model-b", parameters: { reasoningEffort: "low", temperature: 0 } } })] });
        const rows = contractDifferences([column("a"), b, column("c")], 1);
        const model = rows.find((row) => row.label === "Agent main · model");

        expect(model?.cells.map((cell) => [cell.text, cell.changed])).toEqual([
            ["model-a", true],
            ["model-b", false],
            ["model-a", true],
        ]);
    });

    it("has nothing for columns with the same contract", () => {
        expect(contractDifferences([column("a"), column("b")])).toEqual([]);
    });

    it("names the model and the model parameters that differ, and only those", () => {
        const other = column("b", {
            agents: [agent({ model: { name: "model-b", parameters: { reasoningEffort: "high", temperature: 0 } } })],
        });

        expect(summary([column("a"), other])).toEqual([
            ["Agent main · model", ["model-a", "model-b"]],
            ["Agent main · reasoningEffort", ['"low"', '"high"']],
        ]);
    });

    it("marks a changed prompt without printing it, and tools that were added, removed or changed", () => {
        const other = column("b", {
            agents: [
                agent({
                    config: "Be brief.",
                    tools: [
                        { name: "lookup", description: "Finds more things", parameters: {} },
                        { name: "refund", description: "Refunds", parameters: {} },
                    ],
                }),
            ],
        });

        const rows = contractDifferences([column("a"), other]);

        expect(rows.find((row) => row.isPrompt)?.cells.map((cell) => cell.text)).toEqual(["same", "changed"]);
        expect(rows.filter((row) => !row.isPrompt).map((row) => [row.label, row.cells.map((cell) => cell.text)])).toEqual([
            ["Agent main · tool lookup", ["same", "changed"]],
            ["Agent main · tool refund", ["absent", "present"]],
        ]);
    });

    it("lists the otherConfig lines not every column has, and the retrieval data version of the runs", () => {
        const first = column("a", { otherConfig: "Keep it short.\nUse metric units." }, "d1");
        const second = column("b", { otherConfig: "Use metric units." }, "d2");

        expect(summary([first, second])).toEqual([
            ["Other config: Keep it short.", ["present", "absent"]],
            ["Retrieval data version", ["d1", "d2"]],
        ]);
    });

    it("shows runs of one version on other data", () => {
        expect(summary([column("a", {}, "d1"), column("a", {}, "d2")])).toEqual([["Retrieval data version", ["d1", "d2"]]]);
    });

    it("shows an agent one column does not have as absent", () => {
        const second = column("b", { agents: [agent(), agent({ name: "judge", tools: [] })] });

        expect(summary([column("a"), second]).filter(([label]) => label === "Agent judge")).toEqual([
            ["Agent judge", ["absent", "present"]],
        ]);
    });
});
