export function tagOptions(present: Iterable<string>, selected: readonly string[]): string[] {
    return [...new Set([...present, ...selected])].sort((a, b) => a.localeCompare(b));
}
