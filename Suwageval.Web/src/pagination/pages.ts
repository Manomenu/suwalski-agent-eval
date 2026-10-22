// How every list pages: numbered pages of a fixed size, the page number in the address, a page past the last shown as the last.

export const DEFAULT_PAGE_SIZE = 10;

export function pageCountOf(totalCount: number, pageSize = DEFAULT_PAGE_SIZE): number {
    return Math.max(1, Math.ceil(totalCount / pageSize));
}

/** The page to show: the requested one, or the last when the list got shorter (items deleted or hidden) than the address says. */
export function clampPage(requestedPage: number, totalCount: number, pageSize = DEFAULT_PAGE_SIZE): number {
    return Math.min(requestedPage, pageCountOf(totalCount, pageSize));
}

/** One page of a list held in full, in its order. */
export function pageOfItems<T>(items: readonly T[], requestedPage: number, pageSize = DEFAULT_PAGE_SIZE): { items: T[]; page: number } {
    const page = clampPage(requestedPage, items.length, pageSize);
    return { items: items.slice((page - 1) * pageSize, page * pageSize), page };
}

/** The page an address asks for in query parameter `param` (1-based); a missing or malformed value is the first page. */
export function pageFromSearch(search: string, param: string): number {
    const page = Number(new URLSearchParams(search).get(param));
    return Number.isInteger(page) && page > 1 ? page : 1;
}

/** The query with `param` set to `page`; every other parameter stays. The first page leaves no parameter. */
export function searchWithPage(search: string, param: string, page: number): string {
    const query = new URLSearchParams(search);
    if (page > 1) query.set(param, String(page));
    else query.delete(param);
    const text = query.toString();
    return text === "" ? "" : `?${text}`;
}
