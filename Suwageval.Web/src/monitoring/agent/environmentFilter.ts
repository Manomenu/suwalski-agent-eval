const ENVIRONMENT_PARAM = "environment";
const CONVERSATIONS_PAGE_PARAM = "conversationsPage";

export function environmentOf(search: string): string | null {
    const value = new URLSearchParams(search).get(ENVIRONMENT_PARAM)?.trim();
    return value === undefined || value === "" ? null : value;
}

export function searchWithEnvironment(search: string, environment: string | null): string {
    const query = new URLSearchParams(search);
    query.delete(CONVERSATIONS_PAGE_PARAM);
    if (environment === null) query.delete(ENVIRONMENT_PARAM);
    else query.set(ENVIRONMENT_PARAM, environment);
    const text = query.toString();
    return text === "" ? "" : `?${text}`;
}
