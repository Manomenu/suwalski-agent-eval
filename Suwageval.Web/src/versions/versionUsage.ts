// Where a version is used, and the filter of the Versions page built on it: the rule written once here (the CLI has the same in
// `VersionUsages.cs`). The filter lives in the address: `?usage=evaluation,groups`, `?version=<id>,<id>` and `?environment=<name>`.
import type { VersionUsage } from "../routes/links";
import { versionGroupingOf } from "./versionGrouping";

const USAGE_PARAM = "usage";
const VERSION_PARAM = "version";
const LEGACY_PARAM = "versions";
const ENVIRONMENT_PARAM = "environment";

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
    {
        value: "monitoring",
        label: "Monitoring",
        help: "Versions that answered conversations of the deployed agent (in the chosen environment, when one is).",
    },
    { value: "unused", label: "Unused", help: "Versions with no run and no conversation yet, e.g. one just pushed." },
    {
        value: "archived",
        label: "Archived",
        help: "Archived versions: they show only with this on, and then they can be restored or deleted (with their runs).",
    },
];

/** What the page lists when the address says nothing: versions with runs outside a group, versions answering the deployed agent and versions not used yet (a fresh push). */
const DEFAULT_USAGE: readonly VersionUsage[] = ["evaluation", "monitoring", "unused"];

/** Where a version answered monitored conversations, in one environment. */
interface VersionInEnvironment {
    environment: string;
    conversations30Days: number;
    lastConversationAt: string;
    current: boolean;
}

/** The counts a version's usage is read from, and whether it is archived. */
export interface Usable {
    runs: number;
    groupRuns: number;
    archivedAt?: string | null;
    monitoring?: VersionInEnvironment[];
}

/** The version's monitoring in the environment, or in every environment when none is chosen. */
function monitoringIn(version: Usable, environment: string | null): VersionInEnvironment[] {
    return (version.monitoring ?? []).filter((entry) => environment === null || entry.environment === environment);
}

/** What the Versions table shows of a version's monitoring (in the environment, when one is chosen). */
export interface MonitoringSummary {
    conversations30Days: number;
    /** When it last answered a turn; null: it answered none. */
    lastConversationAt: string | null;
    /** The environments where it answers now. */
    currentIn: string[];
    /** It answered conversations and has no run at all: nobody evaluated what is answering. */
    notEvaluated: boolean;
}

export function monitoringSummary(version: Usable, environment: string | null): MonitoringSummary {
    const entries = monitoringIn(version, environment);
    const last = entries.map((entry) => entry.lastConversationAt).sort();
    return {
        conversations30Days: entries.reduce((sum, entry) => sum + entry.conversations30Days, 0),
        lastConversationAt: last.at(-1) ?? null,
        currentIn: entries.filter((entry) => entry.current).map((entry) => entry.environment),
        notEvaluated: entries.length > 0 && version.runs === 0 && version.groupRuns === 0,
    };
}

/** The text of the Current chip: the environment named only when the agent is monitored in more than one. */
export function currentText(currentIn: readonly string[], environments: readonly string[]): string {
    return environments.length > 1 ? `Current · ${currentIn.join(", ")}` : "Current";
}

/** The environments the versions answered conversations in, sorted: what the environment filter offers. */
export function environmentsOf(versions: readonly Usable[]): string[] {
    return [...new Set(versions.flatMap((version) => (version.monitoring ?? []).map((entry) => entry.environment)))].sort();
}

/** The environment an address narrows the monitoring to (`?environment=production`); null: every one. */
export function environmentOf(search: string): string | null {
    const value = new URLSearchParams(search).get(ENVIRONMENT_PARAM)?.trim();
    return value === undefined || value === "" ? null : value;
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

/**
 * The query with the usage, the version ids and the environment replaced (the default usage, no ids and no environment leave no parameter),
 * the older parameter dropped; every other stays.
 */
export function searchWithFilter(
    search: string,
    usage: readonly VersionUsage[],
    versionIds: readonly string[],
    environment: string | null = null,
): string {
    const query = new URLSearchParams(search);
    query.delete(LEGACY_PARAM);
    query.delete(USAGE_PARAM);
    query.delete(VERSION_PARAM);
    query.delete(ENVIRONMENT_PARAM);
    const isDefault = usage.length === DEFAULT_USAGE.length && DEFAULT_USAGE.every((one) => usage.includes(one));
    if (!isDefault) query.set(USAGE_PARAM, inOrder(usage).join(","));
    if (versionIds.length > 0) query.set(VERSION_PARAM, versionIds.join(","));
    if (environment !== null) query.set(ENVIRONMENT_PARAM, environment);
    const text = query.toString().replaceAll("%2C", ",");
    return text === "" ? "" : `?${text}`;
}

/** The usages the chip group's values name, as the closed type. */
export function usagesOfValues(values: readonly string[]): VersionUsage[] {
    return inOrder(values);
}

/**
 * Whether a version is listed under the usages: an archived one only when `archived` is chosen, any other when it has ANY of the chosen
 * usages (evaluation: runs outside a group, groups: runs in a group, monitoring: it answered conversations, in the environment when one is
 * chosen; unused: no run and no conversation anywhere).
 */
export function matchesUsage(version: Usable, usage: readonly VersionUsage[], environment: string | null = null): boolean {
    if (version.archivedAt != null) return usage.includes("archived");
    return (
        (usage.includes("evaluation") && version.runs > 0) ||
        (usage.includes("groups") && version.groupRuns > 0) ||
        (usage.includes("monitoring") && monitoringIn(version, environment).length > 0) ||
        (usage.includes("unused") && version.runs === 0 && version.groupRuns === 0 && (version.monitoring ?? []).length === 0)
    );
}

/** The versions the filter lists: within the usage, and only the named ones when ids are given. */
export function listedVersions<T extends Usable & { id: string }>(
    versions: readonly T[],
    usage: readonly VersionUsage[],
    versionIds: readonly string[],
    environment: string | null = null,
): T[] {
    return versions.filter(
        (version) => matchesUsage(version, usage, environment) && (versionIds.length === 0 || versionIds.includes(version.id)),
    );
}
