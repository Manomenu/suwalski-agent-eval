import { useState } from "react";

import { pageFromSearch, searchWithPage } from "./pages";

export function usePageParam(param: string): [number, (page: number) => void] {
    const [page, setPage] = useState(() => pageFromSearch(window.location.search, param));
    const select = (next: number) => {
        setPage(next);
        const { pathname, search, hash } = window.location;
        window.history.replaceState(null, "", `${pathname}${searchWithPage(search, param, next)}${hash}`);
    };
    return [page, select];
}
