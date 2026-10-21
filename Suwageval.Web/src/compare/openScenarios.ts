// Which scenario rows of the comparison are expanded: the `open` parameters of the address (`?open=a&open=b`), so the view can be linked.
const OPEN_PARAM = "open";

/** The scenarios the address has open, each once, in the order given. */
export function openScenariosOf(search: string): string[] {
    return [...new Set(new URLSearchParams(search).getAll(OPEN_PARAM).filter((key) => key !== ""))];
}

/** The query with its open parameters replaced by `keys`; every other parameter stays. Empty when nothing is left. */
export function searchWithOpenScenarios(search: string, keys: readonly string[]): string {
    const query = new URLSearchParams(search);
    query.delete(OPEN_PARAM);
    for (const key of keys) query.append(OPEN_PARAM, key);
    const text = query.toString();
    return text === "" ? "" : `?${text}`;
}

export function toggledScenario(open: readonly string[], key: string): string[] {
    return open.includes(key) ? open.filter((candidate) => candidate !== key) : [...open, key];
}
