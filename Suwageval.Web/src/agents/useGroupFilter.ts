import { useState } from "react";

import { groupFilterOf, searchWithGroupFilter, type GroupFilter } from "./groupFilter";

/** The runs table's group filter, kept in the address so the view can be shared; changing it does not reload the page. */
export function useGroupFilter(): [GroupFilter, (filter: GroupFilter) => void] {
    const [filter, setFilter] = useState(() => groupFilterOf(window.location.search));
    const select = (next: GroupFilter) => {
        setFilter(next);
        const { pathname, search, hash } = window.location;
        window.history.replaceState(null, "", `${pathname}${searchWithGroupFilter(search, next)}${hash}`);
    };
    return [filter, select];
}
