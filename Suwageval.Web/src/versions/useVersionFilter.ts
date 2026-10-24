import { useState } from "react";

import type { VersionUsage } from "../routes/links";
import { searchWithFilter, usageOf, versionIdsOf } from "./versionUsage";

/** The Versions page's filter, kept in the address (`?usage=…&version=…`) so the view can be shared; changing it does not reload the page. */
export function useVersionFilter(): {
    usage: VersionUsage[];
    versionIds: string[];
    setUsage: (usage: VersionUsage[]) => void;
    removeVersion: (id: string) => void;
} {
    const [filter, setFilter] = useState(() => ({
        usage: usageOf(window.location.search),
        versionIds: versionIdsOf(window.location.search),
    }));
    const change = (next: { usage: VersionUsage[]; versionIds: string[] }) => {
        setFilter(next);
        const { pathname, search, hash } = window.location;
        window.history.replaceState(null, "", `${pathname}${searchWithFilter(search, next.usage, next.versionIds)}${hash}`);
    };
    return {
        ...filter,
        setUsage: (usage) => {
            change({ ...filter, usage });
        },
        removeVersion: (id) => {
            change({ ...filter, versionIds: filter.versionIds.filter((one) => one !== id) });
        },
    };
}
