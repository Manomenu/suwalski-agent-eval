const PRINT_PARAM = "print";

export function printRequested(search: string): boolean {
    return new URLSearchParams(search).get(PRINT_PARAM) === "1";
}

export function searchWithoutPrint(search: string): string {
    const query = new URLSearchParams(search);
    query.delete(PRINT_PARAM);
    const text = query.toString();
    return text === "" ? "" : `?${text}`;
}
