import { useEffect } from "react";

import { printRequested, searchWithoutPrint } from "./printOnLoad";

/**
 * Opens the print dialog once when `ready` (the data is on the page) and the address has `print=1`. The one-off part runs in the timer
 * the effect starts, and StrictMode's second run of the effect cancels the first timer: the dialog opens once, and the parameter is taken
 * out of the address first so a reload does not open it again.
 */
export function usePrintOnLoad(ready: boolean): void {
    useEffect(() => {
        if (!ready || !printRequested(window.location.search)) return;
        const timer = window.setTimeout(() => {
            const { pathname, search, hash } = window.location;
            window.history.replaceState(null, "", `${pathname}${searchWithoutPrint(search)}${hash}`);
            window.print();
        }, 0);
        return () => {
            window.clearTimeout(timer);
        };
    }, [ready]);
}
