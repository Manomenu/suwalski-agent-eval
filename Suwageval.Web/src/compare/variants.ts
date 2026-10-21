// What the comparison page shows of what the server computed (`GET /runs/variants`): which runs the address asks for, and the titles,
// texts, grouping and highlighting of its table. What passed, every rate and every turn time is the server's.
import type { ComparedRate, ComparedTag, VariantCell, VariantCheck, VariantRun, VariantScenario } from "./api";

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

/** The title of a column: the label its run's contract carried, or "Unlabelled". */
export function columnTitle(variant: Pick<VariantRun, "run">): string {
    return variant.run.label?.trim() ? variant.run.label : "Unlabelled";
}

/** What names a column where a title alone is not enough (the detail panel's headers): the label, else "Run" and the start of its id. */
export function columnName(variant: Pick<VariantRun, "run">): string {
    return variant.run.label?.trim() ? variant.run.label : `Run ${variant.run.id.slice(0, 8)}`;
}

/** The tags a run was limited to, or what it means that it has none. */
export function runTagsText(tags: readonly string[] | null | undefined): string {
    return tags?.length ? tags.join(", ") : "all scenarios";
}

export interface CellStatus {
    label: "all passed" | "some failed" | "all failed" | "no answer" | "no checks";
    color: "green" | "red" | "gray";
}

/** The badge of a cell: how its checks came out; "no answer" when the agent answered none of the samples. Null for a run without the scenario. */
export function cellStatus(cell: VariantCell | null): CellStatus | null {
    if (cell === null) return null;
    if (cell.samples > 0 && cell.samplesFailed === cell.samples) return { label: "no answer", color: "red" };
    if (cell.checksTotal === 0) return { label: "no checks", color: "gray" };
    if (cell.checksPassed === cell.checksTotal) return { label: "all passed", color: "green" };
    return cell.checksPassed === 0 ? { label: "all failed", color: "red" } : { label: "some failed", color: "red" };
}

/** Whether the runs came out differently in a scenario: not every cell has the same checks passed, failed and unanswered. */
export function scenarioDiffers(cells: readonly (VariantCell | null)[]): boolean {
    const signature = (cell: VariantCell | null) =>
        cell === null ? "none" : JSON.stringify([cell.checksPassed, cell.checksTotal, cell.failedChecks, cell.samplesFailed]);
    return new Set(cells.map(signature)).size > 1;
}

export interface CheckMatrixRow {
    key: string;
    /** One per column; null where that run's cell has no such check. */
    checks: (VariantCheck | null)[];
}

/** The checks of a scenario across the columns, in the order they first appear. */
export function checkMatrix(cells: readonly (VariantCell | null)[]): CheckMatrixRow[] {
    const keys = [...new Set(cells.flatMap((cell) => cell?.checks.map((check) => check.key) ?? []))];
    return keys.map((key) => ({ key, checks: cells.map((cell) => cell?.checks.find((check) => check.key === key) ?? null) }));
}

/** "passed" or "failed"; with the samples (`2/3`) when some passed and some did not. */
export function checkVerdictText(check: VariantCheck): string {
    const verdict = check.passed ? "passed" : "failed";
    return check.samplesPassed > 0 && check.samplesPassed < check.samples
        ? `${verdict} ${String(check.samplesPassed)}/${String(check.samples)}`
        : verdict;
}

export interface NoAnswerRow {
    column: number;
    sample: number;
    error: string | null;
}

/** The samples without an answer in a scenario, by column. */
export function noAnswerRows(cells: readonly (VariantCell | null)[]): NoAnswerRow[] {
    return cells.flatMap((cell, column) =>
        (cell?.noAnswers ?? []).map((item) => ({ column, sample: item.sample, error: item.error ?? null })),
    );
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

/** The checks and samples of one run's column over these scenarios: what a narrowed view's summary says in place of the run's own. */
export function columnTotals(
    scenarios: readonly VariantScenario[],
    column: number,
): { checksPassed: number; checksFailed: number; samplesTotal: number; samplesDone: number; samplesFailed: number } {
    const cells = scenarios.flatMap((scenario) => scenario.cells[column] ?? []);
    const sum = (count: (cell: VariantCell) => number) => cells.reduce((total, cell) => total + count(cell), 0);
    const checksPassed = sum((cell) => cell.checksPassed);
    const samplesDone = sum((cell) => cell.samples);
    return {
        checksPassed,
        checksFailed: sum((cell) => cell.checksTotal) - checksPassed,
        samplesTotal: samplesDone,
        samplesDone,
        samplesFailed: sum((cell) => cell.samplesFailed),
    };
}

export interface ScenarioSection {
    /** The tag the scenarios are listed under; null when the table is not grouped. */
    title: string | null;
    scenarios: VariantScenario[];
}

const NO_TAG = "no tag";

/**
 * The scenarios as the table lists them: one section by key, or (`byTag`) a section per tag, each scenario under its first tag, those
 * without a tag last; with `selected` tags a scenario goes under its first selected tag. Sections and scenarios are sorted by name.
 */
export function scenarioSections(
    scenarios: readonly VariantScenario[],
    byTag: boolean,
    selected: readonly string[] = [],
): ScenarioSection[] {
    const sorted = [...scenarios].sort((a, b) => a.key.localeCompare(b.key));
    if (!byTag) return [{ title: null, scenarios: sorted }];
    const sections = new Map<string, VariantScenario[]>();
    for (const scenario of sorted) {
        const candidates = selected.length === 0 ? scenario.tags : scenario.tags.filter((tag) => selected.includes(tag));
        const title = [...candidates].sort((a, b) => a.localeCompare(b))[0] ?? NO_TAG;
        sections.set(title, [...(sections.get(title) ?? []), scenario]);
    }
    return [...sections.entries()]
        .sort(([a], [b]) => (a === NO_TAG ? 1 : b === NO_TAG ? -1 : a.localeCompare(b)))
        .map(([title, grouped]) => ({ title, scenarios: grouped }));
}

/** Said on hover over the per-tag section's title. */
export const TAG_RATE_HELP = "A tag's pass rate is shown only for runs that ran all of its scenarios.";

/** The rows to show: a tag no run has a rate for says nothing, so it is left out (the row of every check stays). */
export function visibleTagRows(rows: readonly ComparedTag[]): ComparedTag[] {
    return rows.filter((row) => row.tag == null || row.rates.some((rate) => rate.passRate != null));
}

/** Why a tag has no rate in a run that ran some scenarios of it: "Not every mediacom scenario ran (30 of 44)"; null when it has a rate or no counts. */
export function coverageHint(tag: string | null | undefined, rate: ComparedRate): string | null {
    if (tag == null || rate.passRate != null || rate.scenariosRan == null || rate.scenariosOfTag == null) return null;
    return `Not every ${tag} scenario ran (${String(rate.scenariosRan)} of ${String(rate.scenariosOfTag)})`;
}
