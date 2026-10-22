import { useState } from "react";

import { groupsPageOf, searchWithGroupsPage } from "./groups";

/** The Groups section's page, kept in the address so the view can be shared; changing it does not reload the page. */
export function useGroupsPage(): [number, (page: number) => void] {
    const [page, setPage] = useState(() => groupsPageOf(window.location.search));
    const select = (next: number) => {
        setPage(next);
        const { pathname, search, hash } = window.location;
        window.history.replaceState(null, "", `${pathname}${searchWithGroupsPage(search, next)}${hash}`);
    };
    return [page, select];
}
