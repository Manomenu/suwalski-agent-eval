// Where a version is used, and the filter of the Versions page built on it: the rule written once here (the CLI has the same in
// `VersionUsages.cs`). The filter lives in the address: `?usage=evaluation,groups` and `?version=<id>,<id>`.
import type { VersionUsage } from "../routes/links";
import { versionGroupingOf } from "./versionGrouping";

const USAGE_PARAM = "usage";
const VERSION_PARAM = "version";
const LEGACY_PARAM = "versions";

/** The usages in the order the chips show them, with what each means (the chip's tooltip). */
export const VERSION_USAGE_OPTIONS: { value: VersionUsage; label: string; help: string }[] = [
    {
        value: "evaluation",
        label: "Evaluation",
        help: "Versions with runs in no group: the agent's ordinary runs, its history and regressions.",
    },
    {
        value: "groups",
        label: "Groups",
        help: "Versions with runs in a group (a sweep or an experiment). A version used only there shows only with this on.",
    },
    { value: "unused", label: "Unused", help: "Versions with no run yet, e.g. one just pushed." },
    {
        value: "archived",
        label: "Archived",
        help: "Archived versions: they show only with this on, and then they can be restored or deleted (with their runs).",
    },
];

/** What the page lists when the address says nothing: versions with runs outside a group and versions with no runs yet (a fresh push), as before the filter. */
const DEFAULT_USAGE: readonly VersionUsage[] = ["evaluation", "unused"];

/** The counts a version's usage is read from, and whether it is archived. */
export interface Usable {
    runs: number;
    groupRuns: number;
    archivedAt?: string | null;
}

function inOrder(words: readonly string[]): VersionUsage[] {
    return VERSION_USAGE_OPTIONS.map((option) => option.value).filter((usage) => words.includes(usage));
}

/**
 * The usages an address asks for. No parameter is the default; an empty one (`?usage=`) is none chosen; unknown words are dropped. The
 * older `?versions=any|all` still reads: any group is the groups, all versions every kind but archived.
 */
export function usageOf(search: string): VersionUsage[] {
    const query = new URLSearchParams(search);
    const value = query.get(USAGE_PARAM);
    if (value === null) {
        const legacy = versionGroupingOf(search);
        if (query.has(LEGACY_PARAM) && legacy === "any") return ["groups"];
        if (query.has(LEGACY_PARAM) && legacy === "all") return ["evaluation", "groups", "unused"];
        return [...DEFAULT_USAGE];
    }
    return inOrder(value.split(",").map((word) => word.trim()));
}

/** The version ids an address narrows the list to (`?version=a,b`); empty when it does not. */
export function versionIdsOf(search: string): string[] {
    const value = new URLSearchParams(search).get(VERSION_PARAM) ?? "";
    return value
        .split(",")
        .map((id) => id.trim())
        .filter((id) => id !== "");
}

/** The query with the usage and the version ids replaced (the default usage and no ids leave no parameter), the older parameter dropped; every other stays. */
export function searchWithFilter(search: string, usage: readonly VersionUsage[], versionIds: readonly string[]): string {
    const query = new URLSearchParams(search);
    query.delete(LEGACY_PARAM);
    query.delete(USAGE_PARAM);
    query.delete(VERSION_PARAM);
    const isDefault = usage.length === DEFAULT_USAGE.length && DEFAULT_USAGE.every((one) => usage.includes(one));
    if (!isDefault) query.set(USAGE_PARAM, inOrder(usage).join(","));
    if (versionIds.length > 0) query.set(VERSION_PARAM, versionIds.join(","));
    const text = query.toString().replaceAll("%2C", ",");
    return text === "" ? "" : `?${text}`;
}

/** The usages the chip group's values name, as the closed type. */
export function usagesOfValues(values: readonly string[]): VersionUsage[] {
    return inOrder(values);
}

/**
 * Whether a version is listed under the usages: an archived one only when `archived` is chosen, any other when it has ANY of the chosen
 * usages (evaluation: runs outside a group, groups: runs in a group, unused: no run at all).
 */
export function matchesUsage(version: Usable, usage: readonly VersionUsage[]): boolean {
    if (version.archivedAt != null) return usage.includes("archived");
    return (
        (usage.includes("evaluation") && version.runs > 0) ||
        (usage.includes("groups") && version.groupRuns > 0) ||
        (usage.includes("unused") && version.runs === 0 && version.groupRuns === 0)
    );
}

/** The versions the filter lists: within the usage, and only the named ones when ids are given. */
export function listedVersions<T extends Usable & { id: string }>(
    versions: readonly T[],
    usage: readonly VersionUsage[],
    versionIds: readonly string[],
): T[] {
    return versions.filter((version) => matchesUsage(version, usage) && (versionIds.length === 0 || versionIds.includes(version.id)));
}
