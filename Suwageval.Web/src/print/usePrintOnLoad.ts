import { useEffect } from "react";

import { printRequested, searchWithoutPrint } from "./printOnLoad";

// Why: StrictMode's second effect run cancels the first timer so the dialog opens once, and the parameter is removed first so a reload does not reopen it
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
