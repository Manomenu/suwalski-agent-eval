// What the variants page shows of what the server computed (`GET /runs/variants`): which runs the address asks for, and the titles,
// texts, grouping and highlighting of its table. What passed, every rate and every turn time is the server's.
import { shortId } from "../runs/results";
import type { VariantCell, VariantRun, VariantScenario } from "./api";

/** The most runs the page shows (the server's limit). */
export const VARIANTS_MAX_RUNS = 20;

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export type ValidSelection = { kind: "runs"; ids: string[] } | { kind: "group"; group: string; agent: string | null };

export type VariantsSelection = ValidSelection | { kind: "error"; message: string };

/** What `?runs=a,b` or `?group=name[&agent=name]` ask for, each as it came; or what is wrong with it. */
export function selectionOf(runs: string, group: string, agent: string): VariantsSelection {
    const ids = [
        ...new Set(
            runs
                .split(",")
                .map((id) => id.trim())
                .filter((id) => id !== ""),
        ),
    ];
    if (ids.length > 0 && group !== "") return { kind: "error", message: "Give the runs (runs) or their group (group), not both." };
    if (ids.length === 0 && group === "")
        return { kind: "error", message: "Give the runs to show as ?runs=<id>,<id> or their group as ?group=<name>." };
    if (group !== "") return { kind: "group", group, agent: agent === "" ? null : agent };
    const bad = ids.find((id) => !GUID.test(id));
    if (bad !== undefined) return { kind: "error", message: `"${bad}" is not a run id.` };
    if (ids.length > VARIANTS_MAX_RUNS) return { kind: "error", message: `Choose at most ${String(VARIANTS_MAX_RUNS)} runs.` };
    return { kind: "runs", ids };
}

/** A column's name: the label of its version (the variant), else the start of the version's id. */
export function variantName(variant: Pick<VariantRun, "versionLabel" | "run">): string {
    const label = variant.versionLabel?.trim();
    return label === undefined || label === "" ? shortId(variant.run.agentVersionId) : label;
}

/** `3/4 checks`; a dash for a run that did not run the scenario, "no checks" for a scenario without any. */
export function cellText(cell: VariantCell | null): string {
    if (cell === null) return "—";
    return cell.checksTotal === 0 ? "no checks" : `${String(cell.checksPassed)}/${String(cell.checksTotal)} checks`;
}

/** The samples where the agent gave no answer, as text; null when there is none. */
export function noAnswerText(cell: VariantCell | null): string | null {
    return cell !== null && cell.samplesFailed > 0 ? `${String(cell.samplesFailed)} of ${String(cell.samples)} samples: no answer` : null;
}

/** The share of a cell's checks that passed; null without a cell or a check. */
export function cellShare(cell: VariantCell | null): number | null {
    return cell === null || cell.checksTotal === 0 ? null : cell.checksPassed / cell.checksTotal;
}

/**
 * The columns that are best in a row: those with the highest share. Nothing is marked when fewer than two columns have a share or all
 * of them are equal — a row where every variant did the same says nothing.
 */
export function bestColumns(shares: readonly (number | null | undefined)[]): Set<number> {
    const known = shares.flatMap((share, column) => (share == null ? [] : [{ share, column }]));
    const best = Math.max(...known.map(({ share }) => share));
    if (known.length < 2 || known.every(({ share }) => share === best)) return new Set();
    return new Set(known.filter(({ share }) => share === best).map(({ column }) => column));
}

export interface ScenarioSection {
    /** The tag the scenarios are listed under; null when the table is not grouped. */
    title: string | null;
    scenarios: VariantScenario[];
}

const NO_TAG = "no tag";

/**
 * The scenarios as the table lists them: one section by key, or (`byTag`) a section per tag, each scenario under its first tag, those
 * without a tag last. Sections and scenarios are sorted by name.
 */
export function scenarioSections(scenarios: readonly VariantScenario[], byTag: boolean): ScenarioSection[] {
    const sorted = [...scenarios].sort((a, b) => a.key.localeCompare(b.key));
    if (!byTag) return [{ title: null, scenarios: sorted }];
    const sections = new Map<string, VariantScenario[]>();
    for (const scenario of sorted) {
        const title = [...scenario.tags].sort((a, b) => a.localeCompare(b))[0] ?? NO_TAG;
        sections.set(title, [...(sections.get(title) ?? []), scenario]);
    }
    return [...sections.entries()]
        .sort(([a], [b]) => (a === NO_TAG ? 1 : b === NO_TAG ? -1 : a.localeCompare(b)))
        .map(([title, grouped]) => ({ title, scenarios: grouped }));
}
