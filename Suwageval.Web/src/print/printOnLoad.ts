// `&print=1` in the address (`suwageval results --open --print`): the page opens the print dialog once, when its data is shown.
const PRINT_PARAM = "print";

/** Whether the address asks for the print dialog. */
export function printRequested(search: string): boolean {
    return new URLSearchParams(search).get(PRINT_PARAM) === "1";
}

/** The query without the print parameter (a reload does not print again); every other parameter stays. Empty when nothing is left. */
export function searchWithoutPrint(search: string): string {
    const query = new URLSearchParams(search);
    query.delete(PRINT_PARAM);
    const text = query.toString();
    return text === "" ? "" : `?${text}`;
}
