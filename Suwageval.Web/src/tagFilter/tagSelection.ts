export const TAG_PARAM = "tag";

export function selectedTagsOf(search: string, param = TAG_PARAM): string[] {
    const tags = new URLSearchParams(search).getAll(param).filter((tag) => tag !== "");
    return [...new Set(tags)];
}

export function searchWithTags(search: string, tags: readonly string[], param = TAG_PARAM): string {
    const query = new URLSearchParams(search);
    query.delete(param);
    for (const tag of tags) query.append(param, tag);
    const text = query.toString();
    return text === "" ? "" : `?${text}`;
}

export function keepsTags(tags: readonly string[], selected: readonly string[]): boolean {
    return selected.length === 0 || tags.some((tag) => selected.includes(tag));
}

// Why: the row of every check is dropped with a filter because the server has no rate for any of these tags
export function selectedTagRows<Row extends { tag?: string | null }>(rows: readonly Row[], selected: readonly string[]): Row[] {
    return selected.length === 0 ? [...rows] : rows.filter((row) => row.tag != null && selected.includes(row.tag));
}

export function filteredLabel(label: string, selected: readonly string[]): string {
    return selected.length === 0 ? label : `${label} (${selected.join(", ")})`;
}
