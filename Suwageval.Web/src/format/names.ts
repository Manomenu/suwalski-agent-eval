// How an id or a version is named in a table or a title.

/** The first eight characters of a guid — enough to tell runs and versions apart in a table. */
export function shortId(id: string): string {
    return id.slice(0, 8);
}

/** What names a version in its page's title: its label, or the start of its id when it has none. */
export function versionTitle(id: string, label: string | null | undefined): string {
    return label?.trim() ? label : shortId(id);
}
