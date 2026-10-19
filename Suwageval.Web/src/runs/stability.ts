// Which checks are unstable: they passed in some runs of one agent version and failed in others, so a failure there is noise
// as much as a regression. The server finds them (`GET /agents/<name>/unstable-checks`); this is what the pages do with the list.
import type { UnstableCheck } from "./api";
import { versionTitle } from "./results";

/** The agent page lists at most this many unstable checks. */
export const UNSTABLE_CARD_MAX_ROWS = 8;

/** What the agent page's "Unstable checks" card lists: the checks that are still open. */
export const UNSTABLE_CARD_RULE =
    "Listed: checks unstable in the newest version (among the ones the filters show) that ran them at least twice. A row disappears once a newer version runs the check without the problem; archive a version to hide its rows.";

/** The path of the unstable checks of an agent, limited to the given versions (none: every version not archived). */
export function unstableChecksPath(agent: string, versionIds: readonly string[]): string {
    const query = new URLSearchParams();
    for (const id of versionIds) query.append("version", id);
    const text = query.toString();
    return `/agents/${encodeURIComponent(agent)}/unstable-checks${text === "" ? "" : `?${text}`}`;
}

/** The unstable check of this version, scenario, check key and check revision, if there is one. */
export function findUnstable(
    list: readonly UnstableCheck[],
    versionId: string,
    scenarioKey: string,
    checkKey: string,
    revision: number,
): UnstableCheck | undefined {
    return list.find(
        (item) =>
            item.agentVersionId === versionId &&
            item.scenarioKey === scenarioKey &&
            item.checkKey === checkKey &&
            item.checkRevision === revision,
    );
}

/** "failed in 2 of 9 runs": how often the check failed among the runs of its version. */
function failedIn(item: UnstableCheck): string {
    return `failed in ${item.runs - item.passedRuns} of ${item.runs} runs`;
}

/** "Unstable: failed in 2 of 9 runs of this version". */
export function unstableTooltip(item: UnstableCheck): string {
    return `Unstable: ${failedIn(item)} of this version`;
}

/**
 * The unstable checks of a row of the comparison, one per compared version it is unstable in. `runs` are the compared runs with the
 * results they show: each run's own check revision decides, since an edited check is another check.
 */
export function unstableInRuns(
    list: readonly UnstableCheck[],
    runs: readonly {
        run: { agentVersionId: string };
        scenarios: readonly { key: string; checks: readonly { key: string; revision: number }[] }[];
    }[],
    scenarioKey: string,
    checkKey: string,
): UnstableCheck[] {
    const found = new Map<string, UnstableCheck>();
    for (const results of runs) {
        const check = results.scenarios.find((scenario) => scenario.key === scenarioKey)?.checks.find((c) => c.key === checkKey);
        if (check === undefined) continue;
        const item = findUnstable(list, results.run.agentVersionId, scenarioKey, checkKey, check.revision);
        if (item !== undefined) found.set(item.agentVersionId, item);
    }
    return [...found.values()];
}

/** The tooltip for a comparison row: the plain one for a single version, else one clause per version. */
export function unstableRowTooltip(items: readonly UnstableCheck[]): string {
    const [only] = items;
    if (items.length === 1 && only !== undefined) return unstableTooltip(only);
    return `Unstable: ${items.map((item) => `${failedIn(item)} of version ${versionTitle(item.agentVersionId, item.versionLabel)}`).join("; ")}`;
}

/** The rows the agent page's card shows: the first ones, as the server sorted them (the most often failing first). */
export function cardRows(list: readonly UnstableCheck[]): UnstableCheck[] {
    return list.slice(0, UNSTABLE_CARD_MAX_ROWS);
}

/** "7/9": the runs the check passed in over the runs of its version. */
export function passedOfRuns(item: UnstableCheck): string {
    return `${item.passedRuns}/${item.runs}`;
}
