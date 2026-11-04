const OPEN_PARAM = "open";

export function openScenariosOf(search: string): string[] {
    return [...new Set(new URLSearchParams(search).getAll(OPEN_PARAM).filter((key) => key !== ""))];
}

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
