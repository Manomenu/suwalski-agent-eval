import { describe, expect, it } from "vitest";

import type { AgentVersionDetail } from "./api";
import { contractYaml, diffBlocks, diffRows, lineChanges, oldestFirst, summarizeChanges } from "./versionDiff";

type Contract = AgentVersionDetail["content"];

function contract(change: Partial<Contract> = {}): Contract {
    return {
        label: "v1",
        agents: [
            {
                name: "main",
                model: { name: "model-a", parameters: { temperature: 0.2, top_p: 1 } },
                config: "Be helpful.\nBe brief.\n",
                tools: [{ name: "lookup", description: "Finds an order.", parameters: { type: "object" } }],
            },
        ],
        otherConfig: "none",
        context: { usesRetrieval: false, events: [{ type: "handoff", dataSchema: { type: "object" } }] },
        ...change,
    };
}

function withAgent(change: Partial<Contract["agents"][number]>): Contract {
    const base = contract();
    const [agent] = base.agents;
    if (!agent) throw new Error("fixture has an agent");
    return { ...base, agents: [{ ...agent, ...change }] };
}

describe("oldestFirst", () => {
    it("puts the older version first whatever the order given", () => {
        const older = { id: "a", createdAt: "2026-10-01T10:00:00Z" };
        const newer = { id: "b", createdAt: "2026-10-02T10:00:00Z" };
        expect(oldestFirst(newer, older)).toEqual([older, newer]);
        expect(oldestFirst(older, newer)).toEqual([older, newer]);
    });
});

describe("contractYaml", () => {
    it("writes a multi-line config as a block scalar and never folds a long line", () => {
        const long = "word ".repeat(80).trim();
        const yaml = contractYaml(withAgent({ config: `First line.\n${long}\n` }));
        expect(yaml).toContain("config: |\n");
        expect(yaml).toContain(`      ${long}\n`);
    });

    it("leaves out unset fields, but keeps a null inside a schema the agent wrote", () => {
        const base = contract();
        const yaml = contractYaml({
            ...base,
            label: null,
            context: { ...base.context, retrieval: null, fixturesSchema: { type: "object", default: null } },
        });
        expect(yaml).not.toContain("label:");
        expect(yaml).not.toContain("retrieval:");
        expect(yaml).toContain("default: null");
    });

    it("keeps the contract's own key order", () => {
        const yaml = contractYaml(contract());
        expect(yaml.indexOf("label:")).toBeLessThan(yaml.indexOf("agents:"));
        expect(yaml.indexOf("agents:")).toBeLessThan(yaml.indexOf("otherConfig:"));
        expect(yaml.indexOf("otherConfig:")).toBeLessThan(yaml.indexOf("context:"));
    });
});

describe("lineChanges", () => {
    it("counts added and removed lines", () => {
        expect(lineChanges("a\nb\nc\n", "a\nx\ny\nc\n")).toEqual({ added: 2, removed: 1 });
        expect(lineChanges("a\n", "a\n")).toEqual({ added: 0, removed: 0 });
    });
});

describe("summarizeChanges", () => {
    it("is empty for identical contracts", () => {
        expect(summarizeChanges(contract(), contract())).toEqual([]);
    });

    it("says the label, model, parameters and config changes", () => {
        const changed = withAgent({
            model: { name: "model-b", parameters: { temperature: 0.5, top_p: 1, seed: 7 } },
            config: "Be helpful.\nBe kind.\nBe quick.\n",
        });
        expect(summarizeChanges(contract(), { ...changed, label: "v2" })).toEqual([
            'Label "v1" → "v2"',
            "Agent main: model model-a → model-b",
            "Agent main: model parameters changed (temperature, seed)",
            "Agent main: config changed (+2 −1 lines)",
        ]);
    });

    it("says agents added and removed", () => {
        const base = contract();
        const [main] = base.agents;
        if (!main) throw new Error("fixture has an agent");
        const added = { ...base, agents: [main, { ...main, name: "helper" }] };
        expect(summarizeChanges(base, added)).toEqual(["Agent helper added"]);
        expect(summarizeChanges(added, base)).toEqual(["Agent helper removed"]);
    });

    it("says tools added, removed and changed with what differs", () => {
        const tool = { name: "refund", description: "Refunds.", parameters: {} };
        const next = withAgent({
            tools: [{ name: "lookup", description: "Finds a customer.", parameters: { type: "array" } }, tool],
        });
        expect(summarizeChanges(contract(), next)).toEqual([
            "Agent main: tool lookup changed (description, parameters)",
            "Agent main: tool refund added",
        ]);
        expect(summarizeChanges(withAgent({ tools: [tool] }), contract())).toEqual([
            "Agent main: tool refund removed",
            "Agent main: tool lookup added",
        ]);
    });

    it("says other config, retrieval, events and fixtures schema", () => {
        const next = contract({
            otherConfig: "something",
            context: {
                usesRetrieval: true,
                retrieval: { dataVersion: "2026-10", description: "articles" },
                events: [{ type: "escalated", dataSchema: {} }],
                fixturesSchema: { type: "object" },
            },
        });
        expect(summarizeChanges(contract(), next)).toEqual([
            "Other config changed (+1 −1 lines)",
            "Retrieval added (data 2026-10)",
            "Event handoff removed",
            "Event escalated added",
            "Fixtures schema added",
        ]);
        expect(summarizeChanges(next, contract())).toContain("Retrieval removed (data 2026-10)");
        expect(summarizeChanges(next, contract())).toContain("Fixtures schema removed");
    });

    it("says a data version and a changed event schema or fixtures schema", () => {
        const from = contract({
            context: { retrieval: { dataVersion: "a" }, events: [{ type: "e", dataSchema: { a: 1 } }], fixturesSchema: { a: 1 } },
        });
        const to = contract({
            context: { retrieval: { dataVersion: "b" }, events: [{ type: "e", dataSchema: { a: 2 } }], fixturesSchema: { a: 2 } },
        });
        expect(summarizeChanges(from, to)).toEqual(["Retrieval data version a → b", "Event e changed", "Fixtures schema changed"]);
    });
});

describe("diffRows", () => {
    it("numbers the lines of each side", () => {
        expect(diffRows("a\nb\nc\n", "a\nx\nc\n")).toEqual([
            { kind: "unchanged", text: "a", oldNumber: 1, newNumber: 1 },
            { kind: "removed", text: "b", oldNumber: 2, newNumber: null },
            { kind: "added", text: "x", oldNumber: null, newNumber: 2 },
            { kind: "unchanged", text: "c", oldNumber: 3, newNumber: 3 },
        ]);
    });
});

describe("diffBlocks", () => {
    const lines = (count: number, prefix: string) => Array.from({ length: count }, (_, index) => `${prefix}${String(index)}\n`).join("");

    it("gives no blocks for equal texts", () => {
        expect(diffBlocks(diffRows("a\nb\n", "a\nb\n"))).toEqual([]);
    });

    it("keeps 3 lines of context and folds the rest of a long unchanged run", () => {
        const rows = diffRows(`${lines(10, "top")}old\n${lines(10, "bottom")}`, `${lines(10, "top")}new\n${lines(10, "bottom")}`);
        const blocks = diffBlocks(rows);
        expect(blocks.map((block) => [block.kind, block.rows.length])).toEqual([
            ["folded", 7],
            ["lines", 3],
            ["lines", 2],
            ["lines", 3],
            ["folded", 7],
        ]);
        expect(blocks[1]?.rows.map((row) => row.text)).toEqual(["top7", "top8", "top9"]);
    });

    it("folds the run between two changes only past 6 lines, and never a single line", () => {
        const make = (middle: number) => diffBlocks(diffRows(`x\n${lines(middle, "m")}y\n`, `X\n${lines(middle, "m")}Y\n`));
        expect(make(6).some((block) => block.kind === "folded")).toBe(false);
        expect(make(7).some((block) => block.kind === "folded")).toBe(false);
        const folded = make(8).filter((block) => block.kind === "folded");
        expect(folded.map((block) => block.rows.length)).toEqual([2]);
    });
});
