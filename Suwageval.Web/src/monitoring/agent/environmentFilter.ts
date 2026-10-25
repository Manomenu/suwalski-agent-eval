// The environment the whole Monitoring page is narrowed to, kept in the address (`?environment=production`) so the view can be shared.

const ENVIRONMENT_PARAM = "environment";
/** The conversations' page: a new environment starts the list from its first page. */
const CONVERSATIONS_PAGE_PARAM = "conversationsPage";

/** The environment an address narrows the page to; null: all of them. */
export function environmentOf(search: string): string | null {
    const value = new URLSearchParams(search).get(ENVIRONMENT_PARAM)?.trim();
    return value === undefined || value === "" ? null : value;
}

/** The query with the environment replaced (none: all, no parameter) and the conversations' page reset; every other parameter stays. */
export function searchWithEnvironment(search: string, environment: string | null): string {
    const query = new URLSearchParams(search);
    query.delete(CONVERSATIONS_PAGE_PARAM);
    if (environment === null) query.delete(ENVIRONMENT_PARAM);
    else query.set(ENVIRONMENT_PARAM, environment);
    const text = query.toString();
    return text === "" ? "" : `?${text}`;
}
