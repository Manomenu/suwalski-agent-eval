// The tag filter of the run and comparison pages: which tags are selected (the `tag` parameters of the address), which rows
// they keep and what the narrowed numbers are called. Selecting nothing keeps everything.
const TAG_PARAM = "tag";

/** The tags the address selects (`?tag=a&tag=b`), each once, in the order given. */
export function selectedTagsOf(search: string): string[] {
    const tags = new URLSearchParams(search).getAll(TAG_PARAM).filter((tag) => tag !== "");
    return [...new Set(tags)];
}

/** The query with its tag parameters replaced by `tags`; every other parameter stays. Empty when nothing is left. */
export function searchWithTags(search: string, tags: readonly string[]): string {
    const query = new URLSearchParams(search);
    query.delete(TAG_PARAM);
    for (const tag of tags) query.append(TAG_PARAM, tag);
    const text = query.toString();
    return text === "" ? "" : `?${text}`;
}

export function toggledTags(selected: readonly string[], tag: string): string[] {
    return selected.includes(tag) ? selected.filter((candidate) => candidate !== tag) : [...selected, tag];
}

/** What the chips offer: every tag the view has, and any selected tag it does not have (so it can be unselected), sorted. */
export function chipTags(present: Iterable<string>, selected: readonly string[]): string[] {
    return [...new Set([...present, ...selected])].sort((a, b) => a.localeCompare(b));
}

/** Whether a scenario with these tags stays in view: it has any selected tag, or nothing is selected. */
export function keepsTags(tags: readonly string[], selected: readonly string[]): boolean {
    return selected.length === 0 || tags.some((tag) => selected.includes(tag));
}

/**
 * The per-tag rows of a narrowed view: the selected tags only. The row of every check (no tag) is dropped with a filter: the server has
 * no rate for "any of these tags", and the narrowed counts say what the filter covers.
 */
export function selectedTagRows<Row extends { tag?: string | null }>(rows: readonly Row[], selected: readonly string[]): Row[] {
    return selected.length === 0 ? [...rows] : rows.filter((row) => row.tag != null && selected.includes(row.tag));
}

/** A label that says what a narrowed number covers: `Checks (procedure, serviceability)`; the label itself without a filter. */
export function filteredLabel(label: string, selected: readonly string[]): string {
    return selected.length === 0 ? label : `${label} (${selected.join(", ")})`;
}
