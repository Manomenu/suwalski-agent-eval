import { useEffect } from "react";

/** Once the page has its content (`ready`), scrolls to the element the address's `#hash` names: a link from another page lands on it. */
export function useScrollToHash(ready: boolean): void {
    useEffect(() => {
        if (!ready) return;
        const id = decodeURIComponent(window.location.hash.slice(1));
        if (id !== "") document.getElementById(id)?.scrollIntoView();
    }, [ready]);
}
