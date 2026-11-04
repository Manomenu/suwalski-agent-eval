import { versionTitle } from "../../format/names";
import { type UnstableCheck } from "./api";

export const UNSTABLE_CARD_PAGE_SIZE = 8;

export const UNSTABLE_CARD_RULE =
    "Listed: checks unstable in the newest version (among the ones the filters show) that ran them at least twice. A row disappears once a newer version runs the check without the problem; archive a version to hide its rows.";

export function unstableChecksPath(agent: string, versionIds: readonly string[], dataVersions: readonly string[] = []): string {
    const query = new URLSearchParams();
    for (const id of versionIds) query.append("version", id);
    for (const data of dataVersions) query.append("dataVersion", data);
    const text = query.toString();
    return `/eval/agents/${encodeURIComponent(agent)}/unstable-checks${text === "" ? "" : `?${text}`}`;
}

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

function failedIn(item: UnstableCheck): string {
    return `failed in ${item.runs - item.passedRuns} of ${item.runs} runs`;
}

export function unstableTooltip(item: UnstableCheck): string {
    return `Unstable: ${failedIn(item)} of this version`;
}

export function unstableInRuns(
    list: readonly UnstableCheck[],
    versionIds: readonly string[],
    cells: readonly ({ revision: number } | null)[],
    scenarioKey: string,
    checkKey: string,
): UnstableCheck[] {
    const found = new Map<string, UnstableCheck>();
    for (const [column, versionId] of versionIds.entries()) {
        const cell = cells[column];
        if (cell == null) continue;
        const item = findUnstable(list, versionId, scenarioKey, checkKey, cell.revision);
        if (item !== undefined) found.set(item.agentVersionId, item);
    }
    return [...found.values()];
}

export function unstableRowTooltip(items: readonly UnstableCheck[]): string {
    const [only] = items;
    if (items.length === 1 && only !== undefined) return unstableTooltip(only);
    return `Unstable: ${items.map((item) => `${failedIn(item)} of version ${versionTitle(item.agentVersionId, item.versionLabel)}`).join("; ")}`;
}

export function passedOfRuns(item: UnstableCheck): string {
    return `${item.passedRuns}/${item.runs}`;
}
