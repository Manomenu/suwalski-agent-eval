import { useEffect } from "react";

import { printRequested, searchWithoutPrint } from "./printOnLoad";

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
