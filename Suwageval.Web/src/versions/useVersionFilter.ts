import { useState } from "react";

import type { VersionUsage } from "../routes/links";
import { environmentOf, searchWithFilter, usageOf, versionIdsOf } from "./versionUsage";

interface Filter {
    usage: VersionUsage[];
    versionIds: string[];
    environment: string | null;
}

/** The Versions page's filter, kept in the address (`?usage=…&version=…&environment=…`) so the view can be shared; changing it does not reload the page. */
export function useVersionFilter(): Filter & {
    setUsage: (usage: VersionUsage[]) => void;
    removeVersion: (id: string) => void;
    setEnvironment: (environment: string | null) => void;
} {
    const [filter, setFilter] = useState<Filter>(() => ({
        usage: usageOf(window.location.search),
        versionIds: versionIdsOf(window.location.search),
        environment: environmentOf(window.location.search),
    }));
    const change = (next: Filter) => {
        setFilter(next);
        const { pathname, search, hash } = window.location;
        window.history.replaceState(
            null,
            "",
            `${pathname}${searchWithFilter(search, next.usage, next.versionIds, next.environment)}${hash}`,
        );
    };
    return {
        ...filter,
        setUsage: (usage) => {
            change({ ...filter, usage });
        },
        removeVersion: (id) => {
            change({ ...filter, versionIds: filter.versionIds.filter((one) => one !== id) });
        },
        setEnvironment: (environment) => {
            change({ ...filter, environment });
        },
    };
}
