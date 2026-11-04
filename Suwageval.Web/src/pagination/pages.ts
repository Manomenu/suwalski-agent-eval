export const DEFAULT_PAGE_SIZE = 10;

export function pageCountOf(totalCount: number, pageSize = DEFAULT_PAGE_SIZE): number {
    return Math.max(1, Math.ceil(totalCount / pageSize));
}

export function clampPage(requestedPage: number, totalCount: number, pageSize = DEFAULT_PAGE_SIZE): number {
    return Math.min(requestedPage, pageCountOf(totalCount, pageSize));
}

export function pageOfItems<T>(items: readonly T[], requestedPage: number, pageSize = DEFAULT_PAGE_SIZE): { items: T[]; page: number } {
    const page = clampPage(requestedPage, items.length, pageSize);
    return { items: items.slice((page - 1) * pageSize, page * pageSize), page };
}

export function pageFromSearch(search: string, param: string): number {
    const page = Number(new URLSearchParams(search).get(param));
    return Number.isInteger(page) && page > 1 ? page : 1;
}

export function searchWithPage(search: string, param: string, page: number): string {
    const query = new URLSearchParams(search);
    if (page > 1) query.set(param, String(page));
    else query.delete(param);
    const text = query.toString();
    return text === "" ? "" : `?${text}`;
}
