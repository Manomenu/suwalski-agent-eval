// Which runs the agent page's runs table shows by group: none in a group (the default), any group, all runs, or one group by name.
// It lives in the address (`?group=<name>`, `?runs=any`, `?runs=all`) so the view can be shared; no parameter means no group.
export type GroupFilter = { kind: "none" } | { kind: "any" } | { kind: "all" } | { kind: "group"; name: string };

const NO_GROUP_FILTER: GroupFilter = { kind: "none" };

const GROUP_PARAM = "group";
const RUNS_PARAM = "runs";
const SELECT_GROUP_PREFIX = "group:";

/** The filter an address asks for; a group name wins over `runs`, anything unknown is the default. */
export function groupFilterOf(search: string): GroupFilter {
    const query = new URLSearchParams(search);
    const name = query.get(GROUP_PARAM);
    if (name !== null && name !== "") return { kind: "group", name };
    const runs = query.get(RUNS_PARAM);
    return runs === "any" ? { kind: "any" } : runs === "all" ? { kind: "all" } : NO_GROUP_FILTER;
}

/** The query with the filter's parameters replaced; every other parameter stays. Empty when nothing is left. */
export function searchWithGroupFilter(search: string, filter: GroupFilter): string {
    const query = new URLSearchParams(search);
    query.delete(GROUP_PARAM);
    query.delete(RUNS_PARAM);
    if (filter.kind === "group") query.set(GROUP_PARAM, filter.name);
    else if (filter.kind === "any" || filter.kind === "all") query.set(RUNS_PARAM, filter.kind);
    const text = query.toString();
    return text === "" ? "" : `?${text}`;
}

/** What the server is asked: a group by name, or only runs in no group or in some group; nothing for all runs. */
export function groupQuery(filter: GroupFilter): { group: string | null; grouping: "None" | "Any" | null } {
    switch (filter.kind) {
        case "group":
            return { group: filter.name, grouping: null };
        case "none":
            return { group: null, grouping: "None" };
        case "any":
            return { group: null, grouping: "Any" };
        case "all":
            return { group: null, grouping: null };
    }
}

/** The value of the select's option for a filter, and back (a group is prefixed, so one named like a filter is not mistaken for it). */
export function filterValue(filter: GroupFilter): string {
    return filter.kind === "group" ? `${SELECT_GROUP_PREFIX}${filter.name}` : filter.kind;
}

export function filterOfValue(value: string | null): GroupFilter {
    if (value?.startsWith(SELECT_GROUP_PREFIX) === true) return { kind: "group", name: value.slice(SELECT_GROUP_PREFIX.length) };
    return value === "any" ? { kind: "any" } : value === "all" ? { kind: "all" } : NO_GROUP_FILTER;
}

/** "No group", "Any group", "All runs", then every group by name (a selected archived group stays so it can be unselected). */
export function filterOptions(groups: readonly { name: string; archivedAt?: string | null }[], selected: GroupFilter) {
    const names = new Set(
        groups
            .filter((group) => group.archivedAt == null || (selected.kind === "group" && selected.name === group.name))
            .map((group) => group.name),
    );
    if (selected.kind === "group") names.add(selected.name);
    return [
        { value: "none", label: "No group" },
        { value: "any", label: "Any group" },
        { value: "all", label: "All runs" },
        ...[...names].map((name) => ({ value: `${SELECT_GROUP_PREFIX}${name}`, label: name })),
    ];
}
