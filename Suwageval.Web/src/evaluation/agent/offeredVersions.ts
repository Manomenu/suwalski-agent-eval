import type { GroupFilter } from "./groupFilter";

export type VersionGrouping = "none" | "any" | "all";

interface Counted {
    runs: number;
    groupRuns: number;
}

function inGrouping(version: Counted, grouping: VersionGrouping): boolean {
    if (grouping === "none") return version.runs > 0 || version.groupRuns === 0;
    if (grouping === "any") return version.groupRuns > 0;
    return true;
}

// Why: filters also offer what they already chose, so a choice never disappears when the table's group filter changes
export function offeredVersions<T extends Counted & { id: string; modelNames: string[]; data?: readonly { version: string }[] }>(
    versions: T[],
    grouping: VersionGrouping,
    models: readonly string[],
    dataVersions: readonly string[],
    versionIds: readonly string[],
): T[] {
    return versions.filter(
        (version) =>
            inGrouping(version, grouping) ||
            versionIds.includes(version.id) ||
            version.modelNames.some((model) => models.includes(model)) ||
            (version.data ?? []).some((data) => dataVersions.includes(data.version)),
    );
}

export function versionGroupingFor(filter: GroupFilter): VersionGrouping {
    return filter.kind === "none" || filter.kind === "any" ? filter.kind : "all";
}
