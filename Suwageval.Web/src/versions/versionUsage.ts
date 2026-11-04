import type { VersionUsage } from "../routes/links";

const USAGE_PARAM = "usage";
const VERSION_PARAM = "version";
const ENVIRONMENT_PARAM = "environment";

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

const DEFAULT_USAGE: readonly VersionUsage[] = ["evaluation", "monitoring", "unused"];

interface VersionInEnvironment {
    environment: string;
    conversations30Days: number;
    lastConversationAt: string;
    current: boolean;
}

export interface Usable {
    runs: number;
    groupRuns: number;
    archivedAt?: string | null;
    monitoring?: VersionInEnvironment[];
}

function monitoringIn(version: Usable, environment: string | null): VersionInEnvironment[] {
    return (version.monitoring ?? []).filter((entry) => environment === null || entry.environment === environment);
}

export interface MonitoringSummary {
    conversations30Days: number;
    lastConversationAt: string | null;
    currentIn: string[];
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

export function currentText(currentIn: readonly string[], environments: readonly string[]): string {
    return environments.length > 1 ? `Current · ${currentIn.join(", ")}` : "Current";
}

export function environmentsOf(versions: readonly Usable[]): string[] {
    return [...new Set(versions.flatMap((version) => (version.monitoring ?? []).map((entry) => entry.environment)))].sort();
}

export function environmentOf(search: string): string | null {
    const value = new URLSearchParams(search).get(ENVIRONMENT_PARAM)?.trim();
    return value === undefined || value === "" ? null : value;
}

function inOrder(words: readonly string[]): VersionUsage[] {
    return VERSION_USAGE_OPTIONS.map((option) => option.value).filter((usage) => words.includes(usage));
}

export function usageOf(search: string): VersionUsage[] {
    const value = new URLSearchParams(search).get(USAGE_PARAM);
    if (value === null) return [...DEFAULT_USAGE];
    return inOrder(value.split(",").map((word) => word.trim()));
}

export function versionIdsOf(search: string): string[] {
    const value = new URLSearchParams(search).get(VERSION_PARAM) ?? "";
    return value
        .split(",")
        .map((id) => id.trim())
        .filter((id) => id !== "");
}

export function searchWithFilter(
    search: string,
    usage: readonly VersionUsage[],
    versionIds: readonly string[],
    environment: string | null = null,
): string {
    const query = new URLSearchParams(search);
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

export function usagesOfValues(values: readonly string[]): VersionUsage[] {
    return inOrder(values);
}

export function matchesUsage(version: Usable, usage: readonly VersionUsage[], environment: string | null = null): boolean {
    if (version.archivedAt != null) return usage.includes("archived");
    return (
        (usage.includes("evaluation") && version.runs > 0) ||
        (usage.includes("groups") && version.groupRuns > 0) ||
        (usage.includes("monitoring") && monitoringIn(version, environment).length > 0) ||
        (usage.includes("unused") && version.runs === 0 && version.groupRuns === 0 && (version.monitoring ?? []).length === 0)
    );
}

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
