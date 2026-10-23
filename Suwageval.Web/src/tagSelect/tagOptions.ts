/** What a tag field offers: every tag there is, and any picked tag no longer among them (so it can be unpicked), sorted. */
export function tagOptions(present: Iterable<string>, selected: readonly string[]): string[] {
    return [...new Set([...present, ...selected])].sort((a, b) => a.localeCompare(b));
}
