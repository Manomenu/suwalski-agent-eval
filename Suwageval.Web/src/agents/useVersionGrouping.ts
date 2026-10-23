import { useState } from "react";

import { searchWithVersionGrouping, versionGroupingOf, type VersionGrouping } from "./versionGrouping";

/** The Versions section's grouping, kept in the address (`?versions=any|all`) so the view can be shared; changing it does not reload the page. */
export function useVersionGrouping(): [VersionGrouping, (grouping: VersionGrouping) => void] {
    const [grouping, setGrouping] = useState(() => versionGroupingOf(window.location.search));
    const select = (next: VersionGrouping) => {
        setGrouping(next);
        const { pathname, search, hash } = window.location;
        window.history.replaceState(null, "", `${pathname}${searchWithVersionGrouping(search, next)}${hash}`);
    };
    return [grouping, select];
}
