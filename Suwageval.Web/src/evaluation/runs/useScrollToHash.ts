import { useEffect } from "react";

export function useScrollToHash(ready: boolean): void {
    useEffect(() => {
        if (!ready) return;
        const id = decodeURIComponent(window.location.hash.slice(1));
        if (id !== "") document.getElementById(id)?.scrollIntoView();
    }, [ready]);
}
